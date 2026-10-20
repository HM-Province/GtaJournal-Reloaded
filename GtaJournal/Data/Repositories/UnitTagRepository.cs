using GtaJournal.Models;

namespace GtaJournal.Data.Repositories;

public sealed class UnitTagRepository(Database database)
{
    public List<UnitTag> GetAll()
    {
        using var connection = database.Open();
        using var reader = connection.Command("SELECT id, tag, title, color FROM unit_tags ORDER BY sort_order, id").ExecuteReader();
        var result = new List<UnitTag>();
        while (reader.Read())
            result.Add(new UnitTag(reader.GetInt64(0), reader.GetString(1), reader.GetString(2), reader.GetString(3)));
        return result;
    }

    /// <summary>Полностью заменяет список тегов.</summary>
    public async Task ReplaceAllAsync(IReadOnlyList<UnitTag> tags)
    {
        await using var connection = await database.OpenAsync();
        await using var transaction = connection.BeginTransaction();

        var delete = connection.Command("DELETE FROM unit_tags");
        delete.Transaction = transaction;
        await delete.ExecuteNonQueryAsync();

        for (var i = 0; i < tags.Count; i++)
        {
            var insert = connection.Command(
                "INSERT INTO unit_tags (tag, title, color, sort_order) VALUES ($tag, $title, $color, $order)",
                ("$tag", tags[i].Tag), ("$title", tags[i].Title), ("$color", tags[i].ColorHex), ("$order", i));
            insert.Transaction = transaction;
            await insert.ExecuteNonQueryAsync();
        }

        await transaction.CommitAsync();
    }
}
