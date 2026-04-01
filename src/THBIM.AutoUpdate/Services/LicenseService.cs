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
    private const string LicenseServer = "https://script.google.com/macros/s/AKfycbxbMI884nGWzL6u7p10ChLqzOHSl4KXpJvr9EcsYP0lMiE3_ZNchDmNsDtLhOLEtKiYCw/exec";

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(20) };

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
