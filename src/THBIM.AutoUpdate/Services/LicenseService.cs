using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using THBIM.AutoUpdate.Helpers;

namespace THBIM.AutoUpdate.Services;

public class LicenseService
{
    private static readonly string AppFolder =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "THBIM", "Licensing");

    private const string SessionFile = "session_v2_5.lic";
    private const string VerifyStampFile = "verify.stamp";
    private const string ConsentStampFile = "consent.stamp";
    private const string MigrationStampFile = "migrated_v2.stamp";

    // Backend endpoint resolved at runtime — see LicenseEndpointConfig
    private static string LicenseServer => LicenseEndpointConfig.Resolve();

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(20) };

    // Run the same one-shot migration that LicenseManager runs in the addin.
    // Invoked from LicenseService construction so AutoUpdate also wipes
    // pre-v2.0 unredacted log file even if the user never opens Revit.
    static LicenseService()
    {
        try
        {
            Directory.CreateDirectory(AppFolder);
            MigrateLegacyDataIfNeeded();
        }
        catch { /* best-effort */ }
    }

    private static void MigrateLegacyDataIfNeeded()
    {
        var stamp = Path.Combine(AppFolder, MigrationStampFile);
        if (File.Exists(stamp)) return;
        try
        {
            // Pre-v2.0 licensing.log may contain plaintext PII (email, token).
            // The Licensing module's own migration deletes it, but if the user
            // launches AutoUpdate before opening Revit, we must purge it first.
            var oldLog = Path.Combine(AppFolder, "licensing.log");
            if (File.Exists(oldLog)) File.Delete(oldLog);

            // Pre-v2.0 consent stamps had no version prefix → won't match
            // HasUserConsented() format. Delete so user is re-prompted.
            var oldConsent = Path.Combine(AppFolder, ConsentStampFile);
            if (File.Exists(oldConsent))
            {
                var stored = File.ReadAllText(oldConsent).Trim();
                if (!stored.Contains("|")) File.Delete(oldConsent);
            }

            File.WriteAllText(stamp, "v2.0|" + DateTime.UtcNow.ToString("o"), Encoding.UTF8);
        }
        catch { /* ignore — migration is best-effort */ }
    }

    public struct LicenseStatus
    {
        public bool IsValid;
        public string? Email;
        public string? FullName;
        public string? Tier;
        public DateTime Exp;
    }

    public struct LoginResult
    {
        public bool Ok;
        public string? Error;
        public string? Email;
        public string? FullName;
        public string? Tier;
        public string? PremiumExpYMD;
    }

    public struct ApplyKeyResult
    {
        public bool Ok;
        public string? Error;
        public string? Tier;
        public string? Exp;
        public string? Message;
    }

    public LicenseStatus GetLocalStatus()
    {
        if (TryReadSession(out var ses))
        {
            return new LicenseStatus
            {
                IsValid = !string.IsNullOrWhiteSpace(ses.Token),
                Email = ses.Email,
                FullName = ses.FullName,
                Tier = string.IsNullOrWhiteSpace(ses.Tier) ? "FREE" : ses.Tier.ToUpperInvariant(),
                Exp = ParseDate(ses.PremiumExpYMD) ?? DateTime.MinValue
            };
        }
        return new LicenseStatus { IsValid = false };
    }

    public async Task<LoginResult> LoginAsync(string email, string password)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
                return new LoginResult { Ok = false, Error = "BAD_INPUT" };

            var payload = new
            {
                action = "LOGIN_PWD",
                email = email.Trim(),
                password,
                userAgent = Environment.MachineName
            };
            var json = JsonSerializer.Serialize(payload);
            var resp = await Http.PostAsync(LicenseServer, new StringContent(json, Encoding.UTF8, "application/json"));
            var text = await resp.Content.ReadAsStringAsync();

            if (!resp.IsSuccessStatusCode)
                return new LoginResult { Ok = false, Error = $"HTTP_{(int)resp.StatusCode}" };

            var obj = JsonSerializer.Deserialize<Dictionary<string, object>>(text) ?? [];

            if (!(obj.TryGetValue("ok", out var ok) && IsTrue(ok)))
                return new LoginResult
                {
                    Ok = false,
                    Error = obj.TryGetValue("error", out var err) ? err?.ToString() : "server_not_ok"
                };

            var token = obj.TryGetValue("token", out var t) ? t?.ToString() ?? "" : "";
            var tier = obj.TryGetValue("tier", out var tr) ? tr?.ToString() ?? "" : "";
            var exp = obj.TryGetValue("premiumExp", out var ex) ? ex?.ToString() ?? "" : "";
            var mail = obj.TryGetValue("email", out var em) ? em?.ToString() ?? email : email;
            var name = obj.TryGetValue("fullName", out var fn) ? fn?.ToString() ?? "" : "";

            if (string.IsNullOrWhiteSpace(token))
                return new LoginResult { Ok = false, Error = "NO_TOKEN" };

            var ses = new SessionCache
            {
                Token = token,
                Email = mail,
                FullName = name,
                Tier = string.IsNullOrWhiteSpace(tier) ? "FREE" : tier,
                PremiumExpYMD = exp,
                IssuedAt = DateTime.UtcNow.ToString("o")
            };
            WriteSession(ses);
            UpdateVerifyStamp();

            return new LoginResult
            {
                Ok = true,
                Email = mail,
                FullName = name,
                Tier = ses.Tier,
                PremiumExpYMD = exp
            };
        }
        catch (Exception ex)
        {
            return new LoginResult { Ok = false, Error = ex.Message };
        }
    }

    public async Task<ApplyKeyResult> ApplyKeyAsync(string key)
    {
        try
        {
            if (!TryReadSession(out var ses) || string.IsNullOrWhiteSpace(ses.Token))
                return new ApplyKeyResult { Ok = false, Error = "NOT_LOGGED_IN" };

            var payload = new
            {
                action = "APPLY_KEY",
                token = ses.Token,
                key = key.Trim(),
                machineId = MachineIdHelper.Get()
            };
            var json = JsonSerializer.Serialize(payload);
            var resp = await Http.PostAsync(LicenseServer, new StringContent(json, Encoding.UTF8, "application/json"));
            var text = await resp.Content.ReadAsStringAsync();

            if (!resp.IsSuccessStatusCode)
                return new ApplyKeyResult { Ok = false, Error = $"HTTP_{(int)resp.StatusCode}" };

            var obj = JsonSerializer.Deserialize<Dictionary<string, object>>(text) ?? [];

            if (!(obj.TryGetValue("ok", out var ok) && IsTrue(ok)))
                return new ApplyKeyResult
                {
                    Ok = false,
                    Error = obj.TryGetValue("error", out var err) ? err?.ToString() : "server_not_ok"
                };

            var tier = obj.TryGetValue("tier", out var tr) ? tr?.ToString() : null;
            var exp = obj.TryGetValue("exp", out var ex) ? ex?.ToString() : null;
            var msg = obj.TryGetValue("message", out var m) ? m?.ToString() : null;

            // Update session with new tier/exp
            if (!string.IsNullOrWhiteSpace(tier)) ses.Tier = tier.ToUpperInvariant();
            if (exp != null) ses.PremiumExpYMD = exp;
            WriteSession(ses);

            return new ApplyKeyResult { Ok = true, Tier = ses.Tier, Exp = exp, Message = msg };
        }
        catch (Exception ex)
        {
            return new ApplyKeyResult { Ok = false, Error = ex.Message };
        }
    }

    public async Task<bool> RefreshProfileAsync()
    {
        try
        {
            if (!TryReadSession(out var ses) || string.IsNullOrWhiteSpace(ses.Token))
                return false;

            var payload = new { action = "GET_PROFILE", token = ses.Token };
            var json = JsonSerializer.Serialize(payload);
            var resp = await Http.PostAsync(LicenseServer, new StringContent(json, Encoding.UTF8, "application/json"));
            var text = await resp.Content.ReadAsStringAsync();

            if (!resp.IsSuccessStatusCode) return false;

            var obj = JsonSerializer.Deserialize<Dictionary<string, object>>(text) ?? [];
            if (!(obj.TryGetValue("ok", out var ok) && IsTrue(ok))) return false;

            ses.Email = obj.TryGetValue("email", out var em) ? em?.ToString() ?? ses.Email : ses.Email;
            ses.FullName = obj.TryGetValue("fullName", out var fn) ? fn?.ToString() ?? ses.FullName : ses.FullName;
            ses.Tier = obj.TryGetValue("tier", out var tr) ? (tr?.ToString() ?? "FREE").ToUpperInvariant() : ses.Tier;
            ses.PremiumExpYMD = obj.TryGetValue("premiumExp", out var ex) ? ex?.ToString() ?? ses.PremiumExpYMD : ses.PremiumExpYMD;
            WriteSession(ses);
            return true;
        }
        catch { return false; }
    }

    public void Logout()
    {
        try
        {
            var p = Path.Combine(AppFolder, SessionFile);
            if (File.Exists(p)) File.Delete(p);
            var s = Path.Combine(AppFolder, VerifyStampFile);
            if (File.Exists(s)) File.Delete(s);
        }
        catch { }
    }

    /// <summary>
    /// GDPR Art. 17 (Right to Erasure) — completely remove all locally
    /// stored user data: session, cache, log, and consent record. Returns
    /// true if all known files were removed without error.
    ///
    /// Server-side data (account row, machine binding) is NOT removed by
    /// this call. The caller must instruct the user how to request server
    /// deletion (email the data controller).
    /// </summary>
    public bool ClearAllUserData()
    {
        bool ok = true;
        string[] files = { SessionFile, VerifyStampFile, ConsentStampFile, MigrationStampFile, "licensing.log", "thtools_suite.lic" };
        foreach (var name in files)
        {
            try
            {
                var p = Path.Combine(AppFolder, name);
                if (File.Exists(p)) File.Delete(p);
            }
            catch
            {
                ok = false;
            }
        }
        try
        {
            if (Directory.Exists(AppFolder) &&
                Directory.GetFileSystemEntries(AppFolder).Length == 0)
            {
                Directory.Delete(AppFolder);
            }
        }
        catch { /* ignore */ }
        return ok;
    }

    // ============= CONSENT (GDPR Art. 13/14) =============
    //
    // Stamp format: "<policy-version>|<utc-iso8601>". This MUST stay byte-for-byte
    // compatible with THBIM.Licensing.LicenseManager so that consent recorded by
    // either app is honoured by the other. Bumping PolicyVersion forces every
    // user to re-accept the privacy notice at next sign-in.
    public const string PolicyVersion = "1.0";

    public bool HasUserConsented()
    {
        try
        {
            var p = Path.Combine(AppFolder, ConsentStampFile);
            if (!File.Exists(p)) return false;
            var stored = (File.ReadAllText(p) ?? string.Empty).Trim();
            return stored.StartsWith(PolicyVersion + "|", StringComparison.Ordinal);
        }
        catch { return false; }
    }

    public void RecordUserConsent()
    {
        try
        {
            Directory.CreateDirectory(AppFolder);
            var p = Path.Combine(AppFolder, ConsentStampFile);
            var stamp = PolicyVersion + "|" + DateTime.UtcNow.ToString("o");
            File.WriteAllText(p, stamp, Encoding.UTF8);
        }
        catch { /* ignore */ }
    }

    public string GetMachineId() => MachineIdHelper.Get();

    // ============= Session I/O (same format as THBIM.Licensing) =============

    private class SessionCache
    {
        public string? Token { get; set; }
        public string? Email { get; set; }
        public string? FullName { get; set; }
        public string? Tier { get; set; }
        public string? PremiumExpYMD { get; set; }
        public string? IssuedAt { get; set; }
    }

    private static bool TryReadSession(out SessionCache ses)
    {
        ses = new SessionCache();
        try
        {
            var p = Path.Combine(AppFolder, SessionFile);
            if (!File.Exists(p)) return false;
            var enc = File.ReadAllBytes(p);
            var raw = ProtectedData.Unprotect(enc, null, DataProtectionScope.LocalMachine);
            var txt = Encoding.UTF8.GetString(raw);
            ses = JsonSerializer.Deserialize<SessionCache>(txt) ?? new SessionCache();
            return !string.IsNullOrWhiteSpace(ses.Email);
        }
        catch { return false; }
    }

    private static void WriteSession(SessionCache ses)
    {
        try
        {
            Directory.CreateDirectory(AppFolder);
            var json = JsonSerializer.Serialize(ses);
            var enc = ProtectedData.Protect(Encoding.UTF8.GetBytes(json), null, DataProtectionScope.LocalMachine);
            File.WriteAllBytes(Path.Combine(AppFolder, SessionFile), enc);
        }
        catch { }
    }

    private static void UpdateVerifyStamp()
    {
        try
        {
            Directory.CreateDirectory(AppFolder);
            File.WriteAllText(Path.Combine(AppFolder, VerifyStampFile), DateTime.UtcNow.ToString("yyyy-MM-dd"));
        }
        catch { }
    }

    private static DateTime? ParseDate(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        s = s.Trim();
        if (DateTime.TryParseExact(s, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
            return d.Date;
        return null;
    }

    private static bool IsTrue(object? v)
    {
        if (v == null) return false;
        var s = v.ToString()?.Trim().ToLowerInvariant();
        return s is "true" or "1" or "ok" or "yes";
    }
}
