using System.Diagnostics;
using System.Windows.Input;
using THBIM.AutoUpdate.Helpers;
using THBIM.AutoUpdate.Services;

namespace THBIM.AutoUpdate.ViewModels;

public class AccountViewModel : ViewModelBase
{
    private readonly LicenseService _licenseService;

    public AccountViewModel(LicenseService licenseService)
    {
        _licenseService = licenseService;

        LoginCommand = new RelayCommand(async () => await LoginAsync(), () => !IsBusy && !string.IsNullOrWhiteSpace(Email));
        LogoutCommand = new RelayCommand(DoLogout);
        ApplyKeyCommand = new RelayCommand(async () => await ApplyKeyAsync(), () => !IsBusy && !string.IsNullOrWhiteSpace(LicenseKey));
        RefreshCommand = new RelayCommand(async () => await RefreshAsync(), () => !IsBusy);
        OpenSignUpCommand = new RelayCommand(() =>
        {
            try { Process.Start(new ProcessStartInfo("https://thbim.pages.dev") { UseShellExecute = true }); }
            catch { }
        });

        // Load cached session
        LoadLocalStatus();
    }

    // === Login form fields ===
    private string _email = "";
    public string Email
    {
        get => _email;
        set => SetProperty(ref _email, value);
    }

    private string _password = "";
    public string Password
    {
        get => _password;
        set => SetProperty(ref _password, value);
    }

    // === Profile fields ===
    private string _fullName = "";
    public string FullName
    {
        get => _fullName;
        set => SetProperty(ref _fullName, value);
    }

    private string _tier = "FREE";
    public string Tier
    {
        get => _tier;
        set
        {
            if (SetProperty(ref _tier, value))
            {
                OnPropertyChanged(nameof(TierDisplay));
                OnPropertyChanged(nameof(IsPremium));
                OnPropertyChanged(nameof(IsFree));
            }
        }
    }

    private string _expiryDisplay = "-";
    public string ExpiryDisplay
    {
        get => _expiryDisplay;
        set => SetProperty(ref _expiryDisplay, value);
    }

    private string _machineId = "";
    public string MachineId
    {
        get => _machineId;
        set => SetProperty(ref _machineId, value);
    }

    private string _licenseKey = "";
    public string LicenseKey
    {
        get => _licenseKey;
        set => SetProperty(ref _licenseKey, value);
    }

    // === State ===
    private bool _isLoggedIn;
    public bool IsLoggedIn
    {
        get => _isLoggedIn;
        set
        {
            if (SetProperty(ref _isLoggedIn, value))
            {
                OnPropertyChanged(nameof(ShowLogin));
                OnPropertyChanged(nameof(ShowProfile));
                LoggedInChanged?.Invoke(value);
            }
        }
    }

    public bool ShowLogin => !IsLoggedIn;
    public bool ShowProfile => IsLoggedIn;

    private bool _isBusy;
    public bool IsBusy
    {
        get => _isBusy;
        set => SetProperty(ref _isBusy, value);
    }

    private string _statusMessage = "";
    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    private bool _isError;
    public bool IsError
    {
        get => _isError;
        set => SetProperty(ref _isError, value);
    }

    public string TierDisplay => Tier?.ToUpperInvariant() ?? "FREE";
    public bool IsPremium => string.Equals(Tier, "PREMIUM", StringComparison.OrdinalIgnoreCase);
    public bool IsFree => !IsPremium;

    public string Initials
    {
        get
        {
            if (string.IsNullOrWhiteSpace(FullName)) return "TH";
            var parts = FullName.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2)
                return $"{parts[0][0]}{parts[^1][0]}".ToUpperInvariant();
            return parts[0][..Math.Min(2, parts[0].Length)].ToUpperInvariant();
        }
    }

    // === Events ===
    public event Action<bool>? LoggedInChanged;

    // === Commands ===
    public ICommand LoginCommand { get; }
    public ICommand LogoutCommand { get; }
    public ICommand ApplyKeyCommand { get; }
    public ICommand RefreshCommand { get; }
    public ICommand OpenSignUpCommand { get; }

    // === Methods ===
    private void LoadLocalStatus()
    {
        var status = _licenseService.GetLocalStatus();
        if (status.IsValid)
        {
            Email = status.Email ?? "";
            FullName = status.FullName ?? "";
            Tier = status.Tier ?? "FREE";
            ExpiryDisplay = FormatExpiry(status.Exp);
            MachineId = _licenseService.GetMachineId();
            IsLoggedIn = true;
        }
        else
        {
            MachineId = _licenseService.GetMachineId();
            IsLoggedIn = false;
        }
    }

    private async Task LoginAsync()
    {
        IsBusy = true;
        IsError = false;
        StatusMessage = "Signing in...";

        var result = await _licenseService.LoginAsync(Email, Password);

        if (result.Ok)
        {
            FullName = result.FullName ?? "";
            Email = result.Email ?? Email;
            Tier = result.Tier ?? "FREE";
            ExpiryDisplay = FormatExpiry(ParseDate(result.PremiumExpYMD));
            MachineId = _licenseService.GetMachineId();
            Password = "";
            IsLoggedIn = true;
            StatusMessage = "";
        }
        else
        {
            IsError = true;
            StatusMessage = result.Error switch
            {
                "BAD_INPUT" => "Please enter email and password",
                "EMAIL_NOT_FOUND" => "Email không tồn tại",
                "WRONG_PASSWORD" => "Mật khẩu không đúng",
                "NO_TOKEN" => "Server error: no token",
                _ => result.Error ?? "Login failed"
            };
        }

        IsBusy = false;
    }

    private void DoLogout()
    {
        _licenseService.Logout();
        FullName = "";
        Tier = "FREE";
        ExpiryDisplay = "-";
        LicenseKey = "";
        Password = "";
        StatusMessage = "";
        IsLoggedIn = false;
    }

    private async Task ApplyKeyAsync()
    {
        IsBusy = true;
        IsError = false;
        StatusMessage = "Activating key...";

        var result = await _licenseService.ApplyKeyAsync(LicenseKey);

        if (result.Ok)
        {
            Tier = result.Tier ?? Tier;
            ExpiryDisplay = FormatExpiry(ParseDate(result.Exp));
            LicenseKey = "";
            IsError = false;
            StatusMessage = result.Message ?? "License activated successfully!";
        }
        else
        {
            IsError = true;
            StatusMessage = result.Error ?? "Activation failed";
        }

        IsBusy = false;
    }

    private async Task RefreshAsync()
    {
        IsBusy = true;
        var ok = await _licenseService.RefreshProfileAsync();
        if (ok) LoadLocalStatus();
        IsBusy = false;
    }

    private static string FormatExpiry(DateTime exp)
    {
        if (exp == DateTime.MinValue) return "-";
        if (exp.Year >= 2099) return "LIFETIME";
        return exp.ToString("yyyy-MM-dd");
    }

    private static DateTime ParseDate(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return DateTime.MinValue;
        if (DateTime.TryParseExact(s.Trim(), "yyyy-MM-dd",
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None, out var d))
            return d;
        return DateTime.MinValue;
    }
}
