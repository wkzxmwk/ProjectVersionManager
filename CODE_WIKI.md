# Code Wiki · 项目版本管理系统

> 本文档为 `ProjectVersionManager` 仓库的结构化代码百科，覆盖整体架构、模块职责、关键类与函数、依赖关系、数据库设计及运行方式。
> 文档基于源码现状生成，所有文件引用均可点击跳转。

---

## 目录

1. [项目概述](#1-项目概述)
2. [整体架构](#2-整体架构)
3. [目录结构](#3-目录结构)
4. [后端模块详解（ProjectVersionManager.Api）](#4-后端模块详解projectversionmanagerapi)
   - 4.1 [启动管线 Program.cs](#41-启动管线-programcs)
   - 4.2 [数据访问层（Data / Entities）](#42-数据访问层data--entities)
   - 4.3 [控制器层（Controllers）](#43-控制器层controllers)
   - 4.4 [服务层（Services）](#44-服务层services)
   - 4.5 [基础设施层（Infrastructure）](#45-基础设施层infrastructure)
   - 4.6 [DTO 层（Dtos）](#46-dto-层dtos)
5. [前端模块详解（ProjectVersionManager.Web）](#5-前端模块详解projectversionmanagerweb)
6. [测试模块详解（ProjectVersionManager.Tests）](#6-测试模块详解projectversionmanagertests)
7. [数据库设计](#7-数据库设计)
8. [关键业务机制](#8-关键业务机制)
9. [依赖关系](#9-依赖关系)
10. [API 接口清单](#10-api-接口清单)
11. [项目运行方式](#11-项目运行方式)
12. [构建、测试与部署](#12-构建测试与部署)

---

## 1. 项目概述

**项目版本管理系统**（Project Version Manager，简称 PVM）是一套用于管理软件项目版本生命周期的企业内部 Web 应用，支持：

- 项目版本记录的登记、查询、修改、删除与批量操作；
- 软删除 + 回收站恢复机制；
- 乐观并发控制（`RowVersion`），防止多人同时修改冲突；
- 操作日志审计（记录变更前后数据、操作人、IP、UA）；
- 项目可选项（项目类型 / 开发人 / 项目组 / 程序名 / 编译人）的集中维护与字典下发；
- 人员与权限管理（基于细粒度权限码 + ADMIN 超级角色）；
- Excel 批量导入 / 导出与导入模板下载；
- JWT 登录认证与统一 API 返回 / 异常处理格式。

**技术架构**：前后端分离。

| 维度 | 选型 |
|---|---|
| 前端 | Vue 3.5 + TypeScript + Vite 8 + Element Plus 2.14 + Axios |
| 后端 | ASP.NET Core 8 Web API + EF Core 8（SQL Server Provider） |
| 数据库 | SQL Server（本地开发用 SQL Server Express；历史 Oracle 脚本保留于 `database/OracleBackup/`） |
| 认证 | JWT Bearer + ASP.NET Core Identity 密码哈希 |
| 测试 | xUnit + Coverlet |

---

## 2. 整体架构

### 2.1 分层与请求流

后端采用「轻量分层」结构：Controller 直接依赖 `PvmDbContext`，未拆分独立的 Repository / Application Service / Domain 层（非严格 Clean Architecture）。

```text
浏览器 http://localhost:5173
   │  Vue 3 SPA（单组件 App.vue）
   │  Axios 实例（baseURL=VITE_API_BASE_URL）
   ▼  Authorization: Bearer <JWT>
ASP.NET Core Web API
   │  ExceptionHandlingMiddleware（统一异常→ProblemDetails）
   │  CORS / Authentication / Authorization
   ▼
Controllers  ──►  ProjectOptionCatalog（选项校验）
   │             IJwtTokenService
   ▼
DTO ↔ Entity（MappingExtensions）
   ▼
PvmDbContext（EF Core）
   ▼
SQL Server（PVM_USER / PVM_PROJECT_VERSION / PVM_OPERATION_LOG / PVM_PROJECT_OPTION / PVM_USER_PERMISSION）
```

### 2.2 前后端端口与路由约定

| 角色 | 默认地址 |
|---|---|
| Vue 开发服务器 | `http://localhost:5173`（`--strictPort` 防止端口漂移） |
| 后端 API（http 配置） | `http://localhost:5098`、`https://localhost:7191` |
| Swagger | `http://localhost:5098/swagger` |
| 健康检查 | `http://localhost:5098/health`（`AllowAnonymous`） |

**路由前缀说明**：控制器使用 `[Route("auth")]`、`[Route("project-versions")]` 等相对路由，**不带 `api/` 前缀**。生产部署时 API 作为 IIS 子应用挂在 `/api` 路径下，由 `VITE_API_BASE_URL`（如 `…/api`）补足前缀；本地开发时前端 `VITE_API_BASE_URL` 直接指向 `http://localhost:5098`（或 `https://localhost:7191`），故实际路径为 `/auth/login` 等。Swagger 端点也以相对路径 `./v1/swagger.json` 注册以适配子应用部署。

### 2.3 认证与授权模型

- 登录成功后，`JwtTokenService` 签发 JWT，Claims 含 `sub`(用户Id)、`name`(工号)、`display_name`、`role`、`permission`（每个权限码一条 Claim）。
- `Program.cs` 为 `SystemPermissions.All` 中每个权限码注册同名 Authorization Policy，策略断言：`ADMIN` 角色 **或** 持有对应 `permission` Claim。即 **ADMIN 拥有全部权限**，普通用户按授权权限码访问。
- 前端将 token 与 `LoginResponse` 存于 `sessionStorage`（键 `pvm_token` / `pvm_user`），Axios 请求拦截器注入 `Authorization` 头。

---

## 3. 目录结构

```text
ProjectVersionManager/
├─ src/
│  ├─ ProjectVersionManager.Api/            # ASP.NET Core 8 后端
│  │  ├─ Controllers/                       # HTTP API（6 个控制器）
│  │  ├─ Data/
│  │  │  ├─ PvmDbContext.cs                 # EF Core 上下文 + 实体映射
│  │  │  └─ Migrations/SqlServer/           # EF Core 迁移
│  │  ├─ Dtos/                              # 请求/响应模型（6 文件）
│  │  ├─ Entities/                          # 数据库实体（5 文件）
│  │  ├─ Infrastructure/                    # 异常、中间件、映射、常量
│  │  ├─ Services/                          # JWT、DB 初始化、导入解析、选项校验
│  │  ├─ Properties/                        # launchSettings + 发布配置
│  │  ├─ Program.cs                          # 应用入口 + 服务注册管线
│  │  ├─ appsettings.json / .Development.json
│  │  └─ ProjectVersionManager.Api.csproj
│  └─ ProjectVersionManager.Web/            # Vue 3 前端
│     ├─ src/
│     │  ├─ App.vue                          # 单组件应用（登录 + 业务主界面）
│     │  ├─ api.ts                           # Axios 封装 + 接口定义
│     │  ├─ types.ts                         # TypeScript 类型
│     │  ├─ main.ts                          # 应用入口
│     │  └─ style.css                        # 全局样式
│     ├─ .env.development / .production / .example
│     ├─ vite.config.ts
│     └─ package.json
├─ tests/
│  └─ ProjectVersionManager.Tests/          # xUnit 单元测试
├─ database/
│  ├─ SqlServer_Initial.sql                 # SQL Server 初始建库脚本
│  └─ OracleBackup/PVM_DBA_COMPLETE.sql     # 历史 Oracle 脚本（已停用）
├─ *.md                                     # 各类指南文档
├─ global.json                              # .NET SDK 版本固定
└─ ProjectVersionManager.sln                # VS 解决方案（仅含 Api + Tests）
```

> 注意：Vue 前端不是 `.csproj`，未被纳入 `ProjectVersionManager.sln`。Visual Studio F5 仅启动后端 API 并打开 Swagger，前端需独立 `npm run dev` 启动。

---

## 4. 后端模块详解（ProjectVersionManager.Api）

### 4.1 启动管线 [Program.cs](file:///d:/CodeALL/WebProgram/ProjectVersionManager/src/ProjectVersionManager.Api/Program.cs)

`Program.cs`（顶级语句）完成全部服务注册与中间件装配，关键步骤：

1. **Swagger**：`AddSwaggerGen` 注册 `v1` 文档，配置 `Bearer` JWT 安全方案。
2. **数据库**：从 `ConnectionStrings:DefaultConnection` 读取连接串（缺失则抛异常），`AddDbContext<PvmDbContext>` 使用 `UseSqlServer`。
3. **JWT 配置**：`Configure<JwtOptions>` 绑定 `Jwt` 节；`AddAuthentication().AddJwtBearer()` 设置 `TokenValidationParameters`（校验签发者/受众/生命周期/签名密钥，`ClockSkew=1min`）。
4. **授权策略**：遍历 `SystemPermissions.All`，为每个权限码 `AddPolicy`，断言 `ADMIN` 角色 OR 持有该权限 Claim。
5. **服务注册**：
   - `IJwtTokenService` → `JwtTokenService`（Scoped）
   - `ProjectOptionCatalog`、`DatabaseInitializer`、`DatabaseSchemaValidator`（Scoped）
6. **CORS**：策略名 `Frontend`，允许来源来自 `Cors:AllowedOrigins`（默认 `http://localhost:5173`）。
7. **中间件顺序**：`ExceptionHandlingMiddleware` → `Swagger` → `HttpsRedirection`（仅非 Dev）→ `Cors` → `Authentication` → `Authorization` → `MapControllers` → `/health`。
8. **开发环境初始化**：若 `Development:DevelopmentAdmin:Enabled=true`，启动时先 `DatabaseSchemaValidator.ValidateAsync()` 校验表结构，再 `DatabaseInitializer.EnsureDevelopmentAdminAsync()` 创建/修复 `admin` 账号。

`public partial class Program;` 暴露 `Program` 类型以便测试引用。

### 4.2 数据访问层（Data / Entities）

#### [PvmDbContext.cs](file:///d:/CodeALL/WebProgram/ProjectVersionManager/src/ProjectVersionManager.Api/Data/PvmDbContext.cs)

继承 `DbContext`，使用主构造函数注入 `DbContextOptions`。暴露 5 个 `DbSet`：

| DbSet | 实体 | 表名 |
|---|---|---|
| `Users` | `PvmUser` | `PVM_USER` |
| `ProjectVersions` | `ProjectVersion` | `PVM_PROJECT_VERSION` |
| `OperationLogs` | `OperationLog` | `PVM_OPERATION_LOG` |
| `ProjectOptions` | `ProjectOption` | `PVM_PROJECT_OPTION` |
| `UserPermissions` | `PvmUserPermission` | `PVM_USER_PERMISSION` |

`OnModelCreating` 关键约定：
- 表名采用大写下划线（`PVM_*`），列名映射为 `COLUMN_NAME` 形式。
- 时间列 `datetime2`，默认值 `SYSDATETIME()`。
- **CHECK 约束**：角色（`ADMIN/USER`）、状态（`PENDING/COMPILED/PRE_RELEASED/RELEASED/COMMENTED`）、操作类型（`CREATE/UPDATE/DELETE/RESTORE`）、选项类型、权限码、布尔值。
- **乐观并发**：`ProjectVersion.RowVersion` 与 `ProjectOption.RowVersion` 标记 `IsConcurrencyToken()`（bigint，默认 1）。
- **软删除全局过滤器**：`ProjectVersion` 注册 `HasQueryFilter(x => !x.IsDeleted)`，常规查询自动排除已删除记录；需查看回收站时调用 `IgnoreQueryFilters()`。
- **唯一索引**：
  - `PVM_USER.USERNAME` 唯一；
  - `PVM_PROJECT_OPTION` 的 `(OPTION_TYPE, OPTION_VALUE)` 唯一；
  - `PVM_USER_PERMISSION` 的 `(USER_ID, PERMISSION_CODE)` 唯一；
  - `PVM_PROJECT_VERSION` 的 `(PROGRAM_GROUP, PROGRAM_NAME, PROGRAM_VERSION)` 唯一索引 **`UX_PVM_PV_ACTIVE_UQ`**，带过滤条件 `[IS_DELETED]=0`（仅未删除记录唯一，软删后可重建同版本号）。
- **外键**：`PVM_USER_PERMISSION.USER_ID → PVM_USER.ID`，`ON DELETE CASCADE`（删用户级联删权限）。
- 多列查询索引：项目日期、项目类型、开发人、程序名、释放时间、是否删除、操作时间、业务ID 等。

#### 实体（Entities/）

| 实体 | 关键字段 | 职责 |
|---|---|---|
| [PvmUser](file:///d:/CodeALL/WebProgram/ProjectVersionManager/src/ProjectVersionManager.Api/Entities/PvmUser.cs) | `Id/Username/PasswordHash/DisplayName/RoleCode/IsEnabled` | 登录账号；工号即用户名，`DisplayName` 为人员姓名 |
| [PvmUserPermission](file:///d:/CodeALL/WebProgram/ProjectVersionManager/src/ProjectVersionManager.Api/Entities/PvmUserPermission.cs) | `UserId/PermissionCode/CreatedBy` | 用户↔权限码多对多明细 |
| [ProjectVersion](file:///d:/CodeALL/WebProgram/ProjectVersionManager/src/ProjectVersionManager.Api/Entities/ProjectVersion.cs) | 项目/程序/编译/状态/审计字段 + `IsDeleted/RowVersion` | 核心业务记录 |
| [ProjectOption](file:///d:/CodeALL/WebProgram/ProjectVersionManager/src/ProjectVersionManager.Api/Entities/ProjectOption.cs) | `OptionType/OptionValue/ProgramGroup/SortOrder/RowVersion` | 可选项字典（程序名带所属项目组） |
| [OperationLog](file:///d:/CodeALL/WebProgram/ProjectVersionManager/src/ProjectVersionManager.Api/Entities/OperationLog.cs) | `BusinessId/OperationType/Operator/BeforeData/AfterData/IpAddress/UserAgent` | 审计日志 |

所有主键 `Id` 默认 `Guid.NewGuid().ToString()`（36 位字符串）。

### 4.3 控制器层（Controllers）

所有控制器标注 `[ApiController]`，构造函数通过主构造注入依赖。返回值统一用 `ApiResponse<T>` 包装；分页用 `PagedResult<T>`。

#### [AuthController.cs](file:///d:/CodeALL/WebProgram/ProjectVersionManager/src/ProjectVersionManager.Api/Controllers/AuthController.cs) (`/auth`)
注入 `PvmDbContext` + `IJwtTokenService`。
- `POST /auth/login`（`[AllowAnonymous]`）：用户名+密码登录。先用 `Where(...IsEnabled)` 再 `ToListAsync` 后 `SingleOrDefault`（兼容本地 SQL Server），用 `PasswordHasher<PvmUser>` 校验密码哈希（捕获 `FormatException` 提示旧密码需迁移）；ADMIN 直接给 `SystemPermissions.All`，普通用户查 `UserPermissions` 表，签发 JWT。
- `POST /auth/logout`（`[Authorize]`）：无服务端状态，仅返回成功（前端清 token）。
- `POST /auth/change-password`（`[Authorize]`）：校验新密码不等于当前密码，验证旧密码后重新哈希保存，提示重新登录。
- `GET /auth/me`（`[Authorize]`）：从 Claims 回显当前用户名、显示名、角色、权限列表。

#### [ProjectVersionsController.cs](file:///d:/CodeALL/WebProgram/ProjectVersionManager/src/ProjectVersionManager.Api/Controllers/ProjectVersionsController.cs) (`/project-versions`)
核心业务控制器，注入 `PvmDbContext` + `ProjectOptionCatalog`。类级 `[Authorize]`，方法级按 Policy 细分权限。
- `GET` 列表（`VersionView`）：支持 `DeletedOnly`（回收站，`IgnoreQueryFilters`）、关键字、项目类型/开发人/项目组/状态、日期区间筛选；**先 `ToListAsync` 再内存分页**（注释说明为兼容多数据库 provider）。
- `GET /status-counts`（`VersionView`）：按状态分组统计总数。
- `GET /import-template`（`VersionView`）：用 ClosedXML 生成 `.xlsx` 模板，含「项目版本导入」「填写说明」「可选项参考」三个工作表。
- `POST /import`（`VersionImport`）：`[RequestSizeLimit]` 限制 5MB+64KB；校验 `.xlsx`，调 `ProjectVersionImportParser.Parse` 解析，`ProjectOptionCatalog.EnsureValidAsync` 校验选项，内存去重 + 数据库唯一索引双重防重，事务批量插入并写审计日志。
- `GET /{id}`（`VersionView`）：详情。
- `POST`（`VersionCreate`）：校验选项 + 防重 + 事务插入 + 审计。
- `PUT /{id}`（`VersionUpdate`）：比对 `RowVersion` 乐观锁，校验选项 + 防重，更新并 `RowVersion++`，写审计。
- `DELETE /{id}?rowVersion=`（`VersionDelete`）：**软删除**（`IsDeleted=true`），写审计。
- `DELETE /{id}/permanent?rowVersion=`（`[Authorize(Roles="ADMIN")]`）：**物理删除**，仅管理员。
- `POST /batch-delete`、`POST /batch-restore`（`VersionDelete`）：批量软删 / 恢复，校验 `RowVersion` 一致。
- `GET /export`（`VersionView`）：按当前筛选导出 `.xlsx`。

关键私有方法：
- `SaveChangesWithDuplicateHandlingAsync`：捕获 `DbUpdateException`，按错误消息识别 Oracle 唯一索引/约束/必填列冲突（虽已迁 SQL Server，仍保留兼容判断），转换为友好的 `ConflictException` / `RequestValidationException`。
- `BuildLog`：构造 `OperationLog`，含变更前/后 JSON（`JsonSerializer` web 默认）、IP、UA（截断 1000 字符）。
- `EnsureNoDuplicateAsync`：按 `(ProgramGroup, ProgramName, ProgramVersion)` 大小写不敏感查重，冲突时返回已存在的开发人姓名。

#### [OperationLogsController.cs](file:///d:/CodeALL/WebProgram/ProjectVersionManager/src/ProjectVersionManager.Api/Controllers/OperationLogsController.cs) (`/operation-logs`)
类级 `[Authorize(Policy=LogView)]`。`GET` 列表支持操作类型、时间区间、关键字（操作人/业务ID）筛选，按时间倒序，内存分页。

#### [ProjectOptionsController.cs](file:///d:/CodeALL/WebProgram/ProjectVersionManager/src/ProjectVersionManager.Api/Controllers/ProjectOptionsController.cs) (`/project-options`)
类级 `[Authorize]`，方法内手动判断权限（`HasPermission`/`CanManageOptionType`）。
- `GET`：`OPTION_MANAGE` 看全部；仅 `PROGRAM_RELATION_MANAGE` 者只能看 `PROGRAM_GROUP`/`PROGRAM_NAME`。
- `POST` / `PUT /{id}` / `DELETE /{id}`：CRUD，含 `RowVersion` 乐观锁；程序名选项需校验所属项目组存在；重命名项目组时级联更新子程序名的 `ProgramGroup`；删除项目组前校验无关联程序名。
- `POST /import`：Excel 批量导入选项，去重跳过。
- 私有 `NormalizeProgramGroupAsync`：程序名必须关联到已存在的项目组，否则报错。

#### [PersonnelController.cs](file:///d:/CodeALL/WebProgram/ProjectVersionManager/src/ProjectVersionManager.Api/Controllers/PersonnelController.cs) (`/personnel`)
类级 `[Authorize(Policy=PersonnelManage)]`。管理普通用户（`RoleCode=USER`）。
- `GET`：列出所有普通用户及其权限码。
- `POST` / `PUT /{id}`：增改人员，工号正则校验；**新增/改名时自动同步 `ProjectOption` 中的 `DEVELOPER` 与 `COMPILER` 选项**（确保人员名作为开发人/编译人下拉项存在）；重命名时合并或更新旧选项。
- `PUT /{id}/permissions`（`[Authorize(Roles="ADMIN")]`）：仅管理员分配权限；校验权限码合法；版本写权限隐含 `VERSION_VIEW`；替换式更新权限（删旧增新），提示重新登录生效。
- `POST /{id}/reset-password`：重置管理密码。
- `DELETE /{id}`：删除人员，级联清权限；若该姓名无其他用户引用，则清理对应的开发人/编译人选项。

#### [DictionariesController.cs](file:///d:/CodeALL/WebProgram/ProjectVersionManager/src/ProjectVersionManager.Api/Controllers/DictionariesController.cs) (`/dictionaries`)
类级 `[Authorize(Policy=VersionView)]`，提供下拉字典：`/project-types`、`/developers`、`/program-groups`、`/program-names`（可按 `programGroup` 过滤）、`/compilers`。统一从 `ProjectOption` 表读取。

### 4.4 服务层（Services）

| 服务 | 文件 | 职责 |
|---|---|---|
| `IJwtTokenService` / `JwtTokenService` | [JwtTokenService.cs](file:///d:/CodeALL/WebProgram/ProjectVersionManager/src/ProjectVersionManager.Api/Services/JwtTokenService.cs) | 构造 Claims（含权限 Claim），HmacSha256 签名，返回 `LoginResponse` |
| `DatabaseInitializer` | [DatabaseInitializer.cs](file:///d:/CodeALL/WebProgram/ProjectVersionManager/src/ProjectVersionManager.Api/Services/DatabaseInitializer.cs) | Dev 环境确保 `admin` 账号存在；若旧密码非有效哈希则用 `PasswordHasher` 重新哈希并修复 |
| `DatabaseSchemaValidator` | [DatabaseSchemaValidator.cs](file:///d:/CodeALL/WebProgram/ProjectVersionManager/src/ProjectVersionManager.Api/Services/DatabaseSchemaValidator.cs) | Dev 启动时查 `sys.tables/sys.columns` 校验 5 张表与列是否齐全，检查 `CK_PVM_LOG_OP_TYPE` 约束与 `VERSION_DESCRIPTION` 可空性，仅记录日志不阻断启动 |
| `ProjectOptionCatalog` | [ProjectOptionCatalog.cs](file:///d:/CodeALL/WebProgram/ProjectVersionManager/src/ProjectVersionManager.Api/Services/ProjectOptionCatalog.cs) | 保存版本前校验「项目类型/开发人/项目组/程序名/编译人」均来自 `ProjectOption` 有效项，且程序名归属于所选项目组；支持单条与批量（导入）校验 |
| `ProjectVersionImportParser` | [ProjectVersionImportParser.cs](file:///d:/CodeALL/WebProgram/ProjectVersionManager/src/ProjectVersionManager.Api/Services/ProjectVersionImportParser.cs) | 静态类，解析版本导入 Excel：校验表头、最多 1000 行、必填/可选文本、日期多格式、状态中英文映射（待释放/PENDING 等），累积错误一次性抛出 |
| `ProjectOptionImportParser` | [ProjectOptionImportParser.cs](file:///d:/CodeALL/WebProgram/ProjectVersionManager/src/ProjectVersionManager.Api/Services/ProjectOptionImportParser.cs) | 静态类，解析选项导入 Excel：表头「选项分类/选项内容/排序/(所属项目组)」，分类支持中文与英文代码，排序缺省按行号×10 |

### 4.5 基础设施层（Infrastructure）

| 文件 | 内容 |
|---|---|
| [SystemPermissions.cs](file:///d:/CodeALL/WebProgram/ProjectVersionManager/src/ProjectVersionManager.Api/Infrastructure/SystemPermissions.cs) | 权限码常量与 `All` 列表，`ClaimType="permission"`。9 个权限：`VERSION_VIEW/CREATE/UPDATE/DELETE/IMPORT`、`LOG_VIEW`、`OPTION_MANAGE`、`PROGRAM_RELATION_MANAGE`、`PERSONNEL_MANAGE` |
| [ProjectOptionTypes.cs](file:///d:/CodeALL/WebProgram/ProjectVersionManager/src/ProjectVersionManager.Api/Infrastructure/ProjectOptionTypes.cs) | 选项类型常量：`PROJECT_TYPE/DEVELOPER/PROGRAM_GROUP/PROGRAM_NAME/COMPILER` |
| [ApiExceptions.cs](file:///d:/CodeALL/WebProgram/ProjectVersionManager/src/ProjectVersionManager.Api/Infrastructure/ApiExceptions.cs) | 自定义异常：`NotFoundException`、`ConflictException`、`UnauthorizedException`、`RequestValidationException` |
| [ExceptionHandlingMiddleware.cs](file:///d:/CodeALL/WebProgram/ProjectVersionManager/src/ProjectVersionManager.Api/Infrastructure/ExceptionHandlingMiddleware.cs) | 全局异常中间件：将自定义异常映射为 HTTP 状态码 + `ProblemDetails`（`application/problem+json`），含 `traceId`；非 Dev 环境 500 错误隐藏内部消息；记录日志 |
| [JwtOptions.cs](file:///d:/CodeALL/WebProgram/ProjectVersionManager/src/ProjectVersionManager.Api/Infrastructure/JwtOptions.cs) | 强类型配置：`Issuer/Audience/Key/ExpireMinutes(默认480)` |
| [MappingExtensions.cs](file:///d:/CodeALL/WebProgram/ProjectVersionManager/src/ProjectVersionManager.Api/Infrastructure/MappingExtensions.cs) | `ProjectVersion.ToDto()` 实体→DTO；`SaveProjectVersionRequest.Apply(entity)` 请求→实体（trim、`Clean` 空串转 null、状态大写、日期取 `.Date`） |

### 4.6 DTO 层（Dtos）

统一返回壳与分页定义于 [CommonDtos.cs](file:///d:/CodeALL/WebProgram/ProjectVersionManager/src/ProjectVersionManager.Api/Dtos/CommonDtos.cs)：

```csharp
public sealed record ApiResponse<T>(bool Success, string Message, T? Data)
{
    public static ApiResponse<T> Ok(T data, string message = "操作成功") => new(true, message, data);
}
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int Total)
{
    public int TotalPages => (int)Math.Ceiling(Total / (double)PageSize);
}
```

其余 DTO 文件：[AuthDtos.cs](file:///d:/CodeALL/WebProgram/ProjectVersionManager/src/ProjectVersionManager.Api/Dtos/AuthDtos.cs)（`LoginRequest`/`ChangePasswordRequest`/`LoginResponse`）、[ProjectVersionDtos.cs](file:///d:/CodeALL/WebProgram/ProjectVersionManager/src/ProjectVersionManager.Api/Dtos/ProjectVersionDtos.cs)（查询/保存/DTO/状态统计/导入结果/批量请求）、[ProjectOptionDtos.cs](file:///d:/CodeALL/WebProgram/ProjectVersionManager/src/ProjectVersionManager.Api/Dtos/ProjectOptionDtos.cs)、[PersonnelDtos.cs](file:///d:/CodeALL/WebProgram/ProjectVersionManager/src/ProjectVersionManager.Api/Dtos/PersonnelDtos.cs)、[OperationLogDtos.cs](file:///d:/CodeALL/WebProgram/ProjectVersionManager/src/ProjectVersionManager.Api/Dtos/OperationLogDtos.cs)。DTO 大量使用 `[Required]`、`[StringLength]`、`[RegularExpression]`、`[Range]` 做模型校验。

---

## 5. 前端模块详解（ProjectVersionManager.Web）

### 5.1 技术栈与依赖

参见 [package.json](file:///d:/CodeALL/WebProgram/ProjectVersionManager/src/ProjectVersionManager.Web/package.json)。运行时依赖：`vue ^3.5.39`、`element-plus ^2.14.3`、`@element-plus/icons-vue ^2.3.2`、`axios ^1.19.0`。开发依赖：`vite ^8.1.1`、`typescript ~6.0.2`、`vue-tsc ^3.3.5`、`@vitejs/plugin-vue ^6.0.7`、`@vue/tsconfig ^0.9.1`、`@types/node ^24.13.2`。

**架构特点**：无 Vue Router、无 Pinia/Vuex，整个应用为**单组件架构**（[App.vue](file:///d:/CodeALL/WebProgram/ProjectVersionManager/src/ProjectVersionManager.Web/src/App.vue) 约 1595 行），视图切换由 `activeView` ref 驱动。

### 5.2 [vite.config.ts](file:///d:/CodeALL/WebProgram/ProjectVersionManager/src/ProjectVersionManager.Web/vite.config.ts)
仅注册 `vue()` 插件，**未配置** 端口、`base`、`server.proxy`、`alias`。因此前端直连后端，跨域依赖后端 CORS 配合。默认端口通过命令行 `--port 5173 --strictPort` 指定。

### 5.3 [main.ts](file:///d:/CodeALL/WebProgram/ProjectVersionManager/src/ProjectVersionManager.Web/src/main.ts)
`createApp(App).use(ElementPlus).mount('#app')`，全量引入 Element Plus 及其 CSS，再引入项目 `style.css`。图标按需在 `App.vue` 具名导入，未全局注册。

### 5.4 [App.vue](file:///d:/CodeALL/WebProgram/ProjectVersionManager/src/ProjectVersionManager.Web/src/App.vue)（核心）
`<script setup lang="ts">`，集中管理全部状态与逻辑。

**视图（`activeView`）**：
| 视图 | 说明 | 鉴权 |
|---|---|---|
| 登录页（`!isLoggedIn`） | 品牌+登录卡，演示账号 `admin/admin123` | 无 |
| `records` | 状态统计卡 + 筛选 + 操作面板 + 表格 + 分页 + 回收站 | `VERSION_VIEW` |
| `logs` | 操作日志审计表，变更前后 JSON | `LOG_VIEW` |
| `settings` | 项目选项管理（左分类右表格） | `OPTION_MANAGE` 或 `PROGRAM_RELATION_MANAGE` |
| `personnel` | 人员 CRUD + 权限分配（管理员） | `PERSONNEL_MANAGE` |
| `no-access` | 无权限占位 | — |

**关键状态**：`token`/`user`（持久化于 `sessionStorage` 的 `pvm_token`/`pvm_user`）、`records`/`statusCounts`/`total`/`currentPage`/`pageSize`(默认20)/`selectedRecords`/`showDeleted`/`filters`、`form`/`editingId`/`detailRecord`、`logs`/`logFilters`、`options`/`projectOptions`/`selectedOptionType`、`personnel`/`personnelForm`/`permissionDialogVisible`、各类弹窗显隐 ref。

**权限处理**：`isAdmin = user.roleCode === 'ADMIN'`；`hasPermission(p) = isAdmin || permissions.includes(p)`；`canAccessView` / `firstAccessibleView` 决定登录后默认视图；非管理员默认将 `filters.developer` 设为本人姓名；仅有 `PROGRAM_RELATION_MANAGE` 者在设置视图只看到项目组/程序名两类。

**全局 401 处理**：响应拦截器在 401 时 `dispatchEvent('pvm:unauthorized')`，`App.vue` 监听后清空登录态，由 `isLoggedIn` 计算属性自然切回登录页。

### 5.5 [api.ts](file:///d:/CodeALL/WebProgram/ProjectVersionManager/src/ProjectVersionManager.Web/src/api.ts)
- `axios.create({ baseURL: import.meta.env.VITE_API_BASE_URL, timeout: 20000 })`。
- **请求拦截器**：从 `sessionStorage.pvm_token` 注入 `Authorization: Bearer`。
- **响应拦截器**：401 清 token + 派发 `pvm:unauthorized` 事件。
- `unwrap<T>`：统一从 `ApiResponse<T>` 取 `data.data` 解包；blob 类接口保留原始 response。
- 按域导出：`authApi`、`versionsApi`、`dictionariesApi`、`projectOptionsApi`、`personnelApi`、`logsApi`，以及 `getErrorMessage`（从 axios 错误提取 `message/detail/traceId`，支持 ASP.NET `errors` 字典）。
- 类型 `VersionQuery`/`LogQuery`。

### 5.6 [types.ts](file:///d:/CodeALL/WebProgram/ProjectVersionManager/src/ProjectVersionManager.Web/src/types.ts)
定义 `ApiResponse<T>`、`PagedResult<T>`、`LoginResponse`、`ProjectVersion`、`ProjectVersionForm`、`ProjectVersionStatusCounts`、`ProjectVersionImportResult`、`Personnel`、`OperationLog`、`ProjectOptionType`、`ProjectOption`、`ProjectOptionForm` 等。

### 5.7 环境变量
仅 `VITE_API_BASE_URL`：`.env.development=https://localhost:7191`、`.env.production=https://10.68.200.112:5098/api`、`.env.example=http://localhost:5098/api`。

---

## 6. 测试模块详解（ProjectVersionManager.Tests）

参见 [ProjectVersionManager.Tests.csproj](file:///d:/CodeALL/WebProgram/ProjectVersionManager/tests/ProjectVersionManager.Tests/ProjectVersionManager.Tests.csproj)：`net8.0`，引用 `xunit 2.5.3`、`Microsoft.NET.Test.Sdk 17.8.0`、`coverlet.collector 6.0.0`，全局 `using Xunit`，并引用 API 项目。

测试为**纯单元测试**（不连数据库），聚焦校验与映射逻辑：

| 测试文件 | 覆盖点 |
|---|---|
| [ProjectVersionValidationTests.cs](file:///d:/CodeALL/WebProgram/ProjectVersionManager/tests/ProjectVersionManager.Tests/ProjectVersionValidationTests.cs) | `Apply` 规范化（trim/状态大写/日期取整）、状态枚举校验、批量请求至少 1 条、`RowVersion>0`、拒绝已废弃的 `ARCHIVED` 状态 |
| [JwtTokenServiceTests.cs](file:///d:/CodeALL/WebProgram/ProjectVersionManager/tests/ProjectVersionManager.Tests/JwtTokenServiceTests.cs) | JWT Claims 含身份/角色/权限；`DisplayName` 缺失回退工号 |
| [ProjectOptionImportParserTests.cs](file:///d:/CodeALL/WebProgram/ProjectVersionManager/tests/ProjectVersionManager.Tests/ProjectOptionImportParserTests.cs) | 选项导入：中英文分类、缺省排序、第 4 列程序名所属项目组、表头错误不返回部分数据 |
| [ProjectVersionImportParserTests.cs](file:///d:/CodeALL/WebProgram/ProjectVersionManager/tests/ProjectVersionManager.Tests/ProjectVersionImportParserTests.cs) | 版本导入解析 |
| [AuthValidationTests.cs](file:///d:/CodeALL/WebProgram/ProjectVersionManager/tests/ProjectVersionManager.Tests/AuthValidationTests.cs) | 登录/改密请求校验 |
| [PersonnelPermissionValidationTests.cs](file:///d:/CodeALL/WebProgram/ProjectVersionManager/tests/ProjectVersionManager.Tests/PersonnelPermissionValidationTests.cs) | 人员/权限码校验 |
| [ProjectOptionValidationTests.cs](file:///d:/CodeALL/WebProgram/ProjectVersionManager/tests/ProjectVersionManager.Tests/ProjectOptionValidationTests.cs) | 选项请求校验 |

---

## 7. 数据库设计

初始建库脚本：[database/SqlServer_Initial.sql](file:///d:/CodeALL/WebProgram/ProjectVersionManager/database/SqlServer_Initial.sql)。5 张表，主键统一为 `nvarchar(36)` GUID 字符串。

| 表 | 用途 | 关键约束/索引 |
|---|---|---|
| `PVM_USER` | 登录账号 | `USERNAME` 唯一；`ROLE_CODE ∈ (ADMIN,USER)`；`IS_ENABLED ∈ (0,1)` |
| `PVM_USER_PERMISSION` | 用户权限明细 | `(USER_ID,PERMISSION_CODE)` 唯一；`FK→PVM_USER` 级联删；权限码 CHECK（9 种） |
| `PVM_PROJECT_VERSION` | 版本记录（核心） | `STATUS` CHECK（5 态）；`IS_DELETED` CHECK；`(PROGRAM_GROUP,PROGRAM_NAME,PROGRAM_VERSION)` 唯一过滤索引 `UX_PVM_PV_ACTIVE_UQ`（仅 `IS_DELETED=0`）；多查询索引 |
| `PVM_PROJECT_OPTION` | 可选项字典 | `(OPTION_TYPE,OPTION_VALUE)` 唯一；`OPTION_TYPE` CHECK（5 种）；`(OPTION_TYPE,PROGRAM_GROUP)` 索引 |
| `PVM_OPERATION_LOG` | 审计日志 | `OPERATION_TYPE ∈ (CREATE,UPDATE,DELETE,RESTORE)`；`OPERATION_TIME`/`BUSINESS_ID` 索引 |

历史 Oracle 脚本 [database/OracleBackup/PVM_DBA_COMPLETE.sql](file:///d:/CodeALL/WebProgram/ProjectVersionManager/database/OracleBackup/PVM_DBA_COMPLETE.sql) 已停用，本地 SQL Server 迁移不会连接公司 Oracle。EF Core 迁移位于 [Data/Migrations/SqlServer/](file:///d:/CodeALL/WebProgram/ProjectVersionManager/src/ProjectVersionManager.Api/Data/Migrations/SqlServer/)（迁移 `20260801141018_InitialSqlServer`）。

---

## 8. 关键业务机制

1. **统一响应格式**：所有业务接口返回 `ApiResponse<T>{ success, message, data }`；异常经 `ExceptionHandlingMiddleware` 转 `ProblemDetails`，含 `traceId`。
2. **JWT + 细粒度权限**：登录签发含权限 Claim 的 JWT；策略断言 `ADMIN` OR 权限 Claim；前端按权限码门控 UI（按钮/列/导航/整段面板）。
3. **软删除 + 回收站**：`ProjectVersion.IsDeleted` + 全局查询过滤器；`DeletedOnly` 切换查看回收站；`batch-restore` 恢复；`permanent` 物理删仅 ADMIN。唯一索引带 `IS_DELETED=0` 过滤，软删后可重建同版本号。
4. **乐观并发控制**：`ProjectVersion` / `ProjectOption` 的 `RowVersion` 作为 `IsConcurrencyToken`；前端表单与删除请求携带 `rowVersion`，后端比对不一致抛 `ConflictException`。
5. **审计日志**：版本增改删恢复均写 `PVM_OPERATION_LOG`，记录变更前/后 JSON、操作人、IP、UA（截断 1000）。
6. **Excel 导入/导出**：模板下载（含说明与可选项参考表）；导入解析支持中英文状态/分类、多日期格式、最多 1000 行；导入前用 `ProjectOptionCatalog` 校验选项合法性；导入内存去重 + 数据库唯一索引双保险。
7. **人员↔字典联动**：人员增改删自动同步 `DEVELOPER`/`COMPILER` 选项，保证下拉一致；重命名项目组级联更新子程序名归属。
8. **开发环境便利**：`DatabaseSchemaValidator` 校验表结构、`DatabaseInitializer` 创建 `admin/admin123`，旧明文/无效哈希密码自动迁移为 ASP.NET Core 密码哈希。

---

## 9. 依赖关系

### 9.1 后端 NuGet（[ProjectVersionManager.Api.csproj](file:///d:/CodeALL/WebProgram/ProjectVersionManager/src/ProjectVersionManager.Api/ProjectVersionManager.Api.csproj)）

| 包 | 版本 | 用途 |
|---|---|---|
| `Microsoft.EntityFrameworkCore.SqlServer` | 8.0.11 | EF Core SQL Server Provider |
| `Microsoft.EntityFrameworkCore.Design` | 8.0.11 | 迁移工具（PrivateAssets=all） |
| `Microsoft.AspNetCore.Authentication.JwtBearer` | 8.0.29 | JWT 认证 |
| `Swashbuckle.AspNetCore` | 6.6.2 | Swagger/OpenAPI |
| `ClosedXML` | 0.105.0 | Excel 导入/导出/模板 |

SDK：`net8.0`，启用 `Nullable` 与 `ImplicitUsings`，`UserSecretsId=ProjectVersionManager-Api-Local`。`global.json` 固定 SDK `8.0.417`（`rollForward: latestPatch`）。

### 9.2 前端 npm
见 [5.1](#51-技术栈与依赖)。

### 9.3 项目引用
`Tests → Api`（`ProjectReference`）。前端独立，无项目引用。

### 9.4 模块依赖图
```text
Controllers ──► PvmDbContext ──► EF Core ──► SQL Server
   ├──► ProjectOptionCatalog ──► PvmDbContext
   ├──► IJwtTokenService ──► JwtOptions
   └──► MappingExtensions (Entity↔DTO)
Infrastructure: ExceptionHandlingMiddleware / ApiExceptions / SystemPermissions / ProjectOptionTypes
Program.cs 装配: Db + JWT + Authz + CORS + Swagger + Middleware + Dev 初始化
```

---

## 10. API 接口清单

> 方法级鉴权：`[Authorize(Policy=...)]` 或 `[Authorize(Roles="ADMIN")]`；未注明者为类级 `[Authorize]`。

### 鉴权 `/auth`
| 方法 | 路径 | 鉴权 | 说明 |
|---|---|---|---|
| POST | `/auth/login` | AllowAnonymous | 登录，返回 JWT |
| POST | `/auth/logout` | Authorize | 退出（前端清 token） |
| POST | `/auth/change-password` | Authorize | 修改密码 |
| GET | `/auth/me` | Authorize | 当前用户信息 |

### 项目版本 `/project-versions`（类级 Authorize）
| 方法 | 路径 | 权限 | 说明 |
|---|---|---|---|
| GET | `/project-versions` | VERSION_VIEW | 分页查询/回收站 |
| GET | `/project-versions/status-counts` | VERSION_VIEW | 状态统计 |
| GET | `/project-versions/import-template` | VERSION_VIEW | 下载导入模板 |
| POST | `/project-versions/import` | VERSION_IMPORT | Excel 导入 |
| GET | `/project-versions/{id}` | VERSION_VIEW | 详情 |
| POST | `/project-versions` | VERSION_CREATE | 新增 |
| PUT | `/project-versions/{id}` | VERSION_UPDATE | 修改 |
| DELETE | `/project-versions/{id}?rowVersion=` | VERSION_DELETE | 软删除 |
| DELETE | `/project-versions/{id}/permanent?rowVersion=` | ADMIN | 物理删除 |
| POST | `/project-versions/batch-delete` | VERSION_DELETE | 批量软删 |
| POST | `/project-versions/batch-restore` | VERSION_DELETE | 批量恢复 |
| GET | `/project-versions/export` | VERSION_VIEW | 导出 Excel |

### 操作日志 `/operation-logs`（类级 LogView）
| 方法 | 路径 | 说明 |
|---|---|---|
| GET | `/operation-logs` | 分页查询审计日志 |

### 项目选项 `/project-options`（类级 Authorize，方法内细判权限）
| 方法 | 路径 | 说明 |
|---|---|---|
| GET | `/project-options` | 列表（按权限可见范围） |
| POST | `/project-options` | 新增选项 |
| POST | `/project-options/import` | Excel 导入选项 |
| PUT | `/project-options/{id}` | 修改 |
| DELETE | `/project-options/{id}?rowVersion=` | 删除 |

### 人员 `/personnel`（类级 PersonnelManage）
| 方法 | 路径 | 权限 | 说明 |
|---|---|---|---|
| GET | `/personnel` | PersonnelManage | 列表 |
| POST | `/personnel` | PersonnelManage | 新增 |
| PUT | `/personnel/{id}` | PersonnelManage | 修改 |
| PUT | `/personnel/{id}/permissions` | ADMIN | 分配权限 |
| POST | `/personnel/{id}/reset-password` | PersonnelManage | 重置密码 |
| DELETE | `/personnel/{id}` | PersonnelManage | 删除 |

### 字典 `/dictionaries`（类级 VersionView）
| 方法 | 路径 | 说明 |
|---|---|---|
| GET | `/dictionaries/project-types` | 项目类型 |
| GET | `/dictionaries/developers` | 开发人 |
| GET | `/dictionaries/program-groups` | 项目组 |
| GET | `/dictionaries/program-names?programGroup=` | 程序名（可过滤） |
| GET | `/dictionaries/compilers` | 编译人 |

### 其他
| 方法 | 路径 | 鉴权 | 说明 |
|---|---|---|---|
| GET | `/health` | AllowAnonymous | 健康检查 |
| GET | `/swagger` | — | Swagger UI |

---

## 11. 项目运行方式

### 11.1 前置条件
- .NET SDK 8.0.417（见 [global.json](file:///d:/CodeALL/WebProgram/ProjectVersionManager/global.json)）
- Node.js（支持 Vite 8）
- SQL Server Express（本地开发，Windows 身份验证）

### 11.2 数据库准备
开发连接串（[appsettings.Development.json](file:///d:/CodeALL/WebProgram/ProjectVersionManager/src/ProjectVersionManager.Api/appsettings.Development.json)）：

```text
Server=.\SQLEXPRESS;Database=ProjectVersionManager_Dev;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True
```

首次建库二选一：
- 执行 [database/SqlServer_Initial.sql](file:///d:/CodeALL/WebProgram/ProjectVersionManager/database/SqlServer_Initial.sql)
- 或 `dotnet ef database update --project .\src\ProjectVersionManager.Api --startup-project .\src\ProjectVersionManager.Api`

`DevelopmentAdmin.Enabled=true`（Dev）启动时自动创建管理员 `admin`（密码见 `DevelopmentAdmin:Password`，默认 `admin123`）。生产环境请通过 User Secrets 配置：

```powershell
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "<连接串>" `
  --project .\src\ProjectVersionManager.Api\ProjectVersionManager.Api.csproj
```

> 生产 `Jwt:Key` 必须替换为至少 32 字符的安全密钥。

### 11.3 启动后端
```powershell
dotnet restore .\ProjectVersionManager.sln
dotnet run --project .\src\ProjectVersionManager.Api
```
或 Visual Studio 打开 `ProjectVersionManager.sln`，将 `ProjectVersionManager.Api` 设为启动项目，选 `http`/`https` 配置 F5。验证 `http://localhost:5098/swagger` 与 `/health`。

### 11.4 启动前端
```powershell
cd .\src\ProjectVersionManager.Web
npm install
npm run dev -- --port 5173 --strictPort
```
浏览器打开 `http://localhost:5173`。`--strictPort` 防止端口漂移导致 CORS 失效。前端默认请求 `http://localhost:5098`（见 `.env.example`），可通过 `.env` 覆盖 `VITE_API_BASE_URL`。

### 11.5 调试
- 后端断点：Controllers / Services / `ExceptionHandlingMiddleware`。
- 前端断点：浏览器 F12 → Sources → `App.vue` / `api.ts`。Vite 支持热更新。

---

## 12. 构建、测试与部署

### 12.1 构建
```powershell
# 后端
dotnet build ProjectVersionManager.sln

# 前端（先 vue-tsc 类型检查再 vite build，输出至 dist/）
cd .\src\ProjectVersionManager.Web
npm run build
```

### 12.2 测试
```powershell
dotnet test ProjectVersionManager.sln
```
（Coverlet 收集覆盖率）

### 12.3 部署
- 发布配置见 [Properties/PublishProfiles/FolderProfile.pubxml](file:///d:/CodeALL/WebProgram/ProjectVersionManager/src/ProjectVersionManager.Api/Properties/PublishProfiles/FolderProfile.pubxml)：文件系统发布至 `bin\Release\net8.0\publish\`。
- 生产部署 SQL Server 细节见 [PRODUCTION_DEPLOYMENT_SQLSERVER.md](file:///d:/CodeALL/WebProgram/ProjectVersionManager/PRODUCTION_DEPLOYMENT_SQLSERVER.md) 与 [DEPLOYMENT_GUIDE.md](file:///d:/CodeALL/WebProgram/ProjectVersionManager/DEPLOYMENT_GUIDE.md)。
- API 作为 IIS 子应用挂在 `/api` 下，Swagger 端点以相对路径 `./v1/swagger.json` 适配。
- 前端构建产物 `dist/` 由 Web 服务器托管，`VITE_API_BASE_URL` 指向生产 API 地址。
