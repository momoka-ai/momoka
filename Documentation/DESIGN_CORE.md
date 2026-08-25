# Momoka.Core 架构设计

## 1. 总览

Momoka.Core 是**插件宿主 + 核心能力库**：提供一组**通用机制**（机制在 Core、业务语义在模块），支撑项目主旨「AI 协助/接管用户生活」。

| 子系统 | 职责 | 状态 |
|--------|------|------|
| **Plugins** | 插件契约（IPlugin / Plugin 基类，OnEnable/OnDisable）、manifest、加载/启停/依赖图（PluginLoader） | ✅ 本期完成 |
| **Events** | 事件中心（EventHub）：订阅表分桶、快照分发、订阅级三种分发模式、异常隔离 | ✅ 本期完成 |
| **Registry** | 插件间服务发现表：同类型多注册、优先级/来源插件追踪 | ✅ 本期完成 |
| **Configurations** | 统一配置 + 版本迁移（默认<文件<环境<覆写） | 📋 后续迭代（契约见 §8） |
| **Commands** | 指令注册/解析/调用（`help`/`plugins`/`status` 内置） | 📋 后续迭代（契约见 §8） |
| **Scheduling / Notifications / Profiles / State / Security** | 定时 / 通知 / 家庭成员 / 状态发布订阅 / 安全守卫 | 📋 后续迭代（契约见 §8） |

> 依赖方向：**子模块引用 Core**；Core 不引用任何子模块。本期删除 `Core→Home` 工程引用与 Home 专属网关存根（`HomeService`/`IHomeClient`），依赖方向反转。

## 2. 定位与边界

- **插件宿主**：加载运行期扩展单元（插件），管理生命周期与插件间通信机制。
- **能力库**：提供通用设施（注册表/事件中心/后续的配置/指令/调度/通知/档案/状态/安全）。
- **Core 零业务语义**（2026-08 修订）：设施可持有不透明数据（注册表/状态表/调度表/档案），但**不解释业务语义**；语义全在模块。不持有零业务状态的承诺——改为语义边界。
- **不进 Core 的清单**：Agentic / Memory / LLM（归 Momoka.Ai）；家庭模型与设备语义（归 Home）；感知采集（归 Sense）；传输网关（下一期）。
- **通信 = 本地函数调用**：模块间通过服务接口直接调用（服务注册表解析），事件走内存发布/订阅；无序列化、无状态副本。

## 3. 核心概念与命名（三词分层）

| 词 | 含义 | 形态 |
|----|------|------|
| **插件 Plugin** | 运行期扩展单元 | `Plugin` 子类（实现 `IPlugin` 只读契约），经 manifest 声明 |
| **模块 Module** | 静态子工程 | 如 `Momoka.Home` / `Momoka.Ai` / `Momoka.Sense`，实现插件契约即被宿主托管 |
| **服务 Service** | 能力接口 | 插件注册进服务注册表，供其它插件解析调用 |

命名空间规划：本期 `Momoka.Core.Plugins`（含服务注册表）/ `Momoka.Core.Events`；目标另含 `Configurations` / `Commands` / `Scheduling` / `Notifications` / `Profiles` / `State` / `Security`。

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
│   ├── IPlugin.cs / Plugin.cs / PluginAssembly.cs
│   ├── PluginInfo.cs / PluginState.cs / PluginService.cs
│   ├── PluginLoader.cs / PluginExceptions.cs
│   ├── ServiceRegistry.cs / ServiceSource.cs / ServicePriority.cs
│   └── PluginInfo.cs               # plugin.toml 直接反序列化 + 依赖图/排序/校验纯函数
├── Events/                         # DispatchMode / EventHub
```

## 5. 插件系统

### 5.1 契约：`IPlugin` + `Plugin` 基类

```csharp
public interface IPlugin
{
    string Name { get; }                      // 与 manifest.name 交叉校验（宿主回填）
    string Version { get; }                   // 与 manifest.version 交叉校验（宿主回填）
}
```

- `IPlugin` 为**只读契约**；**宿主能力经共享 `PluginService` 注入 `Plugin` 基类**（统一管理服务注册表 / 事件中心 / 日志工厂 / 插件根目录，全插件共用同一实例）；插件专属能力（日志器 / 插件目录 / 配置）由 `Plugin` 按自身名称派生，插件代码访问 `Host.Services` / `Host.Events` / `Logger` / `GetPluginFolder()`。
- 插件构造器须**轻量无副作用**；业务服务用**服务定位**（`Host.Services.Resolve<T>()`，缺失报清晰错误）。
- 预留扩展点（**不加空方法**）：`RegisterOperation<TReq,TRes>`（网关设施期）、指令/CLI 注册（Commands 期）。
- 守卫：`Host` 注入前访问抛 `InvalidOperationException`。

```csharp
public abstract class Plugin : IPlugin
{
    public PluginInfo Info { get; }                 // 插件信息（manifest，注入时回填）
    public string Name => Info.Name;                // 由 Info 提供
    public string Version => Info.Version;
    public PluginState State { get; }               // Loaded/Enabled/Disabled/Failed，由 Loader 推进

    protected PluginService Host { get; }           // 宿主能力束（共享实例，唯一注入点）
    protected ILogger Logger { get; }               // 专属日志器（类别 = 插件名，懒创建）
    protected DirectoryInfo GetPluginFolder();      // Plugins/<name>/，按需即时生成，编排由插件自行决定
    protected FileInfo GetPluginConfig();           // Plugins/<name>/config.toml，按需即时生成

    public virtual void OnEnable() { }              // 启用钩子：注册服务/订阅事件
    public virtual void OnDisable() { }             // 停用钩子：清理由插件自行完成
    internal void InjectHost(PluginInfo info, PluginService host); // Loader 注入
}
```

```csharp
public sealed class PluginService
{
    // 宿主级共享（DI 注册，注入 PluginLoader 与全部 Plugin）
    public ServiceRegistry Services { get; }        // 服务注册表（富 API：多注册/优先级/来源插件）
    public EventHub Events { get; }                 // 事件中心
    public ILoggerFactory LoggerFactory { get; }    // 日志工厂
    public DirectoryInfo PluginsDirectory { get; }  // <base>/Plugins
}
```

插件代码示例：`Host.Services.Register<ITestService>(...)`、`Host.Events.Subscribe<T>(...)`、`Logger.LogInformation(...)`、`GetPluginFolder()`。

### 5.2 plugin.toml（只读内嵌元数据，一个程序集 = 一个插件）

```toml
name = "home"                  # 必填，全局唯一
version = "1.2.3"              # 必填，SemVer 风格（string：可含预发布/构建元数据）
main = "Momoka.Home.HomePlugin, Momoka.Home"   # 必填，Plugin 子类全名（string：程序集加载后惰性解析，不能用 System.Type）
dependency = ["ai"]            # 可选，硬前置插件名数组；引用未知 → fail-fast
dependencyOptional = ["vision"] # 可选，软前置插件名数组；缺失静默跳过，存在则参与排序
authors = ["alice", "bob"]     # 可选，作者与贡献者
description = "..."            # 可选，可读描述
api = "2.1"                    # 可选，开发时针对的宿主 API 版本（System.Version，默认 1.0）
```

- **无 `settings`、无 `enabled`**——运行态与可写内容一律不进 manifest（设置走 `GetPluginConfig()`、数据走 `GetPluginFolder()`）。
- 解析：Tomlyn `TomlSerializer.Deserialize<PluginInfo>` **直接反序列化到类型**（`[TomlRequired]` 必填 / `[TomlIgnore]` 运行时字段 / `[TomlPropertyName]` 映射 `dependency`/`dependencyOptional`/`api`）；嵌入：`<EmbeddedResource Include="plugin.toml" />`；约定名 `plugin.toml`。
- 无 manifest 的 DLL 视为**依赖库跳过**；有但解析错误 / 缺必填字段 → **fail-fast**（`InvalidInfoException`）。
- 用途：Loader 在实例化**之前**完成识别、入口解析、依赖图构建、排序、校验（避免先实例化再校验）。

### 5.3 目录布局

```
Plugins/                          # 唯一运行时根目录（<base>/Plugins，可经 Plugins:BaseDirectory 覆写）
├── <name>/                       # 插件目录（全部插件数据统一挂此，编排由插件自行决定）
│   ├── <name>.dll …             # 插件程序集及本地依赖
│   ├── config.toml               # GetPluginConfig() 按需即时生成（插件设置）
│   └── Data/ …                   # 开发者自行编排（GetPluginFolder() 返回插件目录本身）
└── *.dll                         # 共享 native/依赖库平铺（无 manifest → 跳过）
```

| 内容 | 位置 | 说明 |
|------|------|------|
| 插件设置 | `Plugins/<name>/config.toml` | `GetPluginConfig()` 按需即时生成；后续 Configurations 提供类型化/版本化访问 |
| 插件数据 | `Plugins/<name>/` | `GetPluginFolder()` 按需即时生成，返回插件目录本身；数据库/缓存等由插件自行编排 |

不再有顶层 `Config/` / `Data/` 目录；插件根目录硬编码于基目录（`AppContext.BaseDirectory`，可经配置 `Plugins:BaseDirectory` 覆写）之下，由 `PluginService` 持有；`Plugins/` 目录启动时自动创建。无 `PluginLoaderOptions`。

### 5.4 PluginLoader 流程

**Load(path)**：加载单个插件（出错抛 `InvalidPluginException`）：

1. `Assembly.LoadFrom(path)`（失败 → fail-fast）
2. 读内嵌 `plugin.toml` → `PluginInfo`；无 → 非插件程序集（fail-fast）
3. 重复 name → fail-fast；`GetPluginMainType` 解析 main 并校验为具体 `Plugin` 子类
4. 实例化（public 无参构造器）→ `InjectHost(Info, PluginService)` → 记录 `PluginAssembly` + `Plugin`（State=Loaded），**不调用 OnEnable**

**EnableAsync(Plugin) / DisableAsync(Plugin)**：单插件启停，返回 bool（未加载 / 已处于目标状态 → false；回调抛异常 → 置 Failed 返回 false）。

**EnableAsync()**：构建依赖图（硬前置 `dependency` 引用未知 → fail-fast；软前置 `dependencyOptional` 可解析则构成排序边；检测环 → fail-fast）→ 拓扑排序 → 依序 `OnEnable`（State=Enabled）；任一失败 → 逆序回滚已启用插件 → 返回 false。

**DisableAsync()**：逆拓扑序 `OnDisable`（State=Disabled，清理由插件自行完成）；任一失败 → 返回 false。

**状态**：`Loaded → Enabled ↔ Disabled / Failed`，记于 `Plugin.State`。加载器无内置状态机，**生命周期与主程序同步**。

**实现约定**：`AssemblyResolve` 兜底探询**所有插件子目录**（跨插件引用与插件本地依赖解析）；静态内省原语 `GetPluginFiles` / `GetPluginInfo` / `GetPluginResource` / `GetPluginMainType` 供宿主与外部按文件级访问；`PluginLoader` 构造器注入宿主级 `PluginService`。

### 5.5 打包约定

- 插件项目：`<EmbeddedResource Include="plugin.toml" />` + `<IsPlugin>true</IsPlugin>` + `<PluginId>home</PluginId>`
- 仓库根 `Directory.Build.targets`：IsPlugin 项目拷贝 `$(OutDir)` 产物到 `Plugins/<PluginId>/`（子目录避免同名依赖冲突）
- `Plugins/` 目录入 `.gitignore`（含可写运行时内容）

### 5.6 校验规则与异常（全部 fail-fast）

| 异常 | 场景 |
|------|------|
| `InvalidPluginException` | 插件结构不合法：DLL 不可加载 / main 类型不存在、非 `Plugin` 子类或无法实例化 / 重复插件名 / 依赖环 / 签名校验失败 |
| `InvalidInfoException` | 插件信息不合法：plugin.toml 缺失或不可读 / TOML 格式非法 / 缺关键字段 / 类型不符 |
| `UnknownDependencyException` | `dependency`（硬前置）引用未知或当前不可用（禁用）的插件 |

另：重复 `Load` 抛 `InvalidOperationException`；运行期 Load/Start 失败不包装，原样上抛。

## 6. 服务注册表（Registry，位于 `Momoka.Core.Plugins`）

```csharp
public sealed class ServiceRegistry
{
    void Register<TService>(TService instance, ServicePriority priority = Normal, IPlugin? plugin = null);
    void Register(Type serviceType, object instance, ServicePriority priority = Normal, IPlugin? plugin = null);
    TService Resolve<TService>() where TService : class;   // 最高优先级；缺失抛 InvalidOperationException（fail-fast）
    TService? TryResolve<TService>() where TService : class;
    TService? GetService<TService>() where TService : class;   // = TryResolve
    bool TryGetService<TService>(out TService? value) where TService : class;
    IEnumerable<ServiceSource<TService>> GetRegistrations<TService>();                  // 全部（优先级降序）
    IEnumerable<ServiceSource<TService>> GetRegistrations<TService>(Type serviceType);  // 指定注册键
    IEnumerable<ServiceSource<TService>> GetRegistrations<TService>(IPlugin plugin);    // 指定来源插件
    bool IsRegistered(Type serviceType);
}

public readonly record struct ServiceSource<T>(Type Service, T Source, ServicePriority Priority, IPlugin? Plugin);
public enum ServicePriority { Highest, High, Normal, Low, Lowest }
```

- 同类型允许多注册，每项记录来源插件与优先级；单值解析取**优先级最高**者（同级按先注册先得）；`GetRegistrations` 按优先级降序返回，可滤按注册键或来源插件。
- `Dictionary<Type, List<Entry>>` + `lock`；Type 为**不透明键**（Core 不解释业务语义）。
- **与 DI 容器分工**：宿主自身设施走 DI（Generic Host）；插件提供的业务服务走 Registry（插件反射实例化无法构造器注入）。

## 7. 事件中心（Events）

```csharp
public enum DispatchMode { Sequential, Parallel, Background }

public sealed class EventHub
{
    IDisposable Subscribe<TEvent>(Func<TEvent, Task> handler, DispatchMode mode = DispatchMode.Sequential);
    Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default);
}
```

- 订阅表按 `Type` 分桶 + `lock`；订阅/退订/发布线程安全；**发布时快照订阅表再分发**（分发中退订不影响本次）。
- **分发模式由订阅者声明**（发布者只 await）：Sequential 按序 await、Parallel 并发（`Task.WhenAll`）、Background 即发即忘；handler 异常一律隔离记日志，绝不向发布方传播。
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
| **Home** | 纯模型库，实现 `Plugin`（`HomePlugin`）作为插件由宿主托管；适配器下一期 |
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
