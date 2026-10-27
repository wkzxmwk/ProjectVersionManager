# Project Version Manager

项目由 Vue 3 前端、ASP.NET Core 8 Web API 和 SQL Server Express 本地开发数据库组成。

## 本地数据库

开发连接使用 Windows 身份验证：

```text
Server=.\SQLEXPRESS;Database=ProjectVersionManager_Dev;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True
```

首次创建或重建本地数据库时，可审查并执行 [database/SqlServer_Initial.sql](database/SqlServer_Initial.sql)，或使用 EF Core：

```powershell
dotnet ef database update --project .\src\ProjectVersionManager.Api --startup-project .\src\ProjectVersionManager.Api
```

正式 Oracle 脚本未删除，已移至 [database/OracleBackup/PVM_DBA_COMPLETE.sql](database/OracleBackup/PVM_DBA_COMPLETE.sql)，本地 SQL Server 迁移不会连接或修改公司 Oracle 数据库。

## 本地运行

```powershell
dotnet restore .\ProjectVersionManager.sln
dotnet run --project .\src\ProjectVersionManager.Api

cd .\src\ProjectVersionManager.Web
npm install
npm run dev -- --port 5173 --strictPort
```

架构、Visual Studio 调试入口和 API 说明见 [ARCHITECTURE_AND_DEBUGGING.md](ARCHITECTURE_AND_DEBUGGING.md)；本次 SQL Server 迁移记录见 [SQLSERVER_MIGRATION.md](SQLSERVER_MIGRATION.md)。
