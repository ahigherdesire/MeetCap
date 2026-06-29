using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Win32;

namespace MeetCap.Services;

public enum LicenseStatus { Trial, TrialExpired, Active, Expired }

public enum ActivationResult { Success, Invalid, Expired }

public interface ILicenseService
{
    LicenseStatus Status { get; }
    /// <summary>True while the trial is valid or a license is active.</summary>
    bool IsRecordingAllowed { get; }
    string StatusText { get; }
    int TrialDaysLeft { get; }

    event EventHandler? Changed;

    ActivationResult Activate(string key);
    void Deactivate();
}

/// <summary>
/// Offline, signature-based licensing. Keys are minted by the seller's keygen (private key) and
/// verified here with the embedded public key — no server required, fully offline, unforgeable.
/// A new install gets a free trial; recording is gated on a valid trial or an activated license.
/// The activation file is bound to this machine so it can't be copied to another PC.
/// </summary>
public sealed class LicenseService : ILicenseService
{
    // Public key (SubjectPublicKeyInfo, base64). The matching PRIVATE key lives only in
    // build\keygen and must never ship. Generated via: tools\KeyGen genkeys.
    private const string PublicKeyBase64 =
        "MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAEKpaxemi/muTRpRgdE+aPdWlLVBscHKlMCb2TAT1joKpdmh9/pdKFAMrGMUko020TQVuuBP1wH5BX5OXOTq4DIw==";

    private const int TrialDays = 7;

    private readonly string _licensePath;
    private readonly string _trialPath;
    private readonly ECDsa _publicKey;

    public LicenseStatus Status { get; private set; }
    public int TrialDaysLeft { get; private set; }

    public event EventHandler? Changed;

    public LicenseService()
    {
        var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MeetCap");
        Directory.CreateDirectory(dir);
        _licensePath = Path.Combine(dir, "license.dat");
        _trialPath = Path.Combine(dir, "trial.dat");

        _publicKey = ECDsa.Create(ECCurve.CreateFromFriendlyName("nistP256"));
        _publicKey.ImportSubjectPublicKeyInfo(Convert.FromBase64String(PublicKeyBase64), out _);

        Evaluate();
    }

    public bool IsRecordingAllowed => Status is LicenseStatus.Active or LicenseStatus.Trial;

    public string StatusText => Status switch
    {
        LicenseStatus.Active => "Activated — thank you for buying MeetCap.",
        LicenseStatus.Trial => $"Free trial — {TrialDaysLeft} day{(TrialDaysLeft == 1 ? "" : "s")} left.",
        LicenseStatus.TrialExpired => "Your free trial has ended. Enter a license key to keep recording.",
        LicenseStatus.Expired => "Your license has expired. Renew it to keep recording.",
        _ => "",
    };

    public ActivationResult Activate(string key)
    {
        if (!LicenseFormat.TryVerify(_publicKey, key, out var payload))
            return ActivationResult.Invalid;
        if (payload.IsExpired)
            return ActivationResult.Expired;

        var record = new StoredLicense { Key = key.Trim(), MachineId = MachineId() };
        File.WriteAllText(_licensePath, JsonSerializer.Serialize(record));
        Evaluate();
        return ActivationResult.Success;
    }

    public void Deactivate()
    {
        try { if (File.Exists(_licensePath)) File.Delete(_licensePath); } catch { }
        Evaluate();
    }

    private void Evaluate()
    {
        var previous = Status;

        if (TryLoadActiveLicense(out bool expired))
            Status = LicenseStatus.Active;
        else if (expired)
            Status = LicenseStatus.Expired;
        else
            EvaluateTrial();

        if (Status != previous || Status == LicenseStatus.Trial)
            Changed?.Invoke(this, EventArgs.Empty);
    }

    private bool TryLoadActiveLicense(out bool expired)
    {
        expired = false;
        try
        {
            if (!File.Exists(_licensePath))
                return false;
            var record = JsonSerializer.Deserialize<StoredLicense>(File.ReadAllText(_licensePath));
            if (record is null || string.IsNullOrWhiteSpace(record.Key))
                return false;
            // Bound to this machine: a copied license.dat won't validate elsewhere.
            if (!string.Equals(record.MachineId, MachineId(), StringComparison.Ordinal))
                return false;
            if (!LicenseFormat.TryVerify(_publicKey, record.Key, out var payload))
                return false;
            if (payload.IsExpired) { expired = true; return false; }
            return true;
        }
        catch
        {
            return false;
        }
    }

    private void EvaluateTrial()
    {
        DateTime start;
        try
        {
            if (File.Exists(_trialPath) &&
                DateTime.TryParse(File.ReadAllText(_trialPath).Trim(), null,
                    System.Globalization.DateTimeStyles.RoundtripKind, out var saved))
            {
                start = saved;
            }
            else
            {
                start = DateTime.UtcNow;
                File.WriteAllText(_trialPath, start.ToString("O"));
            }
        }
        catch
        {
            start = DateTime.UtcNow;
        }

        int used = (int)(DateTime.UtcNow - start).TotalDays;
        TrialDaysLeft = Math.Max(0, TrialDays - used);
        Status = TrialDaysLeft > 0 ? LicenseStatus.Trial : LicenseStatus.TrialExpired;
    }

    private static string MachineId()
    {
        string raw;
        try
        {
            using var key = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64)
                .OpenSubKey(@"SOFTWARE\Microsoft\Cryptography");
            raw = key?.GetValue("MachineGuid") as string ?? Environment.MachineName;
        }
        catch
        {
            raw = Environment.MachineName;
        }
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes("MeetCap:" + raw));
        return Convert.ToHexString(hash)[..16];
    }

    private sealed class StoredLicense
    {
        public string Key { get; set; } = "";
        public string MachineId { get; set; } = "";
    }
}
