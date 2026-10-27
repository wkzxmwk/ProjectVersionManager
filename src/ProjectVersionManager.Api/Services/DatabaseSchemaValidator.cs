using System.Data;
using Microsoft.EntityFrameworkCore;
using ProjectVersionManager.Api.Data;

namespace ProjectVersionManager.Api.Services;

public sealed class DatabaseSchemaValidator(
    PvmDbContext db,
    ILogger<DatabaseSchemaValidator> logger)
{
    private static readonly IReadOnlyDictionary<string, string[]> ExpectedColumns =
        new Dictionary<string, string[]>
        {
            ["PVM_USER"] =
            [
                "ID", "USERNAME", "PASSWORD_HASH", "DISPLAY_NAME", "ROLE_CODE",
                "IS_ENABLED", "CREATED_AT", "UPDATED_AT"
            ],
            ["PVM_PROJECT_VERSION"] =
            [
                "ID", "PROJECT_DATE", "PROJECT_TYPE", "DEVELOPER", "PROGRAM_GROUP",
                "PROGRAM_NAME", "PROGRAM_VERSION", "COMPILER", "COMPILE_TIME",
                "COMPILED_PROGRAM_VERSION", "VERSION_DESCRIPTION", "RELEASE_TIME",
                "BRANCH_NAME", "REMARK1", "REMARK2", "STATUS", "CREATED_BY",
                "CREATED_AT", "UPDATED_BY", "UPDATED_AT", "IS_DELETED", "ROW_VERSION"
            ],
            ["PVM_OPERATION_LOG"] =
            [
                "ID", "BUSINESS_ID", "OPERATION_TYPE", "OPERATOR_USERNAME", "OPERATION_TIME",
                "PROGRAM_GROUP", "PROGRAM_NAME", "PROGRAM_VERSION", "OPERATION_DETAIL",
                "BEFORE_DATA", "AFTER_DATA", "IP_ADDRESS", "USER_AGENT"
            ],
            ["PVM_PROJECT_OPTION"] =
            [
                "ID", "OPTION_TYPE", "OPTION_VALUE", "STAFF_GROUP", "PROGRAM_GROUP", "SORT_ORDER", "CREATED_BY",
                "CREATED_AT", "UPDATED_BY", "UPDATED_AT", "ROW_VERSION"
            ],
            ["PVM_USER_PERMISSION"] =
            [
                "ID", "USER_ID", "PERMISSION_CODE", "CREATED_BY", "CREATED_AT"
            ]
        };

    public async Task ValidateAsync(CancellationToken cancellationToken = default)
    {
        var connection = db.Database.GetDbConnection();
        var wasClosed = connection.State == ConnectionState.Closed;
        try
        {
            if (wasClosed)
            {
                await connection.OpenAsync(cancellationToken);
            }

            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT t.name AS TABLE_NAME, c.name AS COLUMN_NAME
                FROM sys.tables AS t
                INNER JOIN sys.columns AS c ON c.object_id = t.object_id
                WHERE t.name IN (
                    'PVM_USER', 'PVM_PROJECT_VERSION', 'PVM_OPERATION_LOG',
                    'PVM_PROJECT_OPTION', 'PVM_USER_PERMISSION'
                )
                ORDER BY t.name, c.column_id
                """;
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            var actual = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            while (await reader.ReadAsync(cancellationToken))
            {
                var table = reader.GetString(0);
                var column = reader.GetString(1);
                if (!actual.TryGetValue(table, out var columns))
                {
                    columns = [];
                    actual[table] = columns;
                }
                columns.Add(column);
            }

            await reader.DisposeAsync();
            await using var constraintCommand = connection.CreateCommand();
            constraintCommand.CommandText = """
                SELECT cc.name
                FROM sys.check_constraints AS cc
                INNER JOIN sys.tables AS t ON t.object_id = cc.parent_object_id
                WHERE cc.name = 'CK_PVM_LOG_OP_TYPE'
                  AND t.name = 'PVM_OPERATION_LOG'
                """;
            await using var constraintReader = await constraintCommand.ExecuteReaderAsync(cancellationToken);
            if (await constraintReader.ReadAsync(cancellationToken))
            {
                logger.LogInformation(
                    "SQL Server 操作日志类型约束 {ConstraintName} 已存在。",
                    constraintReader.GetString(0));
            }
            await constraintReader.DisposeAsync();

            await using var nullabilityCommand = connection.CreateCommand();
            nullabilityCommand.CommandText = """
                SELECT c.name
                FROM sys.columns AS c
                INNER JOIN sys.tables AS t ON t.object_id = c.object_id
                WHERE t.name = 'PVM_PROJECT_VERSION'
                  AND c.is_nullable = 0
                ORDER BY c.column_id
                """;
            await using var nullabilityReader =
                await nullabilityCommand.ExecuteReaderAsync(cancellationToken);
            var requiredProjectVersionColumns = new List<string>();
            while (await nullabilityReader.ReadAsync(cancellationToken))
            {
                requiredProjectVersionColumns.Add(nullabilityReader.GetString(0));
            }
            logger.LogInformation(
                "PVM_PROJECT_VERSION 数据库必填列: {RequiredColumns}",
                string.Join(", ", requiredProjectVersionColumns));

            foreach (var (table, expectedColumns) in ExpectedColumns)
            {
                if (!actual.TryGetValue(table, out var actualColumns))
                {
                    logger.LogError("缺少数据库表 {Table}。", table);
                    continue;
                }

                var missing = expectedColumns.Except(actualColumns, StringComparer.OrdinalIgnoreCase).ToArray();
                if (missing.Length > 0)
                {
                    logger.LogWarning(
                        "表 {Table} 缺少列: {MissingColumns}。实际列: {ActualColumns}",
                        table,
                        string.Join(", ", missing),
                        string.Join(", ", actualColumns));
                }
            }
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "数据库结构检查失败。");
        }
        finally
        {
            if (wasClosed && connection.State != ConnectionState.Closed)
            {
                await connection.CloseAsync();
            }
        }
    }
}
