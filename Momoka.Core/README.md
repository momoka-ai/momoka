# Momoka.Core

插件宿主 + 核心能力库（C# / .NET 8）。

## 定位

Core 提供一组**通用机制**（机制在 Core、业务语义在模块），支撑「AI 协助/接管用户生活」：

- **Plugins**：插件契约（`IPlugin` / `CorePlugin`）+ manifest（`plugin.toml`）+ 扫描/排序/校验/生命周期（`PluginLoader`）
- **Events**：强类型事件总线（Sequential / Parallel / Background，异常隔离）
- **Registry**：插件间服务发现表（fail-fast）
- **Configurations / Commands / Scheduling / Notifications / Profiles / State / Security**：后续迭代（契约见 `Documentation/DESIGN_CORE.md` §8）

依赖方向：**子模块引用 Core**；Core 不引用任何子模块（Agentic / Memory / LLM 归 Ai，不进 Core）。

## 宿主入口

```bash
dotnet run --project Momoka.Core
```

默认扫描 `<base>/Plugins`（每插件一子目录）、`Config/`（插件管理 `plugins.toml` + 插件设置 `Plugins/<name>.toml`）、`Data/`（插件数据 `Plugins/<name>/`）。目录可用 `appsettings.json` 的 `Plugins:*` 覆写。

## 插件开发要点

- 实现 `CorePlugin`（`Name`/`Version` 由宿主按 manifest 回填；`OnLoad` 注册服务/订阅事件）
- 嵌入 `plugin.toml`（`name` / `version` / `entry` / `dependsOn`），无 settings / enabled
- 项目设置 `<IsPlugin>true</IsPlugin>` + `<PluginId>name</PluginId>`，产物自动拷入 `Plugins/<name>/`
- 业务服务用 `Services.Register/Resolve`；宿主设施（DI）与插件服务（Registry）分工

## 验证

```bash
dotnet build Momoka.sln          # 0 错误 0 警告
dotnet test Tests/Momoka.Core/Momoka.Core.Tests.csproj
```

## 命名空间

`Momoka.Core.Plugins` / `Momoka.Core.Events` / `Momoka.Core.Registry`（后续加入其余设施）。
