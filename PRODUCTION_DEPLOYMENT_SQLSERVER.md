# 正式环境发布步骤（SQL Server）

本文适用于正式环境部署：

```text
Vue 3 静态文件 → ASP.NET Core 8 API → 正式 SQL Server
```

本地开发库 `ProjectVersionManager_Dev` 与正式库必须分开。正式环境不能使用 `appsettings.Development.json`、`admin123` 或本地 `Trusted_Connection` 配置。

## 1. 发布前准备

1. 确认正式 SQL Server 实例、数据库名称、备份策略、字符集/排序规则和 API 服务器网络访问权限。
2. 由 DBA 备份目标数据库；首次部署应确认目标数据库中不存在同名业务表，或先制定数据迁移方案。
3. 准备一个正式服务账号。推荐使用 Windows 域账号；数据库结构由 DBA 执行，API 账号只授予业务表读写权限，不授予 `db_owner`。
4. 准备至少 32 个字符的随机 JWT 密钥，不能使用仓库中的示例值。

## 2. 创建正式数据库

由 DBA 在正式 SQL Server 的 `master` 数据库执行，数据库名称按正式规范替换：

```sql
IF DB_ID(N'ProjectVersionManager') IS NULL
BEGIN
    CREATE DATABASE [ProjectVersionManager];
END;
GO
```

如果使用 Windows 服务账号，可以按实际域名替换以下示例：

```sql
USE [ProjectVersionManager];
GO
CREATE USER [DOMAIN\pvm-api] FOR LOGIN [DOMAIN\pvm-api];
GO
ALTER ROLE [db_datareader] ADD MEMBER [DOMAIN\pvm-api];
ALTER ROLE [db_datawriter] ADD MEMBER [DOMAIN\pvm-api];
GO
```

数据库结构由 DBA 执行审查后的 [database/SqlServer_Initial.sql](database/SqlServer_Initial.sql)，执行目标必须是正式数据库：

```sql
USE [ProjectVersionManager];
GO
-- 在 SSMS 中打开 database/SqlServer_Initial.sql 后执行
```

该脚本创建 `PVM_USER`、`PVM_PROJECT_VERSION`、`PVM_OPERATION_LOG`、`PVM_PROJECT_OPTION`、`PVM_USER_PERMISSION` 和 `__EFMigrationsHistory`，同时创建主键、唯一索引、查询索引、检查约束、默认值、软删除字段、乐观锁字段和权限级联外键。

正式环境不建议由 API 进程自动执行 DDL；后续版本升级应由 DBA 审查 EF 生成的幂等脚本后执行：

```powershell
dotnet ef migrations script --idempotent `
  --project .\src\ProjectVersionManager.Api `
  --startup-project .\src\ProjectVersionManager.Api `
  --context PvmDbContext `
  --output .\database\SqlServer_Production_Idempotent.sql
```

## 3. 正式管理员初始化

当前程序只在 `Development` 环境且 `DevelopmentAdmin:Enabled=true` 时自动创建 `admin`。正式环境默认关闭该功能，因此首次发布前必须由受控的管理员初始化流程创建管理员账号，并确认 `PASSWORD_HASH` 使用 ASP.NET Core `PasswordHasher` 生成，禁止写入明文密码。

如果正式环境暂时没有现成的受控初始化工具，请不要直接把 `admin123` 写入数据库；应先由开发/运维补充一次性管理员初始化命令或受控运维脚本，再开放正式登录。

## 4. 配置 API 使用正式库

不要修改代码中的连接字符串，也不要把正式密码提交到 Git。推荐在 API 服务器设置环境变量：

```powershell
$env:ConnectionStrings__DefaultConnection = "Server=SQLSERVER01;Database=ProjectVersionManager;Integrated Security=True;TrustServerCertificate=True;MultipleActiveResultSets=True"
$env:Jwt__Key = "替换为随机生成且至少32字符的正式密钥"
$env:Jwt__Issuer = "ProjectVersionManager"
$env:Jwt__Audience = "ProjectVersionManager.Web"
$env:Cors__AllowedOrigins__0 = "https://pvm.example.com"
```

如果使用 `appsettings.Production.json`，该文件应只部署在服务器上并加入忽略列表；核心配置结构如下：

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "正式 SQL Server 连接字符串"
  },
  "DevelopmentAdmin": {
    "Enabled": false
  },
  "Cors": {
    "AllowedOrigins": ["https://pvm.example.com"]
  }
}
```

修改配置后必须重启 API 进程。API 读取的配置键固定为 `ConnectionStrings:DefaultConnection`，不能继续使用旧的 `ConnectionStrings:Oracle`。

## 5. 发布 API

在构建机执行：

```powershell
dotnet restore .\ProjectVersionManager.sln
dotnet build .\ProjectVersionManager.sln -c Release
dotnet test .\ProjectVersionManager.sln -c Release
dotnet publish .\src\ProjectVersionManager.Api -c Release -o .\artifacts\api
```

将 `artifacts/api` 部署到 API 服务器，使用 IIS（ASP.NET Core Hosting Bundle）或 Windows Service 托管。发布后检查：

```text
GET /health                 200
POST /api/auth/login        管理员可登录
GET /api/project-versions  分页查询正常
```

新增、更新、软删除和操作日志在版本记录控制器中使用同一数据库事务；重复版本会返回冲突提示，失败时不会提交版本和对应日志。

## 6. 发布 Vue 前端

Vue 源代码不需要修改字段、路由、接口路径或 JSON 结构。正式 API 地址不同于默认的 `http://localhost:5098/api` 时，只在构建环境设置：

```powershell
$env:VITE_API_BASE_URL = "https://pvm.example.com/api"
cd .\src\ProjectVersionManager.Web
npm ci
npm run build
```

把 `dist` 部署到 IIS 或其他静态 Web 服务器，并确保 HTTPS、SPA fallback 和 API CORS 允许的来源一致。

## 7. 上线验证与回滚

上线后按以下顺序验证：

1. `/health` 返回 200。
2. 管理员登录成功，密码不是明文配置。
3. 项目版本分页和筛选正常。
4. 新增、更新、软删除成功，操作日志数量同步增加。
5. 相同项目组、程序名和版本再次提交时返回冲突，不产生第二条有效记录。
6. 事务失败时版本和日志一起回滚。
7. 普通用户权限、项目设置和人员管理权限正常。

回滚时先停止 API，保留数据库备份和当前发布包，恢复上一个 API/前端包。数据库结构回滚必须由 DBA 根据备份或已审查的反向迁移执行，禁止直接删除正式表。

## 8. 重要安全确认

- 正式环境不使用 `DevelopmentAdmin:Password`。
- 不把正式连接字符串、JWT 密钥或密码提交到仓库。
- 不在正式环境执行 `dotnet ef database update`，除非 DBA 明确授权并确认账号具有 DDL 权限。
- 本地 SQL Server 迁移不会连接或修改公司 Oracle 正式数据；Oracle 历史脚本仅保存在 `database/OracleBackup`。
