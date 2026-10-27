-- 执行前请将数据库名称替换为正式环境实际名称。
USE [ProjectVersionManager];
GO

SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID(N'[dbo].[PVM_PROJECT_VERSION]', N'U') IS NULL
    BEGIN
        THROW 50001, '缺少 dbo.PVM_PROJECT_VERSION，已停止执行。', 1;
    END;

    IF EXISTS
    (
        SELECT 1
        FROM [dbo].[PVM_PROJECT_VERSION]
        WHERE [STATUS] NOT IN
        (
            'PENDING',
            'WAIT_COMPILE',
            'COMPILED',
            'PRE_RELEASED',
            'RELEASED',
            'CANCELED',
            'COMMENTED'
        )
    )
    BEGIN
        THROW 50002, 'STATUS 中存在不受支持的值，已停止修改约束。', 1;
    END;

    IF EXISTS
    (
        SELECT 1
        FROM sys.check_constraints
        WHERE [name] = N'CK_PVM_PV_STATUS'
          AND [parent_object_id] = OBJECT_ID(N'[dbo].[PVM_PROJECT_VERSION]')
    )
    BEGIN
        ALTER TABLE [dbo].[PVM_PROJECT_VERSION]
            DROP CONSTRAINT [CK_PVM_PV_STATUS];
    END;

    ALTER TABLE [dbo].[PVM_PROJECT_VERSION] WITH CHECK
        ADD CONSTRAINT [CK_PVM_PV_STATUS]
        CHECK
        (
            [STATUS] IN
            (
                'PENDING',
                'WAIT_COMPILE',
                'COMPILED',
                'PRE_RELEASED',
                'RELEASED',
                'CANCELED',
                'COMMENTED'
            )
        );

    ALTER TABLE [dbo].[PVM_PROJECT_VERSION]
        CHECK CONSTRAINT [CK_PVM_PV_STATUS];

    IF OBJECT_ID(N'[dbo].[__EFMigrationsHistory]', N'U') IS NOT NULL
       AND NOT EXISTS
       (
           SELECT 1
           FROM [dbo].[__EFMigrationsHistory]
           WHERE [MigrationId] = N'20260908035214_AddCanceledStatus'
       )
    BEGIN
        INSERT INTO [dbo].[__EFMigrationsHistory]
            ([MigrationId], [ProductVersion])
        VALUES
            (N'20260908035214_AddCanceledStatus', N'8.0.11');
    END;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0
    BEGIN
        ROLLBACK TRANSACTION;
    END;

    THROW;
END CATCH;
GO

SELECT [name], [definition], [is_disabled], [is_not_trusted]
FROM sys.check_constraints
WHERE [name] = N'CK_PVM_PV_STATUS'
  AND [parent_object_id] = OBJECT_ID(N'[dbo].[PVM_PROJECT_VERSION]');

IF OBJECT_ID(N'[dbo].[__EFMigrationsHistory]', N'U') IS NOT NULL
BEGIN
    SELECT [MigrationId], [ProductVersion]
    FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260908035214_AddCanceledStatus';
END;
GO
