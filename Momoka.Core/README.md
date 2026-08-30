# Momoka.Core

插件宿主 + 核心能力库（C# / .NET 8）。

## 定位

Core 提供一组**通用机制**（机制在 Core、业务语义在模块），支撑「AI 协助/接管用户生活」：

- **Plugins**：插件契约（`Plugin` 基类，生命周期 `OnEnable` / `OnDisable`）+ `plugin.toml` 直接反序列化到 `PluginInfo` + 加载/启停/依赖图（`PluginLoader`）
- **Events / Behaviors**：事件中心 `EventHub`（订阅级顺序 / 并行发布，异常隔离）+ 行为契约 `Behavior`（意图 → 事实，客户端/主机两端共用）
- **Registry**：插件间服务发现表（同类型多注册、优先级/来源插件追踪）
- **Configurations**：统一配置 + 版本迁移链（文件 / 二进制 / 数据库三后端，未知字段保留向后兼容）
- **Commands**：指令定义 / 解析 / 执行（迷你语言 + 类型化参数，Minestom 风格，终端向）
- **Gateway**：Ui 网关设施（`/hubs/gateway` 单路由：操作 request/response + 行为上报管线 + 线上事件广播 + 客户端注册表）
- **Scheduling / Notifications / Profiles / State / Security**：后续迭代（契约见 `Documentation/DESIGN_CORE.md` §10）

依赖方向：**子模块引用 Core**；Core 不引用任何子模块（Agentic / Memory / LLM 归 Ai，不进 Core）。

## 宿主入口

```bash
dotnet run --project Momoka.Core
```

默认扫描 `<base>/Plugins`（每插件一子目录，DLL + `config.toml` + 自编排数据统一挂此；无 manifest 的 DLL 平铺视为依赖库）；路径硬编码于基目录（可经 `appsettings.json` 的 `Plugins:BaseDirectory` 覆写）。启动即 `Load` 全部插件（行为扫描注册）并按依赖图依序 `EnableAsync()`，退出逆序 `DisableAsync()`。

## 插件开发要点

- 实现 `Plugin`（`Info` 含 manifest 身份，由宿主注入；`OnEnable` 注册服务/订阅事件，`OnDisable` 清理——清理由插件自行完成）
- 嵌入 `plugin.toml`（`name` / `version` / `main` / `dependency` / `dependencyOptional` / `authors` / `description` / `api`），无 settings / enabled
- 项目设置 `<IsPlugin>true</IsPlugin>` + `<PluginId>name</PluginId>`，产物自动拷入 `Plugins/<name>/`
- 宿主能力经 `Host` 访问共享设施（`Host.Services.Register/Resolve`、`Host.Events.Subscribe`、`Host.Gateway.RegisterOperation`）+ 专属派生（`Logger` / `GetPluginFolder()` / `GetPluginConfig()`）
- 行为上报：实现 `Behavior`（嵌套 `Intent` / `Event`（携带 `[Publish]`）+ `Execute(Intent, IntentSource?)`），加载期自动扫描注册，客户端 `Post` 意图 → 执行生成事实 → 广播全部终端

## 验证

```bash
dotnet build Momoka.sln          # 0 错误 0 警告
dotnet test Tests/Momoka.Core/Momoka.Core.Tests.csproj
```

## 命名空间

`Momoka.Core.Plugins`（含服务注册表）/ `Momoka.Core.Behaviors`（事件中心 + 行为契约）/ `Momoka.Core.Commands`（含 `Arguments` / `Parsing`）/ `Momoka.Core.Configurations`；网关设施位于根命名空间 `Momoka.Core`。
