using GtaJournal.Models;
using Microsoft.Data.Sqlite;

namespace GtaJournal.Data.Repositories;

public sealed class AccountRepository(Database database)
{
    private const string Columns = "id, login, display_name, avatar_url, user_agent, client_hints, totp_secret, cookies, created_at, last_login_at";

    public async Task<List<Account>> GetAllAsync()
    {
        await using var connection = await database.OpenAsync();
        await using var reader = await connection.Command($"SELECT {Columns} FROM accounts ORDER BY id").ExecuteReaderAsync();
        var result = new List<Account>();
        while (await reader.ReadAsync())
            result.Add(Map(reader));
        return result;
    }

    public async Task<Account?> GetAsync(long id)
    {
        await using var connection = await database.OpenAsync();
        await using var reader = await connection.Command($"SELECT {Columns} FROM accounts WHERE id = $id", ("$id", id)).ExecuteReaderAsync();
        return await reader.ReadAsync() ? Map(reader) : null;
    }

    public async Task<Account?> FindByLoginAsync(string login)
    {
        await using var connection = await database.OpenAsync();
        await using var reader = await connection.Command($"SELECT {Columns} FROM accounts WHERE login = $login", ("$login", login)).ExecuteReaderAsync();
        return await reader.ReadAsync() ? Map(reader) : null;
    }

    public async Task InsertAsync(Account account)
    {
        await using var connection = await database.OpenAsync();
        account.Id = (long)(await connection.Command("""
            INSERT INTO accounts (login, display_name, avatar_url, user_agent, client_hints, totp_secret, cookies, created_at, last_login_at)
            VALUES ($login, $display_name, $avatar_url, $user_agent, $client_hints, $totp_secret, $cookies, $created_at, $last_login_at)
            RETURNING id;
            """, Parameters(account)).ExecuteScalarAsync())!;
    }

    public async Task UpdateAsync(Account account)
    {
        await using var connection = await database.OpenAsync();
        await connection.Command("""
            UPDATE accounts SET
                login = $login, display_name = $display_name, avatar_url = $avatar_url,
                user_agent = $user_agent, client_hints = $client_hints, totp_secret = $totp_secret,
                cookies = $cookies, last_login_at = $last_login_at
            WHERE id = $id;
            """, [.. Parameters(account), ("$id", account.Id)]).ExecuteNonQueryAsync();
    }

    public async Task UpdateCookiesAsync(long id, string? cookiesJson)
    {
        await using var connection = await database.OpenAsync();
        await connection.Command("UPDATE accounts SET cookies = $cookies WHERE id = $id", ("$cookies", cookiesJson), ("$id", id)).ExecuteNonQueryAsync();
    }

    public async Task DeleteAsync(long id)
    {
        await using var connection = await database.OpenAsync();
        await connection.Command("DELETE FROM accounts WHERE id = $id", ("$id", id)).ExecuteNonQueryAsync();
    }

    private static (string, object?)[] Parameters(Account a) =>
    [
        ("$login", a.Login),
        ("$display_name", a.DisplayName),
        ("$avatar_url", a.AvatarUrl),
        ("$user_agent", a.UserAgent),
        ("$client_hints", a.ClientHints),
        ("$totp_secret", a.ProtectedTotpSecret),
        ("$cookies", a.CookiesJson),
        ("$created_at", a.CreatedAt.ToString("O")),
        ("$last_login_at", a.LastLoginAt?.ToString("O")),
    ];

    private static Account Map(SqliteDataReader r) => new()
    {
        Id = r.GetInt64(r.GetOrdinal("id")),
        Login = r.GetString(r.GetOrdinal("login")),
        DisplayName = r.GetNullableString("display_name"),
        AvatarUrl = r.GetNullableString("avatar_url"),
        UserAgent = r.GetString(r.GetOrdinal("user_agent")),
        ClientHints = r.GetNullableString("client_hints"),
        ProtectedTotpSecret = r.GetNullableBlob("totp_secret"),
        CookiesJson = r.GetNullableString("cookies"),
        CreatedAt = DateTimeOffset.Parse(r.GetString(r.GetOrdinal("created_at"))),
        LastLoginAt = r.GetNullableString("last_login_at") is { } s ? DateTimeOffset.Parse(s) : null,
    };
}
