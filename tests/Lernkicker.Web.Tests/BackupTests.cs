using System.IO.Compression;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using Lernkicker.Application;
using Lernkicker.Contracts;
using Lernkicker.Web.Security;

namespace Lernkicker.Web.Tests;

/// <summary>The admin downloads everything as one zip; a fresh server starts from it.</summary>
public sealed class BackupTests
{
    [Fact]
    public async Task A_backup_moves_everything_to_a_new_server()
    {
        byte[] backup;
        using (var old = new LernkickerFactory())
        {
            var parent = await old.NewFamilyAsync("moving@example.org");
            await TestData.CreateChildAsync(parent);
            var admin = await old.AdminAsync();
            var response = await admin.GetAsync("/api/admin/backup");
            Assert.Equal("application/zip", response.Content.Headers.ContentType?.MediaType);
            backup = await response.Content.ReadAsByteArrayAsync();
        }

        using (var zip = new ZipArchive(new MemoryStream(backup)))
        {
            Assert.Contains(zip.Entries, e => e.FullName == "backup.json");
            Assert.Contains(zip.Entries, e => e.FullName == "lernkicker.db");
            Assert.Contains(zip.Entries, e => e.FullName.StartsWith("keys/") && e.FullName.EndsWith(".xml"));
        }

        var dataPath = Path.Combine(Path.GetTempPath(), "lernkicker-tests", Guid.NewGuid().ToString("N"));
        try
        {
            using (var fresh = new LernkickerFactory { SeedAdmin = false, DataPath = dataPath })
            {
                var response = await fresh.Client().PostAsync("/api/setup/restore", new ByteArrayContent(backup));
                Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
                Assert.Equal(1, fresh.Restarts.Count);
            }

            // "Restarted": the restore is applied before the database is opened
            using var restarted = new LernkickerFactory { SeedAdmin = false, DataPath = dataPath };
            var client = restarted.Client();
            var login = await client.PostAsJsonAsync("/api/account/login", new Login("moving@example.org", "parent-password-123"));
            Assert.Equal(HttpStatusCode.NoContent, login.StatusCode);
            Assert.Single(await LernkickerFactory.Json<List<ChildOverview>>(await client.GetAsync("/api/children")));
            Assert.False((await LernkickerFactory.Json<SetupStatus>(await client.GetAsync("/api/setup"))).Needed);
            Assert.Single(Directory.GetDirectories(dataPath, "before-restore-*"));
        }
        finally
        {
            Directory.Delete(dataPath, recursive: true);
        }
    }

    [Fact]
    public async Task Only_a_lernkicker_backup_is_restored()
    {
        using var fresh = new LernkickerFactory { SeedAdmin = false };
        var client = fresh.Client();

        var notAZip = await client.PostAsync("/api/setup/restore", new ByteArrayContent(Encoding.UTF8.GetBytes("hello")));
        var otherZip = await client.PostAsync("/api/setup/restore", new ByteArrayContent(Zip(("readme.txt", "hello"))));
        var fakeDatabase = await client.PostAsync("/api/setup/restore", new ByteArrayContent(Zip(
            ("backup.json", """{"format":"lernkicker-backup/v1"}"""), ("lernkicker.db", "SQLite format 3\0 but not really"), ("keys/../../evil.xml", "<x/>"))));

        Assert.Equal(ErrorCodes.BackupInvalid, (await LernkickerFactory.Error(notAZip, HttpStatusCode.BadRequest)).Code);
        Assert.Equal(ErrorCodes.BackupInvalid, (await LernkickerFactory.Error(otherZip, HttpStatusCode.BadRequest)).Code);
        Assert.Equal(ErrorCodes.BackupInvalid, (await LernkickerFactory.Error(fakeDatabase, HttpStatusCode.BadRequest)).Code);
        Assert.Equal(0, fresh.Restarts.Count);
        Assert.False(File.Exists(Path.Combine(fresh.DataPath, "..", "evil.xml")));
        Assert.False(Directory.Exists(Path.Combine(fresh.DataPath, "restore")));
    }

    [Fact]
    public async Task A_server_in_use_cannot_be_overwritten()
    {
        using var server = new LernkickerFactory();
        var admin = await server.AdminAsync();
        var backup = await (await admin.GetAsync("/api/admin/backup")).Content.ReadAsByteArrayAsync();

        var restore = await server.Client().PostAsync("/api/setup/restore", new ByteArrayContent(backup));

        Assert.Equal(ErrorCodes.SetupDone, (await LernkickerFactory.Error(restore, HttpStatusCode.BadRequest)).Code);
        Assert.Equal(0, server.Restarts.Count);
    }

    [Fact]
    public async Task Only_the_admin_inside_the_tailnet_downloads_a_backup()
    {
        using var server = new LernkickerFactory();
        var parent = await server.NewFamilyAsync("backup-parent@example.org");
        var admin = await server.AdminAsync();
        var viaFunnel = new HttpRequestMessage(HttpMethod.Get, "/api/admin/backup");
        viaFunnel.Headers.Add(TailnetOnlyFilter.FunnelHeader, "?1");

        Assert.Equal(HttpStatusCode.Forbidden, (await parent.GetAsync("/api/admin/backup")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await server.Client().GetAsync("/api/admin/backup")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.SendAsync(viaFunnel)).StatusCode);
    }

    private static byte[] Zip(params (string Name, string Content)[] entries)
    {
        using var buffer = new MemoryStream();
        using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, content) in entries)
            {
                using var writer = new StreamWriter(zip.CreateEntry(name).Open());
                writer.Write(content);
            }
        }

        return buffer.ToArray();
    }
}
