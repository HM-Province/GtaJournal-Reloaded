namespace GtaJournal.Data.Repositories;

public sealed class SettingsRepository(Database database)
{
    public Dictionary<string, string> GetAll()
    {
        using var connection = database.Open();
        using var reader = connection.Command("SELECT key, value FROM settings").ExecuteReader();
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        while (reader.Read())
            result[reader.GetString(0)] = reader.GetString(1);
        return result;
    }

    public async Task SetManyAsync(IEnumerable<KeyValuePair<string, string>> values)
    {
        await using var connection = await database.OpenAsync();
        await using var transaction = connection.BeginTransaction();
        foreach (var (key, value) in values)
        {
            var command = connection.Command(
                "INSERT INTO settings (key, value) VALUES ($key, $value) ON CONFLICT(key) DO UPDATE SET value = excluded.value",
                ("$key", key), ("$value", value));
            command.Transaction = transaction;
            await command.ExecuteNonQueryAsync();
        }
        await transaction.CommitAsync();
    }
}
