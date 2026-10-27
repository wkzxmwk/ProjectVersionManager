-- Project Version Manager - Oracle DBA complete install/upgrade script
-- Target: Oracle 11g or later
-- Execute as the application schema owner (the schema that owns PVM_* tables).
--
-- This is the only database DDL file that should be maintained for the project.
-- It is safe to run against an already initialized schema for the objects and
-- constraints covered below. Existing data is not deleted or changed, except
-- for the constraint/column-definition corrections explicitly documented here.
--
-- Before execution:
--   1. Take a schema backup.
--   2. Run the duplicate query in section 1 and resolve returned rows before
--      the active-version unique index is created.
--   3. Grant the API schema normal DML/DDL access to these PVM_* objects.
--
-- The API creates the development admin account and hashes its password at
-- startup. Do not insert a plaintext password in this script.

SET DEFINE OFF;

------------------------------------------------------------------------
-- 1. Preflight: active duplicate versions (must return no rows)
--    This query is executed after section 2 creates the tables.
------------------------------------------------------------------------

------------------------------------------------------------------------
-- 2. Tables
--    The guards make this script suitable for a fresh schema or an
--    existing schema that already contains some of the PVM tables.
------------------------------------------------------------------------
DECLARE
    V_COUNT NUMBER;
BEGIN
    SELECT COUNT(*) INTO V_COUNT FROM USER_TABLES WHERE TABLE_NAME = 'PVM_USER';
    IF V_COUNT = 0 THEN
        EXECUTE IMMEDIATE q'~
            CREATE TABLE PVM_USER
            (
                ID            VARCHAR2(36 CHAR)  NOT NULL,
                USERNAME      VARCHAR2(100 CHAR) NOT NULL,
                PASSWORD_HASH VARCHAR2(500 CHAR) NOT NULL,
                DISPLAY_NAME  VARCHAR2(100 CHAR),
                ROLE_CODE     VARCHAR2(50 CHAR)  DEFAULT 'USER' NOT NULL,
                IS_ENABLED    NUMBER(1)          DEFAULT 1 NOT NULL,
                CREATED_AT    TIMESTAMP(6)       DEFAULT SYSTIMESTAMP NOT NULL,
                UPDATED_AT    TIMESTAMP(6)       DEFAULT SYSTIMESTAMP NOT NULL,
                CONSTRAINT PK_PVM_USER PRIMARY KEY (ID),
                CONSTRAINT UK_PVM_USER_USERNAME UNIQUE (USERNAME),
                CONSTRAINT CK_PVM_USER_ENABLED CHECK (IS_ENABLED IN (0, 1)),
                CONSTRAINT CK_PVM_USER_ROLE CHECK (ROLE_CODE IN ('ADMIN', 'USER'))
            )~';
    END IF;
END;
/

DECLARE
    V_COUNT NUMBER;
BEGIN
    SELECT COUNT(*) INTO V_COUNT
    FROM USER_TABLES
    WHERE TABLE_NAME = 'PVM_PROJECT_VERSION';
    IF V_COUNT = 0 THEN
        EXECUTE IMMEDIATE q'~
            CREATE TABLE PVM_PROJECT_VERSION
            (
                ID                       VARCHAR2(36 CHAR)   NOT NULL,
                PROJECT_DATE             DATE                NOT NULL,
                PROJECT_TYPE             VARCHAR2(50 CHAR)  NOT NULL,
                DEVELOPER                VARCHAR2(100 CHAR) NOT NULL,
                PROGRAM_GROUP            VARCHAR2(100 CHAR) NOT NULL,
                PROGRAM_NAME             VARCHAR2(200 CHAR) NOT NULL,
                PROGRAM_VERSION          VARCHAR2(50 CHAR)  NOT NULL,
                COMPILER                 VARCHAR2(100 CHAR),
                COMPILE_TIME             TIMESTAMP(6),
                COMPILED_PROGRAM_VERSION VARCHAR2(200 CHAR),
                VERSION_DESCRIPTION      VARCHAR2(2000 CHAR),
                RELEASE_TIME             TIMESTAMP(6),
                BRANCH_NAME              VARCHAR2(300 CHAR),
                REMARK1                  VARCHAR2(2000 CHAR),
                REMARK2                  VARCHAR2(2000 CHAR),
                STATUS                   VARCHAR2(30 CHAR)   DEFAULT 'PENDING' NOT NULL,
                CREATED_BY               VARCHAR2(100 CHAR) NOT NULL,
                CREATED_AT               TIMESTAMP(6)       DEFAULT SYSTIMESTAMP NOT NULL,
                UPDATED_BY               VARCHAR2(100 CHAR) NOT NULL,
                UPDATED_AT               TIMESTAMP(6)       DEFAULT SYSTIMESTAMP NOT NULL,
                IS_DELETED               NUMBER(1)           DEFAULT 0 NOT NULL,
                ROW_VERSION              NUMBER(19)          DEFAULT 1 NOT NULL,
                CONSTRAINT PK_PVM_PROJECT_VERSION PRIMARY KEY (ID),
                CONSTRAINT CK_PVM_PV_STATUS CHECK
                    (STATUS IN ('PENDING', 'COMPILED', 'PRE_RELEASED', 'RELEASED', 'COMMENTED')),
                CONSTRAINT CK_PVM_PV_DELETED CHECK (IS_DELETED IN (0, 1))
            )~';
    END IF;
END;
/

DECLARE
    V_COUNT NUMBER;
BEGIN
    SELECT COUNT(*) INTO V_COUNT
    FROM USER_TABLES
    WHERE TABLE_NAME = 'PVM_OPERATION_LOG';
    IF V_COUNT = 0 THEN
        EXECUTE IMMEDIATE q'~
            CREATE TABLE PVM_OPERATION_LOG
            (
                ID                VARCHAR2(36 CHAR)   NOT NULL,
                BUSINESS_ID       VARCHAR2(36 CHAR)   NOT NULL,
                OPERATION_TYPE    VARCHAR2(20 CHAR)   NOT NULL,
                OPERATOR_USERNAME  VARCHAR2(100 CHAR)  NOT NULL,
                OPERATION_TIME    TIMESTAMP(6)        DEFAULT SYSTIMESTAMP NOT NULL,
                PROGRAM_GROUP     VARCHAR2(100 CHAR),
                PROGRAM_NAME      VARCHAR2(200 CHAR),
                PROGRAM_VERSION   VARCHAR2(50 CHAR),
                OPERATION_DETAIL  VARCHAR2(2000 CHAR),
                BEFORE_DATA       CLOB,
                AFTER_DATA        CLOB,
                IP_ADDRESS        VARCHAR2(64 CHAR),
                USER_AGENT        VARCHAR2(1000 CHAR),
                CONSTRAINT PK_PVM_OPERATION_LOG PRIMARY KEY (ID),
                CONSTRAINT CK_PVM_LOG_OP_TYPE CHECK
                    (OPERATION_TYPE IN ('CREATE', 'UPDATE', 'DELETE', 'RESTORE'))
            )~';
    END IF;
END;
/

DECLARE
    V_COUNT NUMBER;
BEGIN
    SELECT COUNT(*) INTO V_COUNT
    FROM USER_TABLES
    WHERE TABLE_NAME = 'PVM_PROJECT_OPTION';
    IF V_COUNT = 0 THEN
        EXECUTE IMMEDIATE q'~
            CREATE TABLE PVM_PROJECT_OPTION
            (
                ID           VARCHAR2(36 CHAR)  NOT NULL,
                OPTION_TYPE  VARCHAR2(30 CHAR)  NOT NULL,
                OPTION_VALUE VARCHAR2(200 CHAR) NOT NULL,
                STAFF_GROUP  VARCHAR2(100 CHAR),
                PROGRAM_GROUP VARCHAR2(100 CHAR),
                SORT_ORDER   NUMBER(10)         DEFAULT 0 NOT NULL,
                CREATED_BY   VARCHAR2(100 CHAR) NOT NULL,
                CREATED_AT   TIMESTAMP(6)       DEFAULT SYSTIMESTAMP NOT NULL,
                UPDATED_BY   VARCHAR2(100 CHAR) NOT NULL,
                UPDATED_AT   TIMESTAMP(6)       DEFAULT SYSTIMESTAMP NOT NULL,
                ROW_VERSION  NUMBER(19)         DEFAULT 1 NOT NULL,
                CONSTRAINT PK_PVM_PROJECT_OPTION PRIMARY KEY (ID),
                CONSTRAINT UK_PVM_OPTION_TYPE_VALUE UNIQUE (OPTION_TYPE, OPTION_VALUE),
                CONSTRAINT CK_PVM_OPTION_TYPE CHECK
                    (OPTION_TYPE IN
                        ('PROJECT_TYPE', 'STAFF_GROUP', 'DEVELOPER', 'PROGRAM_GROUP', 'PROGRAM_NAME', 'COMPILER'))
            )~';
    END IF;
END;
/

DECLARE
    V_COUNT NUMBER;
BEGIN
    SELECT COUNT(*) INTO V_COUNT
    FROM USER_TABLES
    WHERE TABLE_NAME = 'PVM_USER_PERMISSION';
    IF V_COUNT = 0 THEN
        EXECUTE IMMEDIATE q'~
            CREATE TABLE PVM_USER_PERMISSION
            (
                ID              VARCHAR2(36 CHAR)  NOT NULL,
                USER_ID         VARCHAR2(36 CHAR)  NOT NULL,
                PERMISSION_CODE VARCHAR2(50 CHAR)  NOT NULL,
                CREATED_BY      VARCHAR2(100 CHAR) NOT NULL,
                CREATED_AT      TIMESTAMP(6)       DEFAULT SYSTIMESTAMP NOT NULL,
                CONSTRAINT PK_PVM_USER_PERMISSION PRIMARY KEY (ID),
                CONSTRAINT UK_PVM_USER_PERMISSION UNIQUE (USER_ID, PERMISSION_CODE),
                CONSTRAINT FK_PVM_UP_USER FOREIGN KEY (USER_ID)
                    REFERENCES PVM_USER (ID) ON DELETE CASCADE,
                CONSTRAINT CK_PVM_UP_PERMISSION CHECK
                    (PERMISSION_CODE IN
                    (
                        'VERSION_VIEW', 'VERSION_CREATE', 'VERSION_UPDATE',
                        'VERSION_DELETE', 'VERSION_IMPORT', 'LOG_VIEW',
                        'OPTION_MANAGE', 'PROGRAM_RELATION_MANAGE', 'PERSONNEL_MANAGE'
                    ))
            )~';
    END IF;
END;
/

SELECT
    UPPER(TRIM(PROGRAM_GROUP)) AS PROGRAM_GROUP,
    UPPER(TRIM(PROGRAM_NAME)) AS PROGRAM_NAME,
    UPPER(TRIM(PROGRAM_VERSION)) AS PROGRAM_VERSION,
    COUNT(*) AS DUPLICATE_COUNT
FROM PVM_PROJECT_VERSION
WHERE NVL(IS_DELETED, 0) = 0
GROUP BY
    UPPER(TRIM(PROGRAM_GROUP)),
    UPPER(TRIM(PROGRAM_NAME)),
    UPPER(TRIM(PROGRAM_VERSION))
HAVING COUNT(*) > 1;

------------------------------------------------------------------------
-- 3. Corrections kept in the consolidated script
------------------------------------------------------------------------
-- Program-name options can be associated with one project group. Existing
-- program-name rows remain NULL until an administrator assigns their group.
DECLARE
    V_COUNT NUMBER;
BEGIN
    SELECT COUNT(*) INTO V_COUNT
    FROM USER_TAB_COLUMNS
    WHERE TABLE_NAME = 'PVM_PROJECT_OPTION'
      AND COLUMN_NAME = 'PROGRAM_GROUP';
    IF V_COUNT = 0 THEN
        EXECUTE IMMEDIATE
            'ALTER TABLE PVM_PROJECT_OPTION ADD (PROGRAM_GROUP VARCHAR2(100 CHAR))';
    END IF;
END;
/

-- VERSION_DESCRIPTION is optional in the UI/API.
ALTER TABLE PVM_PROJECT_VERSION MODIFY (VERSION_DESCRIPTION NULL);

-- Replace any older operation-log check constraint with the current API values.
BEGIN
    EXECUTE IMMEDIATE 'ALTER TABLE PVM_OPERATION_LOG DROP CONSTRAINT CK_PVM_LOG_OP_TYPE';
EXCEPTION
    WHEN OTHERS THEN
        IF SQLCODE != -2443 THEN RAISE; END IF;
END;
/
ALTER TABLE PVM_OPERATION_LOG ADD CONSTRAINT CK_PVM_LOG_OP_TYPE
    CHECK (OPERATION_TYPE IN ('CREATE', 'UPDATE', 'DELETE', 'RESTORE'));

-- Replace old status constraints (including generated names) with the current
-- five statuses: pending, compiled, pre-released, released, commented.
BEGIN
    FOR ITEM IN
    (
        SELECT DISTINCT C.CONSTRAINT_NAME
        FROM USER_CONSTRAINTS C
        JOIN USER_CONS_COLUMNS CC
          ON CC.CONSTRAINT_NAME = C.CONSTRAINT_NAME
         AND CC.TABLE_NAME = C.TABLE_NAME
        WHERE C.TABLE_NAME = 'PVM_PROJECT_VERSION'
          AND C.CONSTRAINT_TYPE = 'C'
          AND CC.COLUMN_NAME = 'STATUS'
    )
    LOOP
        EXECUTE IMMEDIATE
            'ALTER TABLE PVM_PROJECT_VERSION DROP CONSTRAINT ' ||
            DBMS_ASSERT.SIMPLE_SQL_NAME(ITEM.CONSTRAINT_NAME);
    END LOOP;
END;
/
ALTER TABLE PVM_PROJECT_VERSION ADD CONSTRAINT CK_PVM_PV_STATUS
    CHECK (STATUS IN ('PENDING', 'COMPILED', 'PRE_RELEASED', 'RELEASED', 'COMMENTED'));

-- Ensure constraints that may be absent in an older manually-created schema.
BEGIN
    EXECUTE IMMEDIATE 'ALTER TABLE PVM_USER_PERMISSION DROP CONSTRAINT CK_PVM_UP_PERMISSION';
EXCEPTION
    WHEN OTHERS THEN
        IF SQLCODE != -2443 THEN RAISE; END IF;
END;
/

DECLARE
    V_COUNT NUMBER;
BEGIN
    SELECT COUNT(*) INTO V_COUNT FROM USER_CONSTRAINTS
    WHERE CONSTRAINT_NAME = 'CK_PVM_OPTION_TYPE';
    IF V_COUNT = 0 THEN
        EXECUTE IMMEDIATE
            'ALTER TABLE PVM_PROJECT_OPTION ADD CONSTRAINT CK_PVM_OPTION_TYPE ' ||
            'CHECK (OPTION_TYPE IN (''PROJECT_TYPE'', ''STAFF_GROUP'', ''DEVELOPER'', ' ||
            '''PROGRAM_GROUP'', ''PROGRAM_NAME'', ''COMPILER''))';
    END IF;

    SELECT COUNT(*) INTO V_COUNT FROM USER_CONSTRAINTS
    WHERE CONSTRAINT_NAME = 'CK_PVM_UP_PERMISSION';
    IF V_COUNT = 0 THEN
        EXECUTE IMMEDIATE
            'ALTER TABLE PVM_USER_PERMISSION ADD CONSTRAINT CK_PVM_UP_PERMISSION ' ||
            'CHECK (PERMISSION_CODE IN (''VERSION_VIEW'', ''VERSION_CREATE'', ' ||
            '''VERSION_UPDATE'', ''VERSION_DELETE'', ''VERSION_IMPORT'', ' ||
            '''LOG_VIEW'', ''OPTION_MANAGE'', ''PROGRAM_RELATION_MANAGE'', ' ||
            '''PERSONNEL_MANAGE''))';
    END IF;

    SELECT COUNT(*) INTO V_COUNT FROM USER_CONSTRAINTS
    WHERE CONSTRAINT_NAME = 'CK_PVM_PV_DELETED';
    IF V_COUNT = 0 THEN
        EXECUTE IMMEDIATE
            'ALTER TABLE PVM_PROJECT_VERSION ADD CONSTRAINT CK_PVM_PV_DELETED ' ||
            'CHECK (IS_DELETED IN (0, 1))';
    END IF;
END;
/

------------------------------------------------------------------------
-- 4. Indexes
------------------------------------------------------------------------
DECLARE
    V_COUNT NUMBER;
BEGIN
    SELECT COUNT(*) INTO V_COUNT FROM USER_INDEXES
    WHERE INDEX_NAME = 'IX_PVM_PV_PROJECT_DATE';
    IF V_COUNT = 0 THEN
        EXECUTE IMMEDIATE 'CREATE INDEX IX_PVM_PV_PROJECT_DATE ON PVM_PROJECT_VERSION (PROJECT_DATE)';
    END IF;
    SELECT COUNT(*) INTO V_COUNT FROM USER_INDEXES
    WHERE INDEX_NAME = 'IX_PVM_PV_PROJECT_TYPE';
    IF V_COUNT = 0 THEN
        EXECUTE IMMEDIATE 'CREATE INDEX IX_PVM_PV_PROJECT_TYPE ON PVM_PROJECT_VERSION (PROJECT_TYPE)';
    END IF;
    SELECT COUNT(*) INTO V_COUNT FROM USER_INDEXES
    WHERE INDEX_NAME = 'IX_PVM_PV_DEVELOPER';
    IF V_COUNT = 0 THEN
        EXECUTE IMMEDIATE 'CREATE INDEX IX_PVM_PV_DEVELOPER ON PVM_PROJECT_VERSION (DEVELOPER)';
    END IF;
    SELECT COUNT(*) INTO V_COUNT FROM USER_INDEXES
    WHERE INDEX_NAME = 'IX_PVM_PV_PROGRAM_NAME';
    IF V_COUNT = 0 THEN
        EXECUTE IMMEDIATE 'CREATE INDEX IX_PVM_PV_PROGRAM_NAME ON PVM_PROJECT_VERSION (PROGRAM_NAME)';
    END IF;
    SELECT COUNT(*) INTO V_COUNT FROM USER_INDEXES
    WHERE INDEX_NAME = 'IX_PVM_PV_RELEASE_TIME';
    IF V_COUNT = 0 THEN
        EXECUTE IMMEDIATE 'CREATE INDEX IX_PVM_PV_RELEASE_TIME ON PVM_PROJECT_VERSION (RELEASE_TIME)';
    END IF;
    SELECT COUNT(*) INTO V_COUNT FROM USER_INDEXES
    WHERE INDEX_NAME = 'IX_PVM_PV_IS_DELETED';
    IF V_COUNT = 0 THEN
        EXECUTE IMMEDIATE 'CREATE INDEX IX_PVM_PV_IS_DELETED ON PVM_PROJECT_VERSION (IS_DELETED)';
    END IF;
    SELECT COUNT(*) INTO V_COUNT FROM USER_INDEXES
    WHERE INDEX_NAME = 'IX_PVM_LOG_TIME';
    IF V_COUNT = 0 THEN
        EXECUTE IMMEDIATE 'CREATE INDEX IX_PVM_LOG_TIME ON PVM_OPERATION_LOG (OPERATION_TIME)';
    END IF;
    SELECT COUNT(*) INTO V_COUNT FROM USER_INDEXES
    WHERE INDEX_NAME = 'IX_PVM_LOG_BUSINESS_ID';
    IF V_COUNT = 0 THEN
        EXECUTE IMMEDIATE 'CREATE INDEX IX_PVM_LOG_BUSINESS_ID ON PVM_OPERATION_LOG (BUSINESS_ID)';
    END IF;
    SELECT COUNT(*) INTO V_COUNT FROM USER_INDEXES
    WHERE INDEX_NAME = 'IX_PVM_UP_USER_ID';
    IF V_COUNT = 0 THEN
        EXECUTE IMMEDIATE 'CREATE INDEX IX_PVM_UP_USER_ID ON PVM_USER_PERMISSION (USER_ID)';
    END IF;
    SELECT COUNT(*) INTO V_COUNT FROM USER_INDEXES
    WHERE INDEX_NAME = 'IX_PVM_OPTION_GROUP';
    IF V_COUNT = 0 THEN
        EXECUTE IMMEDIATE 'CREATE INDEX IX_PVM_OPTION_GROUP ON PVM_PROJECT_OPTION (OPTION_TYPE, PROGRAM_GROUP)';
    END IF;
END;
/

-- Concurrency-safe uniqueness for active records only. Deleted historical
-- records are intentionally excluded. The three expressions are NULL for a
-- deleted row, so Oracle permits multiple deleted historical records.
DECLARE
    V_COUNT NUMBER;
BEGIN
    SELECT COUNT(*) INTO V_COUNT
    FROM USER_INDEXES
    WHERE INDEX_NAME = 'UX_PVM_PV_ACTIVE_UQ';
    IF V_COUNT = 0 THEN
        EXECUTE IMMEDIATE q'~
            CREATE UNIQUE INDEX UX_PVM_PV_ACTIVE_UQ
            ON PVM_PROJECT_VERSION
            (
                CASE WHEN NVL(IS_DELETED, 0) = 0 THEN UPPER(TRIM(PROGRAM_GROUP)) END,
                CASE WHEN NVL(IS_DELETED, 0) = 0 THEN UPPER(TRIM(PROGRAM_NAME)) END,
                CASE WHEN NVL(IS_DELETED, 0) = 0 THEN UPPER(TRIM(PROGRAM_VERSION)) END
            )~';
    END IF;
END;
/

------------------------------------------------------------------------
-- 5. Documentation metadata
------------------------------------------------------------------------
COMMENT ON TABLE PVM_USER IS '项目版本管理系统用户表';
COMMENT ON TABLE PVM_PROJECT_VERSION IS '项目版本记录表；IS_DELETED=1 表示软删除';
COMMENT ON TABLE PVM_OPERATION_LOG IS '项目版本管理系统操作审计日志';
COMMENT ON TABLE PVM_PROJECT_OPTION IS '项目版本表单可选项';
COMMENT ON TABLE PVM_USER_PERMISSION IS '项目版本管理系统用户权限关联表';

COMMENT ON COLUMN PVM_PROJECT_VERSION.VERSION_DESCRIPTION IS '程序版本描述，可为空';
COMMENT ON COLUMN PVM_PROJECT_VERSION.REMARK1 IS '备注1';
COMMENT ON COLUMN PVM_PROJECT_VERSION.REMARK2 IS '备注2';
COMMENT ON COLUMN PVM_PROJECT_VERSION.ROW_VERSION IS '应用层乐观锁版本号';
COMMENT ON COLUMN PVM_PROJECT_OPTION.PROGRAM_GROUP IS '程序名选项所属项目组；仅 PROGRAM_NAME 使用';
COMMENT ON COLUMN PVM_USER_PERMISSION.PERMISSION_CODE IS '系统权限代码；ADMIN 角色无需记录即可拥有全部权限';

------------------------------------------------------------------------
-- 6. Post-checks for DBA sign-off
------------------------------------------------------------------------
SELECT TABLE_NAME FROM USER_TABLES
WHERE TABLE_NAME IN
    ('PVM_USER', 'PVM_PROJECT_VERSION', 'PVM_OPERATION_LOG',
     'PVM_PROJECT_OPTION', 'PVM_USER_PERMISSION')
ORDER BY TABLE_NAME;

SELECT INDEX_NAME, TABLE_NAME, UNIQUENESS
FROM USER_INDEXES
WHERE INDEX_NAME LIKE 'PVM_%'
   OR INDEX_NAME LIKE 'IX_PVM_%'
   OR INDEX_NAME LIKE 'UX_PVM_%'
ORDER BY TABLE_NAME, INDEX_NAME;

-- No INSERT is performed here:
--   * The API creates/repairs the development admin account using
--     DevelopmentAdmin:Password and ASP.NET Core PasswordHasher.
--   * Project options and personnel are maintained through the authorized UI.
--   * If production requires initial option data, load approved values with a
--     separate DBA change script after business-owner review.

COMMIT;
