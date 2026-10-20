using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace GtaJournal.Services.Security;

/// <summary>Параметры TOTP из otpauth:// URI.</summary>
public sealed record TotpParameters(string Secret, int Digits = 6, int Period = 30, string Algorithm = "SHA1", string? Label = null)
{
    public static TotpParameters? TryParseUri(string? uri)
    {
        if (string.IsNullOrWhiteSpace(uri) || !Uri.TryCreate(uri, UriKind.Absolute, out var parsed) || parsed.Scheme != "otpauth")
            return null;

        var query = parsed.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Split('=', 2))
            .ToDictionary(p => Uri.UnescapeDataString(p[0]).ToLowerInvariant(), p => p.Length > 1 ? Uri.UnescapeDataString(p[1]) : "");

        if (!query.TryGetValue("secret", out var secret) || string.IsNullOrWhiteSpace(secret))
            return null;

        return new TotpParameters(
            Totp.NormalizeSecret(secret),
            query.TryGetValue("digits", out var d) && int.TryParse(d, out var digits) ? digits : 6,
            query.TryGetValue("period", out var p) && int.TryParse(p, out var period) ? period : 30,
            query.TryGetValue("algorithm", out var a) ? a.ToUpperInvariant() : "SHA1",
            Uri.UnescapeDataString(parsed.AbsolutePath.TrimStart('/')));
    }
}

/// <summary>RFC 6238 TOTP.</summary>
public static class Totp
{
    public static string NormalizeSecret(string secret) =>
        new string(secret.Where(c => !char.IsWhiteSpace(c) && c != '-').ToArray()).TrimEnd('=').ToUpperInvariant();

    public static bool IsValidSecret(string secret)
    {
        try { return Base32Decode(NormalizeSecret(secret)).Length >= 10; }
        catch (FormatException) { return false; }
    }

    public static string Generate(string secret, DateTimeOffset? at = null, int digits = 6, int period = 30, string algorithm = "SHA1")
    {
        var key = Base32Decode(NormalizeSecret(secret));
        var counter = (at ?? DateTimeOffset.UtcNow).ToUnixTimeSeconds() / period;
        Span<byte> message = stackalloc byte[8];
        BinaryPrimitives.WriteInt64BigEndian(message, counter);

        byte[] hash = algorithm switch
        {
            "SHA256" => HMACSHA256.HashData(key, message),
            "SHA512" => HMACSHA512.HashData(key, message),
            _ => HMACSHA1.HashData(key, message),
        };

        var offset = hash[^1] & 0x0F;
        var binary = (BinaryPrimitives.ReadInt32BigEndian(hash.AsSpan(offset)) & 0x7FFFFFFF) % (int)Math.Pow(10, digits);
        return binary.ToString().PadLeft(digits, '0');
    }

    /// <summary>Секунд до смены кода.</summary>
    public static int SecondsRemaining(int period = 30) => period - (int)(DateTimeOffset.UtcNow.ToUnixTimeSeconds() % period);

    private static byte[] Base32Decode(string input)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var output = new List<byte>(input.Length * 5 / 8);
        int buffer = 0, bits = 0;
        foreach (var c in input)
        {
            var value = alphabet.IndexOf(c);
            if (value < 0) throw new FormatException($"Недопустимый символ Base32: '{c}'");
            buffer = (buffer << 5) | value;
            bits += 5;
            if (bits >= 8)
            {
                output.Add((byte)(buffer >> (bits - 8)));
                bits -= 8;
            }
        }
        return [.. output];
    }
}

/// <summary>Шифрование секретов через DPAPI (привязка к пользователю Windows).</summary>
public static class SecretProtector
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("GtaJournal.TotpSecret.v1");

    public static byte[] Protect(string secret) =>
        ProtectedData.Protect(Encoding.UTF8.GetBytes(secret), Entropy, DataProtectionScope.CurrentUser);

    public static string? TryUnprotect(byte[]? data)
    {
        if (data is not { Length: > 0 }) return null;
        try { return Encoding.UTF8.GetString(ProtectedData.Unprotect(data, Entropy, DataProtectionScope.CurrentUser)); }
        catch (CryptographicException) { return null; }
    }
}
