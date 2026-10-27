# 数据库连接配置指南（SQL Server Express）

当前本地开发环境使用 SQL Server Express，API 通过 EF Core 8.0.11 的 SQL Server Provider 访问数据库。Vue 前端不会直接连接数据库。

## 本地连接字符串

`src/ProjectVersionManager.Api/appsettings.Development.json` 已配置：

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=.\\SQLEXPRESS;Database=ProjectVersionManager_Dev;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True"
  }
}
```

生产环境不要把账号密码提交到仓库。可以用环境变量覆盖配置：

```powershell
$env:ConnectionStrings__DefaultConnection = "Server=服务器名;Database=ProjectVersionManager;User Id=账号;Password=密码;TrustServerCertificate=True;"
```

## API 读取方式

`Program.cs` 从配置系统读取 `ConnectionStrings:DefaultConnection`，并调用 `UseSqlServer`：

```csharp
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<PvmDbContext>(options =>
    options.UseSqlServer(connectionString));
```

代码中没有写死数据库连接字符串。

## 创建数据库结构

完整可审查脚本为 [database/SqlServer_Initial.sql](database/SqlServer_Initial.sql)，包括：

- `PVM_USER`、`PVM_PROJECT_VERSION`、`PVM_OPERATION_LOG`、`PVM_PROJECT_OPTION`、`PVM_USER_PERMISSION`
- 主键、唯一索引、查询索引、有效版本唯一索引
- 状态、权限、选项类型和操作类型检查约束
- 用户权限级联外键
- 软删除、乐观锁和日期默认值

也可以执行 EF Core 命令：

```powershell
dotnet ef database update `
  --project .\src\ProjectVersionManager.Api `
  --startup-project .\src\ProjectVersionManager.Api `
  --context PvmDbContext
```

迁移源码位于 `src/ProjectVersionManager.Api/Data/Migrations/SqlServer`。

## 开发管理员

开发环境开启 `DevelopmentAdmin:Enabled` 时，API 仅在数据库不存在 `admin` 用户时创建账号，密码使用 ASP.NET Core PasswordHasher 保存，不保存明文。默认开发密码来自 `DevelopmentAdmin:Password`，请在本地配置中修改。

## Oracle 备份说明

原公司 Oracle 建表脚本保留在 [database/OracleBackup/PVM_DBA_COMPLETE.sql](database/OracleBackup/PVM_DBA_COMPLETE.sql)，仅供历史审查和正式环境 DBA 参考。本次 SQL Server 迁移没有执行、删除或修改任何 Oracle 正式数据。
