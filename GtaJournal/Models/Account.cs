namespace GtaJournal.Models;

/// <summary>Сохранённый аккаунт журнала.</summary>
public sealed class Account
{
    public long Id { get; set; }
    public required string Login { get; set; }
    public string? DisplayName { get; set; }
    public string? AvatarUrl { get; set; }

    /// <summary>User-Agent, сгенерированный для аккаунта один раз при создании.</summary>
    public required string UserAgent { get; set; }

    /// <summary>Заголовок sec-ch-ua (null для не-Chromium браузеров).</summary>
    public string? ClientHints { get; set; }

    /// <summary>TOTP-секрет, зашифрованный DPAPI. null — пользователь решил не хранить.</summary>
    public byte[]? ProtectedTotpSecret { get; set; }

    public string? CookiesJson { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? LastLoginAt { get; set; }

    public bool HasTotpSecret => ProtectedTotpSecret is { Length: > 0 };
    public string Title => string.IsNullOrWhiteSpace(DisplayName) ? Login : DisplayName!;
}
