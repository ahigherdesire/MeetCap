// MeetCap license keygen — seller-side tool. Keep the private key secret.
//
//   dotnet run -- genkeys                 Create the signing keypair (one time).
//   dotnet run -- mint [--days N]         Mint a license key (perpetual unless --days given).
//   dotnet run -- verify <KEY>            Verify a key against the public key (self-test).
using System.Security.Cryptography;
using MeetCap.Services;

string dir = AppContext.BaseDirectory;
// Resolve the keygen folder (build\keygen) relative to this tool, falling back to CWD.
string keyDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "build", "keygen"));
if (!Directory.Exists(keyDir)) keyDir = Directory.GetCurrentDirectory();
string privPath = Path.Combine(keyDir, "meetcap-private-key.b64");
string pubPath = Path.Combine(keyDir, "meetcap-public-key.b64");

string cmd = args.Length > 0 ? args[0].ToLowerInvariant() : "help";

switch (cmd)
{
    case "genkeys":
        GenKeys();
        break;
    case "mint":
        Mint(args);
        break;
    case "verify":
        Verify(args);
        break;
    default:
        Console.WriteLine("Commands: genkeys | mint [--days N] [--edition E] | verify <KEY>");
        break;
}

void GenKeys()
{
    if (File.Exists(privPath))
    {
        Console.WriteLine($"Private key already exists at {privPath} — refusing to overwrite.");
        return;
    }
    using var ec = ECDsa.Create(ECCurve.CreateFromFriendlyName("nistP256"));
    File.WriteAllText(privPath, Convert.ToBase64String(ec.ExportPkcs8PrivateKey()));
    var pub = Convert.ToBase64String(ec.ExportSubjectPublicKeyInfo());
    File.WriteAllText(pubPath, pub);
    Console.WriteLine($"Private key -> {privPath}  (KEEP SECRET, never commit/ship)");
    Console.WriteLine($"Public  key -> {pubPath}");
    Console.WriteLine();
    Console.WriteLine("Embed this PUBLIC key in LicenseService.PublicKeyBase64:");
    Console.WriteLine(pub);
}

void Mint(string[] a)
{
    if (!File.Exists(privPath)) { Console.WriteLine("No private key. Run 'genkeys' first."); return; }
    using var ec = ECDsa.Create(ECCurve.CreateFromFriendlyName("nistP256"));
    ec.ImportPkcs8PrivateKey(Convert.FromBase64String(File.ReadAllText(privPath).Trim()), out _);

    byte edition = 1;
    DateOnly? expiry = null;
    for (int i = 1; i < a.Length - 1; i++)
    {
        if (a[i] == "--days" && int.TryParse(a[i + 1], out var d))
            expiry = DateOnly.FromDateTime(DateTime.Today).AddDays(d);
        if (a[i] == "--edition" && byte.TryParse(a[i + 1], out var e))
            edition = e;
    }

    var key = LicenseFormat.Mint(ec, edition, expiry);
    Console.WriteLine(key);
    Console.WriteLine(expiry is null ? "(perpetual)" : $"(expires {expiry})");
}

void Verify(string[] a)
{
    if (a.Length < 2) { Console.WriteLine("Usage: verify <KEY>"); return; }
    if (!File.Exists(pubPath)) { Console.WriteLine("No public key. Run 'genkeys' first."); return; }
    using var ec = ECDsa.Create(ECCurve.CreateFromFriendlyName("nistP256"));
    ec.ImportSubjectPublicKeyInfo(Convert.FromBase64String(File.ReadAllText(pubPath).Trim()), out _);

    if (LicenseFormat.TryVerify(ec, a[1], out var p))
        Console.WriteLine($"VALID — edition={p.Edition}, expiry={(p.Expiry?.ToString() ?? "perpetual")}, id={p.KeyId}, expired={p.IsExpired}");
    else
        Console.WriteLine("INVALID");
}
