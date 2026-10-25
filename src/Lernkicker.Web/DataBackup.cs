using System.IO.Compression;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace Lernkicker.Web;

/// <summary>
/// Everything in the data folder as one zip: a consistent copy of the database and the Data Protection keys
/// (cookies, the encrypted Telegram token). A restore is staged next to the data and applied at the next start,
/// before anything opens the database; the container's restart policy brings the app back up.
/// </summary>
public sealed class DataBackup(string dataPath, string databasePath)
{
    public const string Format = "lernkicker-backup/v1";
    public const long MaxBytes = Contracts.ApiLimits.BackupMaxBytes;

    private const string DatabaseEntry = "lernkicker.db";
    private const string KeysFolder = "keys";
    private const string ManifestEntry = "backup.json";

    private string KeysPath => Path.Combine(dataPath, KeysFolder);
    private string StagedPath => Path.Combine(dataPath, "restore");

    public async Task WriteAsync(Stream output, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var snapshot = Path.Combine(dataPath, $"backup-{Guid.NewGuid():N}.db");
        var archive = Path.ChangeExtension(snapshot, ".zip");
        try
        {
            // SQLite's online backup: consistent even while the app writes
            await using (var source = new SqliteConnection($"Data Source={databasePath};Mode=ReadOnly;Pooling=False"))
            await using (var target = new SqliteConnection($"Data Source={snapshot};Pooling=False"))
            {
                await source.OpenAsync(cancellationToken);
                await target.OpenAsync(cancellationToken);
                source.BackupDatabase(target);
            }

            // The zip is written to a file first: ZipArchive writes its directory synchronously
            await using (var zip = new ZipArchive(File.Create(archive), ZipArchiveMode.Create))
            {
                await AddAsync(zip, ManifestEntry, JsonSerializer.SerializeToUtf8Bytes(new { format = Format, createdAt = now }), cancellationToken);
                await AddFileAsync(zip, DatabaseEntry, snapshot, cancellationToken);
                foreach (var key in Directory.Exists(KeysPath) ? Directory.EnumerateFiles(KeysPath, "*.xml") : [])
                {
                    await AddFileAsync(zip, $"{KeysFolder}/{Path.GetFileName(key)}", key, cancellationToken);
                }
            }

            await using var file = File.OpenRead(archive);
            await file.CopyToAsync(output, cancellationToken);
        }
        finally
        {
            File.Delete(snapshot);
            File.Delete(archive);
        }
    }

    /// <summary>Checks an uploaded backup and stages it. False when it is not a Lernkicker backup.</summary>
    public async Task<bool> StageAsync(Stream input, CancellationToken cancellationToken)
    {
        var upload = Path.Combine(dataPath, $"upload-{Guid.NewGuid():N}.zip");
        var staging = StagedPath + ".tmp";
        try
        {
            await using (var file = File.Create(upload))
            {
                await CopyLimitedAsync(input, file, cancellationToken);
            }

            if (Directory.Exists(staging))
            {
                Directory.Delete(staging, recursive: true);
            }

            Directory.CreateDirectory(Path.Combine(staging, KeysFolder));
            await using (var zip = await ZipArchive.CreateAsync(File.OpenRead(upload), ZipArchiveMode.Read, leaveOpen: false, entryNameEncoding: null, cancellationToken))
            {
                if (zip.GetEntry(ManifestEntry) is not { } manifest || !await IsManifestAsync(manifest, cancellationToken) || zip.GetEntry(DatabaseEntry) is not { } database)
                {
                    return false;
                }

                await database.ExtractToFileAsync(Path.Combine(staging, DatabaseEntry), cancellationToken);
                // Only flat key files: no paths from the zip ever reach the file system
                foreach (var key in zip.Entries.Where(e => e.FullName.StartsWith($"{KeysFolder}/", StringComparison.Ordinal)))
                {
                    var name = Path.GetFileName(key.FullName);
                    if (name.Length > 0 && name.EndsWith(".xml", StringComparison.Ordinal) && name == key.FullName[(KeysFolder.Length + 1)..])
                    {
                        await key.ExtractToFileAsync(Path.Combine(staging, KeysFolder, name), cancellationToken);
                    }
                }
            }

            if (!IsLernkickerDatabase(Path.Combine(staging, DatabaseEntry)))
            {
                return false;
            }

            if (Directory.Exists(StagedPath))
            {
                Directory.Delete(StagedPath, recursive: true);
            }

            Directory.Move(staging, StagedPath);
            return true;
        }
        catch (InvalidDataException)
        {
            return false;
        }
        finally
        {
            File.Delete(upload);
            if (Directory.Exists(staging))
            {
                Directory.Delete(staging, recursive: true);
            }
        }
    }

    /// <summary>
    /// At start: replaces the database and keys with a staged restore. The previous files are kept in
    /// <c>before-restore-&lt;time&gt;</c>. Returns true when a restore was applied.
    /// </summary>
    public bool ApplyStaged(DateTimeOffset now)
    {
        if (!Directory.Exists(StagedPath))
        {
            return false;
        }

        // Pooled connections would keep using the old file
        SqliteConnection.ClearAllPools();
        var previous = Path.Combine(dataPath, $"before-restore-{now:yyyyMMdd-HHmmss}");
        Directory.CreateDirectory(previous);
        foreach (var suffix in new[] { "", "-wal", "-shm" })
        {
            if (File.Exists(databasePath + suffix))
            {
                File.Move(databasePath + suffix, Path.Combine(previous, Path.GetFileName(databasePath) + suffix));
            }
        }

        if (Directory.Exists(KeysPath))
        {
            Directory.Move(KeysPath, Path.Combine(previous, KeysFolder));
        }

        File.Move(Path.Combine(StagedPath, DatabaseEntry), databasePath);
        Directory.Move(Path.Combine(StagedPath, KeysFolder), KeysPath);
        Directory.Delete(StagedPath, recursive: true);
        return true;
    }

    private static async Task CopyLimitedAsync(Stream input, Stream output, CancellationToken cancellationToken)
    {
        var buffer = new byte[81920];
        long total = 0;
        int read;
        while ((read = await input.ReadAsync(buffer, cancellationToken)) > 0)
        {
            total += read;
            if (total > MaxBytes)
            {
                throw new InvalidDataException("Backup too large");
            }

            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }
    }

    private static async Task<bool> IsManifestAsync(ZipArchiveEntry entry, CancellationToken cancellationToken)
    {
        if (entry.Length > 4096)
        {
            return false;
        }

        try
        {
            await using var stream = await entry.OpenAsync(cancellationToken);
            using var json = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            return json.RootElement.TryGetProperty("format", out var format) && format.GetString() == Format;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    // A SQLite file with Lernkicker's migrations table
    private static bool IsLernkickerDatabase(string path)
    {
        var header = new byte[16];
        using (var file = File.OpenRead(path))
        {
            if (file.Read(header) != 16 || Encoding.ASCII.GetString(header) != "SQLite format 3\0")
            {
                return false;
            }
        }

        try
        {
            using var connection = new SqliteConnection($"Data Source={path};Mode=ReadOnly;Pooling=False");
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT count(*) FROM \"__EFMigrationsHistory\"";
            return Convert.ToInt64(command.ExecuteScalar()) > 0;
        }
        catch (SqliteException)
        {
            return false;
        }
    }

    private static async Task AddAsync(ZipArchive zip, string name, byte[] content, CancellationToken cancellationToken)
    {
        await using var stream = await zip.CreateEntry(name, CompressionLevel.Optimal).OpenAsync(cancellationToken);
        await stream.WriteAsync(content, cancellationToken);
    }

    private static async Task AddFileAsync(ZipArchive zip, string name, string path, CancellationToken cancellationToken)
    {
        await using var stream = await zip.CreateEntry(name, CompressionLevel.Optimal).OpenAsync(cancellationToken);
        await using var file = File.OpenRead(path);
        await file.CopyToAsync(stream, cancellationToken);
    }
}
