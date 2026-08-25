# Momoka.Core 架构设计

## 1. 总览

Momoka.Core 是**插件宿主 + 核心能力库**：提供一组**通用机制**（机制在 Core、业务语义在模块），支撑项目主旨「AI 协助/接管用户生活」。

| 子系统 | 职责 | 状态 |
|--------|------|------|
| **Plugins** | 插件契约（IPlugin / CorePlugin）、manifest、扫描/排序/校验/生命周期（PluginLoader） | ✅ 本期完成 |
| **Events** | 强类型事件总线：订阅表分桶、快照分发、三种分发模式、异常隔离 | ✅ 本期完成 |
| **Registry** | 插件间服务发现表：每类型单实例、fail-fast | ✅ 本期完成 |
| **Configurations** | 统一配置 + 版本迁移（默认<文件<环境<覆写） | 📋 后续迭代（契约见 §8） |
| **Commands** | 指令注册/解析/调用（`help`/`plugins`/`status` 内置） | 📋 后续迭代（契约见 §8） |
| **Scheduling / Notifications / Profiles / State / Security** | 定时 / 通知 / 家庭成员 / 状态发布订阅 / 安全守卫 | 📋 后续迭代（契约见 §8） |

> 依赖方向：**子模块引用 Core**；Core 不引用任何子模块。本期删除 `Core→Home` 工程引用与 Home 专属网关存根（`HomeService`/`IHomeClient`），依赖方向反转。

## 2. 定位与边界

- **插件宿主**：加载运行期扩展单元（插件），管理生命周期与插件间通信机制。
- **能力库**：提供通用设施（注册表/事件总线/后续的配置/指令/调度/通知/档案/状态/安全）。
- **Core 零业务语义**（2026-08 修订）：设施可持有不透明数据（注册表/状态表/调度表/档案），但**不解释业务语义**；语义全在模块。不持有零业务状态的承诺——改为语义边界。
- **不进 Core 的清单**：Agentic / Memory / LLM（归 Momoka.Ai）；家庭模型与设备语义（归 Home）；感知采集（归 Sense）；传输网关（下一期）。
- **通信 = 本地函数调用**：模块间通过服务接口直接调用（服务注册表解析），事件走内存发布/订阅；无序列化、无状态副本。

## 3. 核心概念与命名（三词分层）

| 词 | 含义 | 形态 |
|----|------|------|
| **插件 Plugin** | 运行期扩展单元 | `IPlugin` 实现（`CorePlugin` 子类），经 manifest 声明 |
| **模块 Module** | 静态子工程 | 如 `Momoka.Home` / `Momoka.Ai` / `Momoka.Sense`，实现插件契约即被宿主托管 |
| **服务 Service** | 能力接口 | 插件注册进服务注册表，供其它插件解析调用 |

命名空间规划：本期 `Momoka.Core.Plugins` / `Momoka.Core.Events` / `Momoka.Core.Registry`；目标另含 `Configurations` / `Commands` / `Scheduling` / `Notifications` / `Profiles` / `State` / `Security`。

## 4. 依赖方向与工程结构

```
Momoka.Home ─┐
Momoka.Ai   ─┤──▶ Momoka.Core  （只依赖框架与 Tomlyn）
Momoka.Sense┘
```

```
Momoka.Core/
├── Program.cs                      # 宿主入口：Generic Host + PluginLoader
├── Momoka.Core.csproj              # Microsoft.AspNetCore.App + Tomlyn（无子模块引用）
├── Plugins/                        # 插件子系统
│   ├── IPlugin.cs / CorePlugin.cs
│   ├── PluginInfo.cs / PluginService.cs
│   ├── PluginLoader.cs / PluginExceptions.cs
│   └── PluginInfo.cs               # plugin.toml 直接反序列化 + 依赖图/排序/校验纯函数
├── Events/                         # DispatchMode / IEventBus / EventBus
└── Registry/                       # IServiceRegistry / ServiceRegistry
```

## 5. 插件系统

### 5.1 契约：`IPlugin` + `CorePlugin`

```csharp
public interface IPlugin
{
    string Name { get; }                      // 与 manifest.name 交叉校验（宿主回填）
    string Version { get; }                   // 与 manifest.version 交叉校验（宿主回填）
    Task StartAsync(CancellationToken cancellationToken);
    Task StopAsync(CancellationToken cancellationToken);
}
```

- `IPlugin` 无 `Load`；**宿主能力经单一 `PluginService` 注入 `CorePlugin` 基类**（统一管理服务注册表 / 事件总线 / 日志器 / 数据与配置目录），插件一律经 `Plugin.*` 直接访问；`CorePlugin` **不提供任何快捷成员**（Services/Events/Logger/Subscribe/GetPluginFolder 等），保持插件代码简单。
- 插件构造器须**轻量无副作用**；业务服务用**服务定位**（`Plugin.Services.Resolve<T>()`，缺失报清晰错误）。
- 预留扩展点（**不加空方法**）：`RegisterOperation<TReq,TRes>`（网关设施期）、指令/CLI 注册（Commands 期）。
- 守卫：`Plugin` 注入前访问抛 `InvalidOperationException`；重复 `Load` 抛 `InvalidOperationException`（以 `Info.State`（`CorePlugin.PluginState`）守卫）。

```csharp
public abstract class CorePlugin : IPlugin
{
    public PluginInfo Info { get; }                 // 插件信息（manifest + 状态，注入时回填）
    public string Name => Info.Name;                // 由 Info 提供
    public string Version => Info.Version;

    protected PluginService Plugin { get; }         // 宿主能力束（唯一注入点）
    protected virtual void OnLoad() { }             // 初始化钩子
    internal void InjectHost(PluginInfo info, PluginService service); // Loader 注入
    internal void Load();                           // 非虚：Info.State 守卫 + OnLoad
}
```

```csharp
public sealed class PluginService
{
    // 宿主级（DI 注入 PluginLoader）：Services / Events / LoggerFactory / 三目录
    public IServiceRegistry Services { get; }
    public IEventBus Events { get; }
    public ILoggerFactory LoggerFactory { get; }
    public DirectoryInfo PluginsDirectory { get; }   // <base>/Plugins
    public DirectoryInfo ConfigDirectory { get; }     // <base>/Config
    public DirectoryInfo DataDirectory { get; }       // <base>/Data
    // 插件级（ForPlugin 派生，注入 CorePlugin）：
    public string Name { get; }
    public ILogger Logger { get; }                   // 类别 = 插件名（{Plugin: name} 前缀语义）
    public DirectoryInfo GetPluginFolder();          // Data/Plugins/<name>/，按需即时生成
    public FileInfo GetPluginConfig();               // Config/Plugins/<name>.toml，按需即时生成
}
```

插件代码示例：`Plugin.Services.Register<ITestService>(...)`、`Plugin.Events.Subscribe<T>(...)`、`Plugin.GetPluginFolder()`。

### 5.2 plugin.toml（只读内嵌元数据，一个程序集 = 一个插件）

```toml
name = "home"                  # 必填，全局唯一
version = "1.2.3"              # 必填，SemVer 风格（string：可含预发布/构建元数据）
main = "Momoka.Home.HomePlugin, Momoka.Home"   # 必填，CorePlugin 子类全名（string：程序集加载后惰性解析，不能用 System.Type）
dependency = ["ai"]            # 可选，硬前置插件名数组；引用未知/禁用 → fail-fast
dependencyOptional = ["vision"] # 可选，软前置插件名数组；缺失/禁用静默跳过，存在则参与排序
authors = ["alice", "bob"]     # 可选，作者与贡献者
description = "..."            # 可选，可读描述
api = "2.1"                    # 可选，开发时针对的宿主 API 版本（System.Version，默认 1.0）
```

- **无 `settings`、无 `enabled`**——运行态与可写内容一律不进 manifest（`enabled` 走宿主配置、设置走 `GetPluginConfig()`、数据走 `GetPluginFolder()`）。
- 解析：Tomlyn `TomlSerializer.Deserialize<PluginInfo>` **直接反序列化到类型**（`[TomlRequired]` 必填 / `[TomlIgnore]` 运行时字段 / `[TomlPropertyName]` 映射 `dependency`/`dependencyOptional`/`api`）；嵌入：`<EmbeddedResource Include="plugin.toml" />`；约定名 `plugin.toml`。
- 无 manifest 的 DLL 视为**依赖库跳过**；有但解析错误 / 缺必填字段 → **fail-fast**（`InvalidInfoException`）。
- 用途：Loader 在实例化**之前**完成识别、入口解析、依赖图构建、排序、校验（避免先实例化再校验）。

### 5.3 可写配置与数据目录

| 内容 | 位置 | 说明 |
|------|------|------|
| 插件启用（宿主插件管理） | `Config/plugins.toml`，`[home] enabled = false` | 缺失默认 true；禁用无需重新打包 |
| 插件设置 | `Config/Plugins/<name>.toml` | `GetPluginConfig()` 按需即时生成；后续 Configurations 提供类型化/版本化访问 |
| 插件数据 | `Data/Plugins/<name>/` | `GetPluginFolder()` 按需即时生成；数据库/缓存等 |

目录名遵循大驼峰约定（UE 风格 `Plugins` / `Config` / `Data`）。三个路径**硬编码**于基目录（`AppContext.BaseDirectory`，可经配置 `Plugins:BaseDirectory` 覆写）之下，由 `PluginService` 持有；目录启动时自动创建。无 `PluginLoaderOptions`。

### 5.4 PluginLoader 流程

**StartAsync**：

1. 递归扫描 `PluginsDirectory` 内 `*.dll` → `Assembly.LoadFrom`（默认 ALC）→ 读内嵌 `plugin.toml`；无 → 跳过（依赖库）；解析错误 → fail-fast
2. 读 `Config/plugins.toml` 得 `enabled` 覆盖，过滤启用插件；**重复 name → fail-fast**
3. 构建插件名依赖图：硬前置（`dependency`）引用未知/禁用插件 → fail-fast；软前置（`dependencyOptional`）缺失/禁用静默跳过、存在则同样构成排序边；**检测环 → fail-fast**
4. 拓扑排序（Kahn）→ Load/Start 顺序
5. 实例化 main（public 无参构造器）并校验为 `CorePlugin` 子类；type 不存在 / 抽象 / 非 CorePlugin → fail-fast
6. `InjectHost(Info, ForPlugin(Name))` → `Load()`（OnLoad）→ 依序 `StartAsync`
7. **失败回滚**：Load/Start 任一失败 → 逆序 Stop 已 Started 插件（best-effort）→ 原样上抛（校验类抛 `InvalidPluginException` / `InvalidInfoException` / `UnknownDependencyException`；运行期异常不包装）

**StopAsync**：逆序 `StopAsync`（best-effort，异常聚合记录，不抛出）。状态推进 `Discovered→Loaded→Started→Stopped/Failed` 记于 `PluginInfo.State`（枚举 `CorePlugin.PluginState` 内嵌于 `CorePlugin`）。加载器无内置状态机，**生命周期与主程序同步**。

**实现约定**：`AssemblyResolve` 兜底探询**所有插件子目录**（跨插件引用与插件本地依赖解析）；启动打印插件图（名称/版本/依赖/顺序）；`PluginLoader` 构造器注入宿主级 `PluginService`。

### 5.5 打包约定

- 插件项目：`<EmbeddedResource Include="plugin.toml" />` + `<IsPlugin>true</IsPlugin>` + `<PluginId>home</PluginId>`
- 仓库根 `Directory.Build.targets`：IsPlugin 项目拷贝 `$(OutDir)` 产物到 `Plugins/<PluginId>/`（子目录避免同名依赖冲突）
- `Plugins/` / `Config/` / `Data/` 目录入 `.gitignore`（Config 含可写运行时配置）

### 5.6 校验规则与异常（全部 fail-fast）

| 异常 | 场景 |
|------|------|
| `InvalidPluginException` | 插件结构不合法：DLL 不可加载 / main 类型不存在、非 `CorePlugin` 或无法实例化 / 重复插件名 / 依赖环 / 签名校验失败 |
| `InvalidInfoException` | 插件信息不合法：plugin.toml 缺失或不可读 / TOML 格式非法 / 缺关键字段 / 类型不符 |
| `UnknownDependencyException` | `dependency`（硬前置）引用未知或当前不可用（禁用）的插件 |

另：重复服务注册、重复 `Load` 抛 `InvalidOperationException`；运行期 Load/Start 失败不包装，原样上抛。

## 6. 服务注册表（Registry）

```csharp
public interface IServiceRegistry
{
    void Register<TService>(TService instance) where TService : class;
    void Register(Type serviceType, object instance);   // 校验 IsInstanceOfType
    TService Resolve<TService>() where TService : class; // 缺失抛 InvalidOperationException（fail-fast）
    TService? TryResolve<TService>() where TService : class;
    bool IsRegistered(Type serviceType);
}
```

- 每类型单实例，重复注册 fail-fast；`Dictionary<Type, object>` + `lock`；Type 为**不透明键**（Core 不解释业务语义）。
- **与 DI 容器分工**：宿主自身设施走 DI（Generic Host）；插件提供的业务服务走 Registry（插件反射实例化无法构造器注入）。

## 7. 事件总线（Events）

```csharp
public enum DispatchMode { Sequential, Parallel, Background }

public interface IEventBus
{
    IDisposable Subscribe<TEvent>(Func<TEvent, Task> handler);
    Task PublishAsync<TEvent>(TEvent @event, DispatchMode mode = DispatchMode.Sequential,
                              CancellationToken cancellationToken = default);
}
```

- 订阅表按 `Type` 分桶 + `lock`；订阅/退订/发布线程安全；**发布时快照订阅表再分发**（分发中退订不影响本次）。
- Sequential：按序 await，单 handler 异常隔离记日志；Parallel：`Task.WhenAll` 异常聚合；Background：`Task.Run` fire-and-forget（handler 异常各自隔离）。
- 事件类型由插件自声明；**Core 不定义业务事件**。

## 8. 目标能力面（后续迭代契约草图）

> 本期只定义契约，不实现。

- **Configurations**（统一配置 + 版本迁移）：`IConfiguration.Get<T>(path)` 分层访问（默认<文件<环境<覆写）；配置文件带 `version`，`IMigration`（from→to）升级链——旧配置**向上升级**、未知字段保留（**向后兼容**）；接管/取代 `GetPluginConfig()` 的类型化访问
- **Commands**：`ICommandRegistry.Register(ICommandDefinition)` / `InvokeAsync(name, args, caller)`；格式迷你语言（`<必需> [可选] --flag`）+ 解析器；`RequiredRole` 与 Profiles/Security 联动；内置 `help`/`plugins`/`status`；供 CLI/终端/AI 工具面接入
- **Scheduling**：`IScheduler.ScheduleOnce/ScheduleRecurring`；任务持久化；可包装 Command 调用（「8 点执行 lights.on」）
- **Notifications**：`INotificationService.NotifyAsync`；severity/dedupeKey/目标；通道抽象（终端=网关通道）；离线入队待送达
- **Profiles**：`IProfileService`（家庭成员 id/name/avatar/role/preferences/presence）；与连接身份、Security、Ai 记忆联动
- **State/Context**：`IStateStore.Publish(entityId, key, value[, ttl])` / Get / Subscribe；不透明类型化值；Home 发布设备状态、Profiles 发布在场、Sense 汇入传感器，Ai 消费（决策依据）
- **Security**（定案）：`ISecurityGuard.RegisterPolicy` / `AuthorizeAsync`；机制在 Core、规则由插件注册；危险操作拦截

**数据流**：Sense 采集→标准化→State；Home 设备状态→State；用户指令/LLM 意图→Commands（经 Security 校验）→设备执行；Scheduling→定时触发 Command/事件；事件→Notifications→终端（网关通道）；Profiles↔连接身份↔Security↔Ai 记忆。

**边界**：Memory/LLM/Agentic→Ai；家庭模型/设备语义→Home；感知采集→Sense；传输（网关设施）→下一期。

## 9. Ui 传输范式（下一期）

Core 网关设施 · **单路由**（通用操作路由）：一个通用路由承载操作（RegisterOperation 注册）与连接身份/角色/token 鉴权；不采用插件内手写传输中间件（Envelope 已删除的定案不变）。网关设施本身位于 Core（宿主设施），插件只注册操作与订阅事件。

## 10. 与其它模块关系

| 模块 | 关系 |
|------|------|
| **Home** | 纯模型库，实现 `CorePlugin`（`HomePlugin`）作为插件由宿主托管；适配器下一期 |
| **Ai** | Agentic / Memory / LLM，独立模块（不进 Core）；亦作为插件注册 |
| **Sense** | 感知采集，输出标准化状态到 State；作为插件注册 |
| **Voice** | Python HTTP 服务（TTS），经 Commands / 网关调用 |
| **Ui** | 唯一远程边界（Godot C# .NET），下一期经 Core 网关设施单路由连接 |

## 11. 设计原则

- **SOLID**：接口 + 抽象基类 + 单一职责子模块
- **fail-fast**：校验一律前置、错误信息清晰、细分异常（InvalidPlugin / InvalidInfo / UnknownDependency）统一出口
- **零业务语义**：Core 只提供机制，语义全在模块
- **服务定位 vs DI 分工**：宿主设施走 DI，插件服务走 Registry
- **编译期统一**：同解决方案编译期类型安全；Roslyn 运行期编译 / 第三方动态加载 / 热插拔推迟（待插件生态需要时再评估）
- **简单优先**：无独立 IPluginContext、无空扩展方法、无过度抽象
