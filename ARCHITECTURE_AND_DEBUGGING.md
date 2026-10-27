# 项目架构与本地调试指南

## 1. 项目概述

本项目采用前后端分离架构：

- 前端使用 Vue 3 构建单页应用。
- 后端使用 ASP.NET Core 8 Web API 提供数据接口。
- 后端通过 Entity Framework Core 访问 Oracle 数据库。
- 前端和后端在开发阶段分别运行在不同端口。

```text
浏览器：http://localhost:5173
        │
        ▼
Vue 3 + Element Plus
        │
        │ Axios + JWT
        ▼
ASP.NET Core Web API：http://localhost:5098/api
        │
        ▼
Entity Framework Core
        │
        ▼
Oracle 数据库
```

Swagger 接口调试页面：

```text
http://localhost:5098/swagger
```

## 2. 目录结构

```text
ProjectVersionManager
├─ src
│  ├─ ProjectVersionManager.Api    # ASP.NET Core 后端
│  └─ ProjectVersionManager.Web    # Vue 3 前端
├─ tests
│  └─ ProjectVersionManager.Tests  # 后端单元测试
├─ ProjectVersionManager.sln       # Visual Studio 解决方案
└─ database
   └─ PVM_DBA_COMPLETE.sql         # DBA 完整建表/升级脚本
```

### 2.1 正式前端

正式前端位于：

```text
src/ProjectVersionManager.Web
```

主要入口文件：

```text
index.html
    ↓
src/main.ts
    ↓
src/App.vue
    ↓
src/api.ts 调用后端接口
```

主要文件说明：

| 文件 | 作用 |
|---|---|
| `src/ProjectVersionManager.Web/index.html` | 前端 HTML 入口，提供 `#app` 挂载节点 |
| `src/ProjectVersionManager.Web/src/main.ts` | 创建 Vue 应用并加载 Element Plus |
| `src/ProjectVersionManager.Web/src/App.vue` | 登录页和主要业务界面 |
| `src/ProjectVersionManager.Web/src/api.ts` | Axios 实例和后端接口封装 |
| `src/ProjectVersionManager.Web/src/types.ts` | TypeScript 数据类型 |
| `src/ProjectVersionManager.Web/src/style.css` | 全局界面样式 |

### 2.2 后端 API

后端位于：

```text
src/ProjectVersionManager.Api
```

主要目录说明：

| 目录 | 作用 |
|---|---|
| `Controllers` | HTTP API 接口 |
| `Dtos` | 接口请求和响应模型 |
| `Entities` | 数据库实体 |
| `Data` | EF Core 数据库上下文 |
| `Services` | JWT、数据库初始化等服务 |
| `Infrastructure` | 异常处理、映射及公共基础设施 |

当前 API 是一种相对简单的分层结构：

```text
Controllers
    ↓
DTO / Entity
    ↓
PvmDbContext
    ↓
Oracle
```

它还不属于严格的 Clean Architecture，因为 Controller 会直接使用
`PvmDbContext`，目前没有独立拆分 Repository、Application Service 和
Domain 层。

## 3. 使用的主要技术

### 3.1 前端技术

- Vue 3：构建单页应用。
- TypeScript：提供静态类型检查。
- Vite：开发服务器、热更新和生产构建。
- Element Plus：表格、表单、分页、抽屉、弹窗等 UI 组件。
- Axios：调用 ASP.NET Core API。
- CSS：自定义主题、布局和响应式界面。

相关依赖定义在：

```text
src/ProjectVersionManager.Web/package.json
```

### 3.2 后端技术

- .NET 8。
- ASP.NET Core Web API。
- Entity Framework Core。
- Oracle Entity Framework Core Provider。
- Oracle 19c SQL 兼容模式。
- JWT Bearer 身份认证。
- 基于角色的权限控制。
- Swagger/OpenAPI。
- ClosedXML Excel 导出。
- 自定义异常处理中间件。
- xUnit 单元测试。
- Coverlet 测试覆盖率收集。

### 3.3 当前实现的业务机制

- JWT 登录认证。
- ADMIN 管理员角色权限。
- 项目版本分页和组合筛选。
- 新增、修改、删除和批量操作。
- 软删除和回收站恢复。
- `RowVersion` 乐观并发控制。
- 操作日志审计。
- Excel 导出。
- 统一 API 返回格式。
- 统一异常处理。

## 4. 与传统 Web 项目的区别

本项目仍然是 Web 应用，只是采用了现代的前后端分离模式。与传统的
ASP.NET MVC 或 Razor Pages 相比，主要区别如下：

| 对比项 | 当前项目 | 传统 ASP.NET MVC/Razor |
|---|---|---|
| 页面生成 | Vue 在浏览器中渲染 | ASP.NET 在服务端生成 HTML |
| 前端启动 | 需要单独启动 Vite | 启动 ASP.NET 通常即可 |
| 后端输出 | 主要返回 JSON | 通常直接返回 HTML |
| 页面切换 | 单页应用内部切换 | 浏览器请求新的页面 |
| 登录状态 | JWT + SessionStorage | 常见为 Cookie + Session |
| 开发端口 | 前端 5173、后端 5098 | 通常只有一个站点端口 |
| 跨域处理 | 需要配置 CORS | 同站点时一般不需要 |
| 调试方式 | 前端、后端分别调试 | 通常由 VS 一次启动 |

因此，Visual Studio 启动 API 后打开 Swagger 是正常现象。Swagger 是
后端接口文档和测试页面，不是业务 UI。

## 5. 为什么 Visual Studio 启动 API 后没有 UI

当前 `ProjectVersionManager.sln` 中包含：

- `ProjectVersionManager.Api`
- `ProjectVersionManager.Tests`

Vue 前端不是 `.csproj` 项目，因此没有被加入 Visual Studio 解决方案。

`ProjectVersionManager.slnLaunch` 中的“API 调试”配置也只启动：

```text
src/ProjectVersionManager.Api/ProjectVersionManager.Api.csproj
```

所以在 Visual Studio 中按 F5 后，只会启动 ASP.NET Core API，并打开：

```text
http://localhost:5098/swagger
```

Vue UI 需要通过 Node.js 和 Vite 单独启动。

## 6. 本地运行和联调

### 6.1 启动后端 API

1. 使用 Visual Studio 打开：

   ```text
   ProjectVersionManager.sln
   ```

2. 将 `ProjectVersionManager.Api` 设置为启动项目。

3. 在启动配置中选择 `http` 或 `https` 项目配置。

4. 按 F5 启动调试。

5. 确认以下地址可以访问：

   ```text
   Swagger：http://localhost:5098/swagger
   健康检查：http://localhost:5098/health
   ```

建议不要使用 IIS Express 配置，因为 Vue 前端默认请求：

```text
http://localhost:5098/api
```

API 的端口配置位于：

```text
src/ProjectVersionManager.Api/Properties/launchSettings.json
```

### 6.2 配置 Oracle 连接字符串

`appsettings.json` 中没有保存真实的 Oracle 密码。项目定义了
`UserSecretsId`，建议在本机使用 .NET User Secrets 保存连接字符串。

在项目根目录执行：

```powershell
dotnet user-secrets set "ConnectionStrings:Oracle" "<Oracle连接字符串>" `
  --project .\src\ProjectVersionManager.Api\ProjectVersionManager.Api.csproj
```

真实密码和连接字符串不要提交到 Git。

### 6.3 启动 Vue UI

打开 PowerShell、CMD 或 Visual Studio Terminal：

```powershell
cd D:\CodeALL\WebProgram\ProjectVersionManager\src\ProjectVersionManager.Web
npm install
npm run dev -- --port 5173 --strictPort
```

`npm install` 一般只需要在第一次运行，或者 `package.json` 发生变化后执行。

启动成功后会显示：

```text
Local: http://localhost:5173/
```

在浏览器中打开：

```text
http://localhost:5173
```

该地址才是正式 Vue UI 的开发入口。

使用 `--strictPort` 可以防止端口被占用时 Vite 自动切换到 5174。当前后端
CORS 默认只允许 `http://localhost:5173`，如果自动切换端口，接口可能因
跨域限制而无法访问。

### 6.4 前端 API 地址

前端默认请求：

```text
http://localhost:5098/api
```

配置位置：

```text
src/ProjectVersionManager.Web/src/api.ts
```

也可以在前端目录创建 `.env`：

```env
VITE_API_BASE_URL=http://localhost:5098/api
```

修改 `.env` 后需要重新启动 Vite。

## 7. 断点调试

### 7.1 后端断点

1. 在 Visual Studio 的 Controller、Service 或中间件中设置 C# 断点。
2. 按 F5 启动 `ProjectVersionManager.Api`。
3. 使用 `npm run dev` 启动 Vue 前端。
4. 在浏览器中执行登录、查询、新增或修改操作。
5. 请求到达 API 后，Visual Studio 会命中相应断点。

常用断点位置：

```text
Controllers/AuthController.cs
Controllers/ProjectVersionsController.cs
Controllers/OperationLogsController.cs
Infrastructure/ExceptionHandlingMiddleware.cs
```

### 7.2 前端断点

1. 使用 `npm run dev` 启动 Vite。
2. 浏览器打开 `http://localhost:5173`。
3. 按 F12 打开开发者工具。
4. 在 Sources 中找到 `App.vue`、`api.ts` 等源文件。
5. 在 TypeScript 或 Vue 方法中设置断点。

Vite 支持热更新。修改 Vue 或 CSS 后，通常不需要重新启动前端服务。

## 8. 常见问题

### 8.1 UI 可以打开，但接口全部失败

检查：

- API 是否运行在 `http://localhost:5098`。
- Swagger 是否可以访问。
- 前端是否运行在 `http://localhost:5173`。
- `VITE_API_BASE_URL` 是否正确。
- 浏览器 Network 中的请求地址和状态码。
- 后端 CORS 是否允许当前前端地址。
- Oracle 连接字符串是否正确。

### 8.2 出现 CORS 错误

当前允许的前端地址在：

```text
src/ProjectVersionManager.Api/appsettings.json
```

默认值为：

```json
{
  "Cors": {
    "AllowedOrigins": [
      "http://localhost:5173"
    ]
  }
}
```

确保浏览器地址与这里完全一致，包括协议和端口。

### 8.3 Swagger 可以访问，但登录失败

检查：

- Oracle 数据库是否可连接。
- `PVM_USER` 用户表中是否存在相应用户。
- 用户是否启用。
- 密码哈希是否有效。
- API 当前是否使用 Development 环境。
- DevelopmentAdmin 初始化配置是否启用。

### 8.4 API 使用 IIS Express 启动后，UI 请求失败

IIS Express 使用的端口与 `5098` 不同，而前端默认请求 `5098`。

推荐改用 Visual Studio 中的 `http` 或 `https` 项目配置。也可以将
`VITE_API_BASE_URL` 修改成 IIS Express 的实际 API 地址。

### 8.5 修改 CSS 后页面没有更新

正常情况下 Vite 会自动热更新。如果没有更新：

1. 确认终端中的 Vite 仍在运行。
2. 检查浏览器控制台是否有编译错误。
3. 手动刷新页面。
4. 必要时停止并重新执行 `npm run dev`。

## 9. 构建和测试命令

### 9.1 前端生产构建

```powershell
cd .\src\ProjectVersionManager.Web
npm run build
```

构建结果输出到：

```text
src/ProjectVersionManager.Web/dist
```

### 9.2 后端构建

```powershell
dotnet build ProjectVersionManager.sln
```

### 9.3 后端测试

```powershell
dotnet test ProjectVersionManager.sln
```

## 10. 正式前端

开发和联调应使用：

```text
src/ProjectVersionManager.Web
```

并通过以下命令启动：

```powershell
npm run dev -- --port 5173 --strictPort
```
