# 项目版本管理系统部署指南

本文档适用于当前项目的生产部署，覆盖 Oracle 数据库、ASP.NET Core API、Vue 前端和 IIS 配置。

## 一、部署顺序

推荐按照以下顺序执行：

1. DBA 创建 Oracle 用户/Schema 并准备权限。
2. DBA 执行完整数据库脚本。
3. 检查数据库表、约束和索引。
4. 初始化生产管理员账号。
5. 发布 ASP.NET Core API。
6. 配置 API 的数据库连接、JWT 和 CORS。
7. 构建并发布 Vue 前端。
8. 进行登录、版本保存、项目设置和操作日志验证。

## 二、DBA 数据库准备

### 2.1 创建 Oracle 用户

建议 API 使用的数据库用户就是 `PVM_*` 表的所有者。DBA 需要根据公司规范完成：

- 创建 Oracle 用户/Schema。
- 分配默认表空间和空间配额。
- 授予连接、建表、建索引和修改自身表结构的权限。
- 确保 API 服务器可以访问 Oracle Listener，通常为 `1521` 端口。

执行脚本前可以确认当前账号：

```sql
SELECT USER FROM DUAL;
SELECT SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA') FROM DUAL;
```

不要使用其他业务 Schema 误执行脚本。建议直接连接到拥有 `PVM_*` 表的账号执行。

### 2.2 执行完整数据库脚本

完整数据库脚本位于：

`database/PVM_DBA_COMPLETE.sql`

也可以直接打开：[PVM_DBA_COMPLETE.sql](database/PVM_DBA_COMPLETE.sql)

该脚本包含：

- `PVM_USER`
- `PVM_PROJECT_VERSION`
- `PVM_OPERATION_LOG`
- `PVM_PROJECT_OPTION`
- `PVM_USER_PERMISSION`
- 主键、唯一约束和外键
- 项目版本状态约束
- 操作日志类型约束
- 权限代码约束
- 查询索引
- 有效项目版本重复保护索引
- 数据库字段注释和执行后的检查语句

全新数据库可以直接执行完整脚本。

已有数据的数据库执行前必须：

1. 备份数据库。
2. 执行脚本中的有效版本重复检查。
3. 处理相同项目组、程序名、程序版本的重复有效记录。
4. 再创建唯一索引。

重复判断条件为：

```text
PROGRAM_GROUP + PROGRAM_NAME + PROGRAM_VERSION
```

且 `IS_DELETED = 0`。脚本不会删除业务数据，但 Oracle DDL 会隐式提交，因此不能依赖普通 `ROLLBACK` 撤销建表或约束变更。

该脚本不写入管理员明文密码，也不自动插入项目选项和人员数据。

## 三、初始化生产管理员

当前 API 只在以下条件同时满足时自动创建开发管理员：

```text
ASPNETCORE_ENVIRONMENT=Development
DevelopmentAdmin:Enabled=true
```

生产环境使用 `Production` 时不会自动创建管理员。因此新数据库首次上线前必须完成管理员初始化。

推荐由项目增加独立的一次性管理员初始化命令。当前版本的临时处理方式是：

1. 在受控环境中让 API 临时连接正式数据库。
2. 临时启用 `DevelopmentAdmin`。
3. 设置正式的强密码并启动一次 API。
4. 确认 `PVM_USER` 中已创建 `admin`。
5. 停止 API。
6. 删除临时管理员密码配置。
7. 正式服务器只使用 `Production` 环境。

不要向 `PASSWORD_HASH` 写入明文密码，该字段必须是 ASP.NET Core 密码哈希格式。

## 四、发布 API

### 4.1 服务器要求

Windows Server + IIS 部署 API 时需要：

- IIS
- .NET 8 Hosting Bundle
- IIS ASP.NET Core Module
- API 服务器到 Oracle 的网络访问权限

### 4.2 生成 API 发布目录

在项目根目录执行：

```powershell
dotnet publish `
  .\src\ProjectVersionManager.Api\ProjectVersionManager.Api.csproj `
  -c Release `
  -o .\publish\Api
```

将 `publish\Api` 复制到服务器，例如：

```text
D:\WebSites\ProjectVersionManager\Api
```

### 4.3 IIS 配置

创建 API 网站或应用：

- 物理路径指向 API 发布目录。
- 应用程序池使用 `No Managed Code`。
- 配置正式 HTTPS 证书。
- 给 IIS 应用程序池账号授予发布目录读取和执行权限。
- 环境设置为 `Production`。

## 五、API 生产配置

API 通过 ASP.NET Core 配置系统读取数据库连接，核心配置项如下：

```text
ASPNETCORE_ENVIRONMENT=Production
ConnectionStrings__Oracle=User Id=PVM;Password=真实密码;Data Source=192.168.1.100:1521/ORCL;
Jwt__Key=至少32位以上的随机强密钥
Jwt__Issuer=ProjectVersionManager
Jwt__Audience=ProjectVersionManager.Web
Cors__AllowedOrigins__0=https://pvm.company.local
```

生产环境不要使用 `appsettings.json` 中的默认 JWT 密钥：

```text
CHANGE-THIS-IN-PRODUCTION-AT-LEAST-32-CHARS
```

也不要把真实数据库密码、JWT 密钥提交到 Git、Markdown 文件或前端 `.env` 文件。

CORS 地址只填写前端来源，不要带 `/api`：

```text
正确：https://pvm.company.local
错误：https://pvm.company.local/api
```

配置修改后需要回收 IIS 应用程序池或重启 API 网站。

## 六、发布 Vue 前端

### 6.1 配置 API 地址

当前前端默认地址是开发环境：

```text
http://localhost:5098/api
```

正式构建前设置生产 API 地址：

```powershell
$env:VITE_API_BASE_URL = 'https://pvm-api.company.local/api'
```

### 6.2 构建前端

```powershell
cd .\src\ProjectVersionManager.Web
npm ci
npm run build
```

构建结果位于：

```text
src\ProjectVersionManager.Web\dist
```

将 `dist` 中的文件部署到 IIS 静态网站，例如：

```text
D:\WebSites\ProjectVersionManager\Web
```

建议前端使用独立 IIS 网站根目录。如果部署到 `/pvm/` 等虚拟目录，需要修改 Vite 的 `base` 配置后重新构建，否则静态资源路径可能出现 404。

## 七、部署后验证

### 7.1 API 存活检查

```text
https://API地址/health
```

`/health` 只表示 API 进程存活，不会主动查询 Oracle。

### 7.2 Swagger 检查

```text
https://API地址/swagger
```

### 7.3 业务功能检查

- 管理员登录。
- 项目设置新增、修改、删除选项。
- 人员管理新增人员和分配权限。
- 新增项目版本记录。
- 重复项目版本被正确阻止。
- 软删除、恢复和管理员真实删除正常。
- 操作日志正常生成。
- 批量导入和模板下载正常。
- 浏览器控制台没有 CORS 或接口 404 错误。

## 八、后续修改数据库连接

如果只是修改数据库地址、账号或密码，只需修改服务器上的：

```text
ConnectionStrings__Oracle
```

然后回收 API 应用程序池，再通过登录或版本列表接口验证。不需要重新构建 API，也不需要重新构建前端，除非 API 的访问地址发生变化。

如果 API 地址发生变化：

1. 修改 `VITE_API_BASE_URL`。
2. 重新执行 `npm run build`。
3. 重新发布前端 `dist`。
4. 修改 `Cors__AllowedOrigins__0`。
5. 回收 API 应用程序池。

## 九、后续修改数据库结构

生产环境不要依赖 API 自动建表或 EF Core 自动迁移。

推荐流程：

1. 修改实体和数据库设计。
2. 更新 `database/PVM_DBA_COMPLETE.sql` 作为完整结构基线。
3. 为已上线数据库准备本次变更 SQL。
4. DBA 审核并备份数据库。
5. DBA 在维护窗口执行变更 SQL。
6. 发布对应版本的 API。
7. 验证表结构、约束、接口和操作日志。

`PVM_DBA_COMPLETE.sql` 主要用于首次安装和结构核对，不需要在每次普通 API 发布时重复执行。

## 十、当前部署注意事项

- API 的数据库连接只能配置在后端，不能放到 Vue 前端。
- 生产环境必须替换默认 JWT 密钥。
- 正式环境不要启用 `DevelopmentAdmin`。
- 正式环境不要使用开发数据库账号和开发密码。
- Swagger 当前由 API 注册，若正式系统不希望对外开放，应在生产环境增加访问限制或关闭。
- 测试项目不会进入 API 发布目录，部署时不需要复制 `tests` 文件夹。
