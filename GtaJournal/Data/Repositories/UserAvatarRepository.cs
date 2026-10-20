namespace GtaJournal.Data.Repositories;

public sealed class UserAvatarRepository(Database database)
{
    public Dictionary<string, string> GetAll()
    {
        using var connection = database.Open();
        using var reader = connection.Command("SELECT username, avatar_url FROM user_avatars").ExecuteReader();
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        while (reader.Read())
            result[reader.GetString(0)] = reader.GetString(1);
        return result;
    }

    public async Task UpsertManyAsync(IReadOnlyCollection<KeyValuePair<string, string>> avatars)
    {
        if (avatars.Count == 0) return;
        await using var connection = await database.OpenAsync();
        await using var transaction = connection.BeginTransaction();
        var now = DateTimeOffset.UtcNow.ToString("O");
        foreach (var (username, url) in avatars)
        {
            var command = connection.Command("""
                INSERT INTO user_avatars (username, avatar_url, updated_at) VALUES ($username, $url, $at)
                ON CONFLICT(username) DO UPDATE SET avatar_url = excluded.avatar_url, updated_at = excluded.updated_at
                """,
                ("$username", username), ("$url", url), ("$at", now));
            command.Transaction = transaction;
            await command.ExecuteNonQueryAsync();
        }
        await transaction.CommitAsync();
    }
}
