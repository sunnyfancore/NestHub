using System.Data;
using System.Data.Common;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using FreeSql;
using Microsoft.Data.Sqlite;
using MySqlConnector;
using NestHub.Api.Domain.Entities;

namespace NestHub.Api.Services;

public static class DatabaseMigrationService
{
    private static readonly JsonSerializerOptions CompareOptions = new()
    {
        Converters = { new ComparableDateTimeConverter() }
    };

    public static async Task<int> RunAsync(IConfiguration configuration)
    {
        var sourceConnection = configuration["Migration:MySqlConnectionString"];
        var destination = Path.GetFullPath(configuration["Migration:SqlitePath"] ?? "AppData/nesthub.db");
        if (string.IsNullOrWhiteSpace(sourceConnection))
        {
            Console.Error.WriteLine("请设置 Migration__MySqlConnectionString 为原 MySQL 连接字符串。");
            return 1;
        }
        if (File.Exists(destination))
        {
            Console.Error.WriteLine("目标 SQLite 文件已存在，迁移不会覆盖它。请选择一个新的文件路径。");
            return 1;
        }
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        var temporary = destination + ".migration-" + Guid.NewGuid().ToString("N");
        try
        {
            using (var source = new FreeSqlBuilder().UseConnectionString(DataType.MySql, sourceConnection).UseAutoSyncStructure(false).Build())
            using (var target = new FreeSqlBuilder().UseConnectionString(DataType.Sqlite,
                new SqliteConnectionStringBuilder { DataSource = temporary, Pooling = false }.ConnectionString).Build())
            {
                await using var mysql = new MySqlConnection(sourceConnection);
                await mysql.OpenAsync();
                await using (var readOnly = new MySqlCommand("SET TRANSACTION READ ONLY", mysql))
                    await readOnly.ExecuteNonQueryAsync();
                await using var snapshot = await mysql.BeginTransactionAsync(IsolationLevel.RepeatableRead);
                CreateSchema(target);
                await using var sqlite = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = temporary, Pooling = false }.ConnectionString);
                await sqlite.OpenAsync();
                await using var write = await sqlite.BeginTransactionAsync();
                await CopyAllAsync(source, target, snapshot, write);
                await write.CommitAsync();
                await snapshot.CommitAsync();
            }
            File.Move(temporary, destination, overwrite: false);
            Console.WriteLine("迁移和逐表数据校验完成：" + destination);
            Console.WriteLine("原 MySQL 未修改。请另行复制 AppData 上传文件，并保留原 JWT 密钥。");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine("迁移失败，未生成目标文件：" + exception.Message);
            return 1;
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    public static void CreateSchema(IFreeSql target) => target.CodeFirst.SyncStructure(
        typeof(Tenant), typeof(SiteSetting), typeof(Folder), typeof(Bookmark),
        typeof(ShareLink), typeof(TransitionSetting), typeof(PasswordResetToken), typeof(SmtpSetting));

    public static async Task CopyAllAsync(IFreeSql source, IFreeSql target, DbTransaction read, DbTransaction write)
    {
        await CopyTableAsync<Tenant>("tenants", source, target, read, write);
        await CopyTableAsync<SiteSetting>("site_settings", source, target, read, write);
        await CopyTableAsync<Folder>("folders", source, target, read, write);
        await CopyTableAsync<Bookmark>("bookmarks", source, target, read, write);
        await CopyTableAsync<ShareLink>("share_links", source, target, read, write, optional: true);
        await CopyTableAsync<TransitionSetting>("transition_settings", source, target, read, write, optional: true);
        await CopyTableAsync<PasswordResetToken>("password_reset_tokens", source, target, read, write, optional: true);
        await CopyTableAsync<SmtpSetting>("smtp_settings", source, target, read, write, optional: true);
    }

    private static async Task CopyTableAsync<T>(string name, IFreeSql source, IFreeSql target, DbTransaction read, DbTransaction write, bool optional = false) where T : class
    {
        if (!source.DbFirst.ExistsTable(name))
        {
            if (!optional) throw new InvalidOperationException("原数据库缺少必要表：" + name);
            Console.WriteLine(name + "：旧版本中不存在，跳过。");
            return;
        }
        var rows = await source.Select<T>().WithTransaction(read).ToListAsync();
        foreach (var batch in rows.Chunk(300))
            await target.Insert(batch).WithTransaction(write).ExecuteAffrowsAsync();
        var copied = await target.Select<T>().WithTransaction(write).ToListAsync();
        var originals = rows.Select(row => JsonSerializer.Serialize(row, CompareOptions)).Order(StringComparer.Ordinal);
        var results = copied.Select(row => JsonSerializer.Serialize(row, CompareOptions)).Order(StringComparer.Ordinal);
        if (!originals.SequenceEqual(results)) throw new InvalidOperationException("迁移数据校验失败：" + name);
        Console.WriteLine(name + "：" + rows.Count + " 条，全部字段校验通过。");
    }

    private sealed class ComparableDateTimeConverter : JsonConverter<DateTime>
    {
        public override DateTime Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options) => throw new NotSupportedException();
        public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options) => writer.WriteStringValue(value.ToString("yyyy-MM-dd'T'HH:mm:ss.fffffff", CultureInfo.InvariantCulture));
    }
}
