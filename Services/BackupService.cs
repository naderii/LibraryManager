using System.IO;
using LibraryManager.Data;
using Microsoft.Data.Sqlite;

namespace LibraryManager.Services;

public static class BackupService
{
    public static string BackupDir => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Backups");
    public static string DbPath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "LibraryManager.db");

    public static void AutoBackup()
    {
        Directory.CreateDirectory(BackupDir);

        if (SettingsService.Get("LastBackupDate", "") == DateTime.Today.ToString("yyyy-MM-dd"))
            return;

        try
        {
            var backupPath = Path.Combine(
                BackupDir,
                "Library_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".bak");

            Backup(backupPath);
            SettingsService.Save("LastBackupDate", DateTime.Today.ToString("yyyy-MM-dd"));
        }
        catch
        {
            // A backup failure should not prevent the application from starting.
        }
    }

    /// <summary>
    /// Creates a transactionally consistent SQLite backup, including committed WAL data.
    /// The SQLite backup API avoids copying the database and WAL sidecar files separately.
    /// </summary>
    public static void Backup(string path)
    {
        if (!File.Exists(DbPath))
            throw new FileNotFoundException("فایل پایگاه داده پیدا نشد.", DbPath);

        var fullPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(fullPath);
        if (string.IsNullOrWhiteSpace(directory))
            throw new InvalidOperationException("مسیر فایل پشتیبان معتبر نیست.");

        Directory.CreateDirectory(directory);

        var sourceConnectionString = new SqliteConnectionStringBuilder
        {
            DataSource = DbPath,
            Mode = SqliteOpenMode.ReadOnly
        }.ToString();

        var destinationConnectionString = new SqliteConnectionStringBuilder
        {
            DataSource = fullPath,
            Mode = SqliteOpenMode.ReadWriteCreate
        }.ToString();

        using var source = new SqliteConnection(sourceConnectionString);
        using var destination = new SqliteConnection(destinationConnectionString);

        source.Open();
        destination.Open();
        source.BackupDatabase(destination);
    }

    public static List<FileInfo> Files()
    {
        Directory.CreateDirectory(BackupDir);
        return new DirectoryInfo(BackupDir)
            .GetFiles("*.bak")
            .OrderByDescending(file => file.CreationTime)
            .ToList();
    }
}
