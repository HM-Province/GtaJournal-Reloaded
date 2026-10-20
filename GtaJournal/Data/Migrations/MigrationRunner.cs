using System.Reflection;
using Microsoft.Data.Sqlite;

namespace GtaJournal.Data.Migrations;

/// <summary>
/// Применяет SQL-миграции, зашитые в сборку как EmbeddedResource (Data/Migrations/Scripts/*.sql).
/// Порядок — по имени файла (NNNN_Name.sql). Применённые миграции фиксируются в __migrations_history,
/// каждая выполняется в своей транзакции.
/// Чтобы добавить миграцию — положите новый файл с бо́льшим номером в Scripts, ничего регистрировать не нужно.
/// </summary>
public sealed class MigrationRunner(Database database)
{
    private const string ResourcePrefix = "Migrations.";

    public IReadOnlyList<string> ApplyPending()
    {
        using var connection = database.Open();
        connection.Command("""
            CREATE TABLE IF NOT EXISTS __migrations_history (
                id         TEXT NOT NULL PRIMARY KEY,
                applied_at TEXT NOT NULL
            );
            """).ExecuteNonQuery();

        var applied = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using (var reader = connection.Command("SELECT id FROM __migrations_history").ExecuteReader())
            while (reader.Read())
                applied.Add(reader.GetString(0));

        var appliedNow = new List<string>();
        foreach (var migration in LoadMigrations().Where(m => !applied.Contains(m.Id)))
        {
            using var transaction = connection.BeginTransaction();
            try
            {
                var command = connection.Command(migration.Sql);
                command.Transaction = transaction;
                command.ExecuteNonQuery();

                var history = connection.Command(
                    "INSERT INTO __migrations_history (id, applied_at) VALUES ($id, $at)",
                    ("$id", migration.Id), ("$at", DateTimeOffset.UtcNow.ToString("O")));
                history.Transaction = transaction;
                history.ExecuteNonQuery();

                transaction.Commit();
                appliedNow.Add(migration.Id);
            }
            catch (SqliteException ex)
            {
                transaction.Rollback();
                throw new InvalidOperationException($"Не удалось применить миграцию {migration.Id}: {ex.Message}", ex);
            }
        }

        return appliedNow;
    }

    private static IEnumerable<(string Id, string Sql)> LoadMigrations()
    {
        var assembly = Assembly.GetExecutingAssembly();
        foreach (var name in assembly.GetManifestResourceNames()
                     .Where(n => n.StartsWith(ResourcePrefix, StringComparison.Ordinal) && n.EndsWith(".sql", StringComparison.OrdinalIgnoreCase))
                     .Order(StringComparer.Ordinal))
        {
            using var stream = assembly.GetManifestResourceStream(name)!;
            using var reader = new StreamReader(stream);
            yield return (Path.GetFileNameWithoutExtension(name[ResourcePrefix.Length..]), reader.ReadToEnd());
        }
    }
}
