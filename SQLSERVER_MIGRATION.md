# SQL Server Express 本地迁移记录

本次迁移只针对本地开发环境：

```text
Vue 3 → ASP.NET Core 8 API → SQL Server Express (.\SQLEXPRESS)
```

公司 Oracle 正式环境没有被连接、迁移或修改。原 Oracle DBA 脚本已移动到 `database/OracleBackup/PVM_DBA_COMPLETE.sql`。

## 主要修改

- API 保持 .NET 8 和 EF Core 8.0.11，不升级大版本。
- 添加 `Microsoft.EntityFrameworkCore.SqlServer` 8.0.11 和 EF Design 8.0.11。
- `Program.cs` 使用 `GetConnectionString("DefaultConnection")` 和 `UseSqlServer`。
- 开发连接字符串位于 `appsettings.Development.json`，使用 Windows 身份验证。
- `DatabaseSchemaValidator` 改用 SQL Server `sys.tables`、`sys.columns` 和 `sys.check_constraints` 字典查询。
- Oracle 类型已映射为 SQL Server 类型：`nvarchar`、`nvarchar(max)`、`bit`、`int`、`bigint`、`datetime2`。
- 原生 Oracle Provider 和 Oracle 专用运行时代码已清理；Vue 页面、路由、接口地址和 JSON 结构未改变。

## 数据库结构

EF Core SQL Server 初始迁移源码位于：

`src/ProjectVersionManager.Api/Data/Migrations/SqlServer`

可审查脚本位于：

`database/SqlServer_Initial.sql`

脚本包含 5 张业务表、主键、唯一约束、查询索引、状态/权限检查约束、软删除字段、乐观锁字段、默认值以及 `PVM_USER_PERMISSION` 到 `PVM_USER` 的级联外键。

## 常用命令

```powershell
dotnet restore .\ProjectVersionManager.sln
dotnet build .\ProjectVersionManager.sln -c Release
dotnet test .\ProjectVersionManager.sln -c Release

dotnet ef database update `
  --project .\src\ProjectVersionManager.Api `
  --startup-project .\src\ProjectVersionManager.Api `
  --context PvmDbContext
```

如果只需要审查而不直接更新数据库，执行 `database/SqlServer_Initial.sql` 前应先由 DBA 确认数据库名称、权限和备份策略。
