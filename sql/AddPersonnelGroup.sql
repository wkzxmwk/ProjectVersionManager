-- ======================================================================
-- 人员组 (PERSONNEL_GROUP) 功能数据库迁移脚本
-- 适用环境: SQL Server
-- 执行方式: 在目标数据库中直接执行本脚本
-- 注意: 所有步骤均使用 IF NOT EXISTS 保护，可重复执行
-- ======================================================================

USE [PVM_DB]; -- TODO: 替换为实际的数据库名称
GO

-- ======================================================================
-- 步骤 1: PVM_PROJECT_OPTION 表 - 添加 PERSONNEL_GROUP 列
-- ======================================================================
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('PVM_PROJECT_OPTION') AND name = 'PERSONNEL_GROUP'
)
BEGIN
    ALTER TABLE PVM_PROJECT_OPTION ADD PERSONNEL_GROUP NVARCHAR(100) NULL;
    PRINT '步骤 1: PVM_PROJECT_OPTION 表已添加 PERSONNEL_GROUP 列';
END
ELSE
BEGIN
    PRINT '步骤 1: PVM_PROJECT_OPTION 表 PERSONNEL_GROUP 列已存在，跳过';
END
GO

-- ======================================================================
-- 步骤 2: PVM_USER 表 - 添加 PERSONNEL_GROUP 列
-- ======================================================================
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('PVM_USER') AND name = 'PERSONNEL_GROUP'
)
BEGIN
    ALTER TABLE PVM_USER ADD PERSONNEL_GROUP NVARCHAR(100) NULL;
    PRINT '步骤 2: PVM_USER 表已添加 PERSONNEL_GROUP 列';
END
ELSE
BEGIN
    PRINT '步骤 2: PVM_USER 表 PERSONNEL_GROUP 列已存在，跳过';
END
GO

-- ======================================================================
-- 步骤 3: PVM_PROJECT_VERSION 表 - 添加 PERSONNEL_GROUP 列
-- ======================================================================
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('PVM_PROJECT_VERSION') AND name = 'PERSONNEL_GROUP'
)
BEGIN
    ALTER TABLE PVM_PROJECT_VERSION ADD PERSONNEL_GROUP NVARCHAR(100) NULL;
    PRINT '步骤 3: PVM_PROJECT_VERSION 表已添加 PERSONNEL_GROUP 列';
END
ELSE
BEGIN
    PRINT '步骤 3: PVM_PROJECT_VERSION 表 PERSONNEL_GROUP 列已存在，跳过';
END
GO

-- ======================================================================
-- 步骤 4: PVM_PROJECT_OPTION 表 - 重建 CHECK 约束 CK_PVM_OPTION_TYPE
-- 添加 PERSONNEL_GROUP 到允许的选项类型列表
-- ======================================================================
IF EXISTS (
    SELECT 1 FROM sys.check_constraints
    WHERE name = 'CK_PVM_OPTION_TYPE'
)
BEGIN
    ALTER TABLE PVM_PROJECT_OPTION DROP CONSTRAINT CK_PVM_OPTION_TYPE;
    PRINT '步骤 4a: 已删除旧的 CK_PVM_OPTION_TYPE 约束';
END

IF NOT EXISTS (
    SELECT 1 FROM sys.check_constraints
    WHERE name = 'CK_PVM_OPTION_TYPE'
)
BEGIN
    ALTER TABLE PVM_PROJECT_OPTION
    ADD CONSTRAINT CK_PVM_OPTION_TYPE
    CHECK ([OPTION_TYPE] IN ('PROJECT_TYPE', 'STAFF_GROUP', 'PERSONNEL_GROUP', 'DEVELOPER', 'PROGRAM_GROUP', 'PROGRAM_NAME', 'COMPILER'));
    PRINT '步骤 4b: 已创建新的 CK_PVM_OPTION_TYPE 约束 (含 PERSONNEL_GROUP)';
END
GO

-- ======================================================================
-- 步骤 5: PVM_PROJECT_OPTION 表 - 删除旧唯一索引并创建新的复合唯一索引
-- 旧索引: (OptionType, OptionValue) — 可能以 IX_ 或 UX_ 开头，名称不确定
-- 新索引: (OptionType, OptionValue, StaffGroup, PersonnelGroup, ProgramGroup)
-- 允许不同人员组下有相同专案名称
-- ======================================================================

-- 先查找 PVM_PROJECT_OPTION 表上除主键外的所有唯一索引并删除
DECLARE @DropIdxCursor CURSOR;
DECLARE @IdxName NVARCHAR(128);
SET @DropIdxCursor = CURSOR FAST_FORWARD FOR
    SELECT name
    FROM sys.indexes
    WHERE object_id = OBJECT_ID('PVM_PROJECT_OPTION')
      AND is_unique = 1
      AND is_primary_key = 0;

OPEN @DropIdxCursor;
FETCH NEXT FROM @DropIdxCursor INTO @IdxName;
WHILE @@FETCH_STATUS = 0
BEGIN
    DECLARE @sql NVARCHAR(MAX) = N'DROP INDEX [' + @IdxName + N'] ON [PVM_PROJECT_OPTION]';
    EXEC sp_executesql @sql;
    PRINT '步骤 5a: 已删除旧唯一索引 ' + @IdxName;
    FETCH NEXT FROM @DropIdxCursor INTO @IdxName;
END
CLOSE @DropIdxCursor;
DEALLOCATE @DropIdxCursor;

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'UX_PVM_OPTION_COMPOSITE'
      AND object_id = OBJECT_ID('PVM_PROJECT_OPTION')
)
BEGIN
    CREATE UNIQUE INDEX UX_PVM_OPTION_COMPOSITE
    ON PVM_PROJECT_OPTION (OPTION_TYPE, OPTION_VALUE, STAFF_GROUP, PERSONNEL_GROUP, PROGRAM_GROUP);
    PRINT '步骤 5b: 已创建新复合唯一索引 UX_PVM_OPTION_COMPOSITE';
END
ELSE
BEGIN
    PRINT '步骤 5b: UX_PVM_OPTION_COMPOSITE 已存在，跳过';
END
GO

-- ======================================================================
-- 步骤 6: PVM_PROJECT_OPTION 表 - 添加 PERSONNEL_GROUP 索引
-- ======================================================================
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'IX_PVM_OPTION_PERSONNEL_GROUP'
      AND object_id = OBJECT_ID('PVM_PROJECT_OPTION')
)
BEGIN
    CREATE INDEX IX_PVM_OPTION_PERSONNEL_GROUP
    ON PVM_PROJECT_OPTION (OPTION_TYPE, PERSONNEL_GROUP);
    PRINT '步骤 6: 已创建索引 IX_PVM_OPTION_PERSONNEL_GROUP';
END
GO

-- ======================================================================
-- 步骤 7: PVM_PROJECT_VERSION 表 - 重建唯一索引
-- 旧索引: (ProgramGroup, ProgramName, ProgramVersionNumber) WHERE [IS_DELETED] = 0
-- 新索引: (ProgramGroup, PersonnelGroup, ProgramName, ProgramVersionNumber) WHERE [IS_DELETED] = 0
-- 允许不同人员组下有相同专案+程序名+版本号
-- ======================================================================

-- 删除 PVM_PROJECT_VERSION 表上除主键外的所有唯一索引
DECLARE @DropIdxCursor2 CURSOR;
DECLARE @IdxName2 NVARCHAR(128);
SET @DropIdxCursor2 = CURSOR FAST_FORWARD FOR
    SELECT name
    FROM sys.indexes
    WHERE object_id = OBJECT_ID('PVM_PROJECT_VERSION')
      AND is_unique = 1
      AND is_primary_key = 0;

OPEN @DropIdxCursor2;
FETCH NEXT FROM @DropIdxCursor2 INTO @IdxName2;
WHILE @@FETCH_STATUS = 0
BEGIN
    DECLARE @sql2 NVARCHAR(MAX) = N'DROP INDEX [' + @IdxName2 + N'] ON [PVM_PROJECT_VERSION]';
    EXEC sp_executesql @sql2;
    PRINT '步骤 7a: 已删除旧唯一索引 ' + @IdxName2;
    FETCH NEXT FROM @DropIdxCursor2 INTO @IdxName2;
END
CLOSE @DropIdxCursor2;
DEALLOCATE @DropIdxCursor2;

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'UX_PVM_PV_ACTIVE_UQ'
      AND object_id = OBJECT_ID('PVM_PROJECT_VERSION')
)
BEGIN
    CREATE UNIQUE INDEX UX_PVM_PV_ACTIVE_UQ
    ON PVM_PROJECT_VERSION (PROGRAM_GROUP, PERSONNEL_GROUP, PROGRAM_NAME, PROGRAM_VERSION)
    WHERE [IS_DELETED] = 0;
    PRINT '步骤 7b: 已创建新唯一索引 UX_PVM_PV_ACTIVE_UQ (含 PERSONNEL_GROUP)';
END
ELSE
BEGIN
    PRINT '步骤 7b: UX_PVM_PV_ACTIVE_UQ 已存在，跳过';
END
GO

-- ======================================================================
-- 步骤 8: 记录迁移历史到 __EFMigrationsHistory
-- ======================================================================
IF NOT EXISTS (
    SELECT 1 FROM __EFMigrationsHistory
    WHERE MigrationId = '20260807160000_AddPersonnelGroup'
)
BEGIN
    INSERT INTO __EFMigrationsHistory (MigrationId, ProductVersion)
    VALUES ('20260807160000_AddPersonnelGroup', '8.0.0');
    PRINT '步骤 8: 已记录迁移历史 20260807160000_AddPersonnelGroup';
END
GO

PRINT '';
PRINT '========================================================';
PRINT '人员组 (PERSONNEL_GROUP) 数据库迁移完成';
PRINT '========================================================';
PRINT '';
PRINT '变更摘要:';
PRINT '  1. PVM_PROJECT_OPTION 添加 PERSONNEL_GROUP 列';
PRINT '  2. PVM_USER 添加 PERSONNEL_GROUP 列';
PRINT '  3. PVM_PROJECT_VERSION 添加 PERSONNEL_GROUP 列';
PRINT '  4. CK_PVM_OPTION_TYPE 约束已更新 (含 PERSONNEL_GROUP)';
PRINT '  5. PVM_PROJECT_OPTION 唯一索引已更新为复合键';
PRINT '  6. 新增 IX_PVM_OPTION_PERSONNEL_GROUP 索引';
PRINT '  7. PVM_PROJECT_VERSION 唯一索引已更新 (含 PERSONNEL_GROUP)';
PRINT '  8. 迁移历史已记录';
GO
