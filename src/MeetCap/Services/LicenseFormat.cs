using System.Security.Cryptography;
using System.Text;

namespace MeetCap.Services;

/// <summary>One license key's contents (after signature verification).</summary>
public readonly record struct LicensePayload(byte Version, byte Edition, DateOnly? Expiry, string KeyId)
{
    public bool IsExpired => Expiry is { } e && DateOnly.FromDateTime(DateTime.Today) > e;
}

/// <summary>
/// Shared license-key format used by BOTH the app (verify, with the public key) and the keygen
/// tool (mint, with the private key), so the two can never drift. A key is:
///
///   token = payload(14 bytes) + ECDSA-P256 signature(64 bytes)
///   text  = "MEETCAP-" + Base32(token) grouped in fives
///
/// The signature makes keys unforgeable without the private key; verification is fully offline.
/// </summary>
public static class LicenseFormat
{
    public const string KeyPrefix = "MEETCAP-";
    private const byte CurrentVersion = 1;
    private static readonly DateOnly Epoch = new(2020, 1, 1);
    private const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567"; // RFC 4648, no padding

    // ---- Minting (keygen / private key) ----

    public static string Mint(ECDsa privateKey, byte edition, DateOnly? expiry, string? keyId = null)
    {
        var payload = BuildPayload(edition, expiry, keyId);
        var sig = privateKey.SignData(payload, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        var token = new byte[payload.Length + sig.Length];
        Buffer.BlockCopy(payload, 0, token, 0, payload.Length);
        Buffer.BlockCopy(sig, 0, token, payload.Length, sig.Length);
        return Format(KeyPrefix + Base32Encode(token));
    }

    private static byte[] BuildPayload(byte edition, DateOnly? expiry, string? keyId)
    {
        var p = new byte[14];
        // Random key id FIRST so every key looks distinct from the very first characters.
        var id = new byte[8];
        if (keyId is not null)
        {
            var raw = Convert.FromHexString(keyId.PadRight(16, '0')[..16]);
            Array.Copy(raw, id, 8);
        }
        else
        {
            RandomNumberGenerator.Fill(id);
        }
        id.CopyTo(p, 0);             // bytes 0..7  = key id
        p[8] = CurrentVersion;       // byte  8     = version
        p[9] = edition;              // byte  9     = edition
        uint days = expiry is { } e ? (uint)Math.Max(0, e.DayNumber - Epoch.DayNumber) : 0u;
        BitConverter.GetBytes(days).CopyTo(p, 10); // bytes 10..13 = expiry (days, LE)
        return p;
    }

    // ---- Verifying (app / public key) ----

    public static bool TryVerify(ECDsa publicKey, string key, out LicensePayload payload)
    {
        payload = default;
        try
        {
            var token = Base32Decode(Normalize(key));
            if (token is null || token.Length != 78)
                return false;

            var data = token[..14];
            var sig = token[14..];
            if (!publicKey.VerifyData(data, sig, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation))
                return false;

            uint days = BitConverter.ToUInt32(data, 10);
            DateOnly? expiry = days == 0 ? null : Epoch.AddDays((int)days);
            payload = new LicensePayload(data[8], data[9], expiry, Convert.ToHexString(data, 0, 8));
            return data[8] == CurrentVersion;
        }
        catch
        {
            return false;
        }
    }

    // ---- Helpers ----

    private static string Normalize(string key)
    {
        var s = key.Trim().ToUpperInvariant();
        if (s.StartsWith(KeyPrefix, StringComparison.Ordinal))
            s = s[KeyPrefix.Length..];
        var sb = new StringBuilder(s.Length);
        foreach (var c in s)
            if (Alphabet.IndexOf(c) >= 0)
                sb.Append(c);
        return sb.ToString();
    }

    private static string Format(string s)
    {
        // Keep the prefix, then dash-group the base32 body in fives.
        var prefix = s.StartsWith(KeyPrefix, StringComparison.Ordinal) ? KeyPrefix : "";
        var body = s[prefix.Length..];
        var sb = new StringBuilder(prefix);
        for (int i = 0; i < body.Length; i++)
        {
            if (i > 0 && i % 5 == 0) sb.Append('-');
            sb.Append(body[i]);
        }
        return sb.ToString();
    }

    private static string Base32Encode(byte[] data)
    {
        var sb = new StringBuilder((data.Length + 4) / 5 * 8);
        int buffer = 0, bits = 0;
        foreach (var b in data)
        {
            buffer = (buffer << 8) | b;
            bits += 8;
            while (bits >= 5)
            {
                bits -= 5;
                sb.Append(Alphabet[(buffer >> bits) & 31]);
            }
        }
        if (bits > 0)
            sb.Append(Alphabet[(buffer << (5 - bits)) & 31]);
        return sb.ToString();
    }

    private static byte[]? Base32Decode(string s)
    {
        int buffer = 0, bits = 0;
        var output = new List<byte>(s.Length * 5 / 8);
        foreach (var c in s)
        {
            int v = Alphabet.IndexOf(c);
            if (v < 0) return null;
            buffer = (buffer << 5) | v;
            bits += 5;
            if (bits >= 8)
            {
                bits -= 8;
                output.Add((byte)((buffer >> bits) & 0xFF));
            }
        }
        return output.ToArray();
    }
}
