using Microsoft.EntityFrameworkCore;
using LibraryManager.Data;

namespace LibraryManager.Services;

/// <summary>Adds optional catalog metadata columns to databases created by earlier app versions.</summary>
public static class DatabaseUpgradeService
{
    public static void EnsureBookMetadataColumns(LibraryDbContext db)
    {
        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using (var command = db.Database.GetDbConnection().CreateCommand())
        {
            if (command.Connection!.State != System.Data.ConnectionState.Open) command.Connection.Open();
            command.CommandText = "PRAGMA table_info('Books');";
            using var reader = command.ExecuteReader();
            while (reader.Read()) columns.Add(reader.GetString(1));
        }

        AddColumn(db, columns, "Description", "TEXT NOT NULL DEFAULT ''");
        AddColumn(db, columns, "PageCount", "INTEGER NOT NULL DEFAULT 0");
        AddColumn(db, columns, "Language", "TEXT NOT NULL DEFAULT ''");
        AddColumn(db, columns, "Edition", "TEXT NOT NULL DEFAULT ''");
        AddColumn(db, columns, "CoverUrl", "TEXT NOT NULL DEFAULT ''");
    }

    private static void AddColumn(LibraryDbContext db, HashSet<string> existing, string name, string definition)
    {
        if (existing.Contains(name)) return;
        db.Database.ExecuteSqlRaw($"ALTER TABLE Books ADD COLUMN {name} {definition};");
    }
}
