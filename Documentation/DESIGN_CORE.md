# Momoka.Core 架构设计

## 1. 总览

Momoka.Core 是**插件宿主 + 核心能力库**：提供一组**通用机制**（机制在 Core、业务语义在模块），支撑项目主旨「AI 协助/接管用户生活」。

| 子系统 | 职责 | 状态 |
|--------|------|------|
| **Plugins** | 插件契约（`Plugin` 基类，OnEnable/OnDisable）、manifest、加载/启停/依赖图（PluginLoader） | ✅ 本期完成 |
| **Events** | 事件中心（EventHub）：订阅表分桶、快照分发、订阅级三种分发模式、异常隔离 | ✅ 本期完成 |
| **Registry** | 插件间服务发现表：同类型多注册、优先级/来源插件追踪 | ✅ 本期完成 |
| **Configurations** | 统一配置 + 版本迁移：不透明值树 + 版本键 + 迁移链（文件 / 二进制 / 数据库三后端） | ✅ 本期完成 |
| **Commands** | 指令定义 / 解析 / 执行（迷你语言 + 类型化参数，Minestom 风格，终端向） | ✅ 本期完成 |
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
| **插件 Plugin** | 运行期扩展单元 | `Plugin` 子类，经 manifest 声明 |
| **模块 Module** | 静态子工程 | 如 `Momoka.Home` / `Momoka.Ai` / `Momoka.Sense`，实现插件契约即被宿主托管 |
| **服务 Service** | 能力接口 | 插件注册进服务注册表，供其它插件解析调用 |

命名空间规划：本期 `Momoka.Core.Plugins`（含服务注册表）/ `Momoka.Core.Events` / `Momoka.Core.Configurations` / `Momoka.Core.Commands`（含 `Arguments` / `Parsing` 子命名空间）；目标另含 `Scheduling` / `Notifications` / `Profiles` / `State` / `Security`。

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
│   ├── Plugin.cs / PluginState.cs / PluginService.cs
│   ├── PluginInfo.cs / PluginLoader.cs / PluginExceptions.cs
│   └── ServiceRegistry.cs / ServiceSource.cs / ServicePriority.cs
├── Events/                         # DispatchMode / EventHub
├── Configurations/                 # Configuration / Migration + File/Binary/Database 三种后端
└── Commands/                       # Command / CommandExecutor / CommandManager / CommandParser
```

## 5. 插件系统

### 5.1 契约：`Plugin` 基类

- **宿主能力经共享 `PluginService` 注入 `Plugin` 基类**（统一管理服务注册表 / 事件中心 / 日志工厂 / 插件根目录，全插件共用同一实例）；插件专属能力（日志器 / 插件目录 / 配置）由 `Plugin` 按自身名称派生，插件代码访问 `Host.Services` / `Host.Events` / `Logger` / `GetPluginFolder()`。
- 插件构造器须**轻量无副作用**；业务服务用**服务定位**（`Host.Services.Resolve<T>()`，缺失报清晰错误）。
- 预留扩展点（**不加空方法**）：`RegisterOperation<TReq,TRes>`（网关设施期）、指令/CLI 注册（Commands 期）。
- 守卫：`Host` 注入前访问抛 `InvalidOperationException`。

```csharp
public abstract class Plugin
{
    public PluginInfo Info { get; }                 // 插件信息（manifest，注入时回填）
    public string Name => Info.Name;                // 由 Info 提供
    public string Version => Info.Version;
    public PluginState State { get; }               // Loaded/Enabled/Disabled/Failed，由 Loader 推进

    protected PluginService Host { get; }           // 宿主能力束（共享实例，唯一注入点）
    protected ILogger Logger { get; }               // 专属日志器（类别 = 插件名，懒创建）
    protected DirectoryInfo GetPluginFolder();      // Plugins/<name>/，按需即时生成，编排由插件自行决定
    protected FileInfo GetPluginConfig();           // Plugins/<name>/config.toml，按需即时生成
    protected Stream? GetPluginResource(string path); // 提取本插件打包的嵌入资源流（内嵌名），未找到返回 null

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
    void Register<TService>(TService instance, ServicePriority priority = Normal, Plugin? plugin = null);
    void Register(Type serviceType, object instance, ServicePriority priority = Normal, Plugin? plugin = null);
    TService Resolve<TService>() where TService : class;   // 最高优先级；缺失抛 InvalidOperationException（fail-fast）
    TService? TryResolve<TService>() where TService : class;
    TService? GetService<TService>() where TService : class;   // = TryResolve
    bool TryGetService<TService>(out TService? value) where TService : class;
    IEnumerable<ServiceSource<TService>> GetRegistrations<TService>();                  // 全部（优先级降序）
    IEnumerable<ServiceSource<TService>> GetRegistrations<TService>(Type serviceType);  // 指定注册键
    IEnumerable<ServiceSource<TService>> GetRegistrations<TService>(Plugin plugin);    // 指定来源插件
    bool IsRegistered(Type serviceType);
}

public readonly record struct ServiceSource<T>(Type Service, T Source, ServicePriority Priority, Plugin? Plugin);
public enum ServicePriority { Highest, High, Normal, Low, Lowest }
```

- 同类型允许多注册，每项记录来源插件与优先级；单值解析取**优先级最高**者（同级按先注册先得）；`GetRegistrations` 按优先级降序返回，可滤按注册键或来源插件。
- `Dictionary<Type, List<Entry>>` + `lock`；Type 为**不透明键**（Core 不解释业务语义）。
- **与 DI 容器分工**：宿主自身设施走 DI（Generic Host）；插件提供的业务服务走 Registry（插件反射实例化无法构造器注入）。

## 7. 事件中心（Events）

```csharp
public enum DispatchMode { Sequential, Parallel, Background }
public enum EventDestination { None, Listeners, Client, Everyone }
public enum EventPriority { Lowest, Low, Normal, High, Highest, Monitor }

[AttributeUsage(AttributeTargets.Class)]
public sealed class EventRouterAttribute
{
    string? Id { get; set; }                        // 线上 eventId（snake_case，全局唯一）
    EventDestination Destination { get; set; } = EventDestination.Listeners;
    bool FromClients { get; set; }                  // 接受客户端上报（wire-in）；true 时须带 Id 且 Destination=Listeners
}

[AttributeUsage(AttributeTargets.Method)]
public sealed class EventSubscribeAttribute
{
    Type Target { get; }                            // 事件类型
    EventPriority Priority { get; set; } = EventPriority.Normal;
    bool IgnoreCancellation { get; set; }           // 预留（可取消事件随 Security/拦截需求再定）
}

public sealed class EventHub
{
    IDisposable Subscribe<TEvent>(Func<TEvent, Task> handler, DispatchMode mode = DispatchMode.Sequential);
    IDisposable AddSubscribers(object subscriber, Plugin? plugin = null);   // 扫描 [EventSubscribe]，整体退订令牌
    void RegisterEventType(Type type);              // 扫描 [EventRouter]，重复 Id / 组合非法 fail-fast
    Task InvokeAsync<TEvent>(TEvent @event, CancellationToken ct = default);
    Task InvokeAsync(object @event, CancellationToken ct = default);        // 按运行期类型分发（wire-in 用）
    Task PublishAsync<TEvent>(TEvent @event, CancellationToken ct = default); // InvokeAsync 兼容别名
    // 构造注入：wire-sender（线上广播）/ recorder（被动审计 sink）
}
public sealed class EventRecorder { Task RecordAsync(object @event, CancellationToken ct); }  // ILogger 后端
```

- 订阅表按 `Type` 分桶 + `lock`；订阅/退订/发布线程安全；**发布时快照订阅表再分发**（分发中退订不影响本次）。
- **分发模式由订阅者声明**（发布者只 await）：Sequential 按序 await、Parallel 并发（`Task.WhenAll`）、Background 即发即忘；handler 异常一律隔离记日志，绝不向发布方传播。
- **监听自动化**：`AddSubscribers` 实例扫描 `[EventSubscribe]`（Bukkit 风格，签名 = 单参数 Target + 返回 Task/void，非法 fail-fast）；按 `EventPriority` 排序执行（高者先、同级按注册序、**Monitor 恒最后**）；返回令牌整体退订（插件 OnDisable 用）；与手动 `Subscribe<T>` lambda 共存。
- **路由自动化**：`RegisterEventType` 注册 `[EventRouter]` 类型后，`InvokeAsync` 按**路由矩阵**统一分发——记录器恒记录；`None` 仅记录器 / `Listeners` 仅监听者 / `Client` 仅 wire-out（须 Id）/ `Everyone` 监听者 + wire-out（须 Id）；`FromClients=true` 接受 wire-in 且**只进监听者、绝不广播回客户端**（避免 echo）。wire-sender 失败只记日志，进程内分发不受影响。
- 事件类型由插件自声明；**Core 不定义业务事件**。

## 8. 配置（Configurations）

```csharp
public abstract class Configuration
{
    // 不透明值树：点分路径分层键（a.b.c），值 = 基础类型(string/bool/long/double/DateTime) / 值列表 / 嵌套表
    public Version Version { get; }                         // 当前版本（迁移链应用后）
    public T Get<T>(string path);                           // 类型化读取（枚举/Guid/DateTime/int…自动转换），缺失 fail-fast
    public bool TryGet<T>(string path, out T? value);
    public object? GetValue(string path);                   // 原始存储形态
    public void Set<T>(string path, T value);               // 枚举→名、Guid→字符串、数值→long/double
    public IReadOnlyCollection<string> GetKeys(string path);
    public bool Contains(string path);
}

public sealed class Migration(Version from, Version to, Action<Configuration> apply);
public sealed class FileConfiguration : Configuration       // TOML（顶层保留键 version）
public sealed class BinaryConfiguration : Configuration      // 紧凑 BLOB（魔数 MCFG + 格式版本 + 标签化值）
public sealed class DatabaseConfiguration : Configuration    // IConfigurationStore 扁平行 + JSON 列表编码
public interface IConfigurationStore { IReadOnlyDictionary<string,string?> ReadAll(); void WriteAll(...); }
```

- **版本迁移链**：文件/存储顶层保留键 `version`；加载时按迁移链从存储版本升到目标版本（缺省 = 迁移链最大 To）；**断链 fail-fast**（`ConfigurationException`）；迁移只增改已知键，**未知字段保留**（向后兼容）。迁移来源版本重复 fail-fast。
- **三种后端同一套值树 + 类型化 API**：文件（Tomlyn 直接反序列化到 `TomlTable`，未知字段原样往返）；二进制（自定小端/UTF8 长度前缀 codec，适合插件数据的紧凑不透明持久化）；数据库（扁平行「点分键 = 文本」，值文本类型嗅探解释，列表用 System.Text.Json）。存储层不解释语义（Core 只提供机制）。
- 接管 / 取代 `GetPluginConfig()` 的类型化访问（原契约见 §8→本文）。

## 9. 指令（Commands）

```csharp
public abstract class Command
{
    public string Name; public virtual IReadOnlyList<string> Aliases;
    public virtual string Description; public virtual string Syntax;   // 迷你语言声明（字符串语法糖）
    public CommandExecutor? Executor;                                  // 可组合执行器
    public virtual CommandExecutor? DefaultExecutor;                   // 语法全部不匹配时调用
    public virtual IReadOnlyList<CommandSyntax> Syntaxes;              // 类型化语法表（字符串 Syntax 惰性派生）
    public virtual IReadOnlyList<Command> Subcommands;                 // 子命令（name <sub> … 分派）
    public virtual Task ExecuteAsync(CommandContext, CancellationToken);
    protected void AddSyntax(CommandExecutor, params Argument[]);      // 子类声明语法
    protected void AddSubcommand(Command);
}
// 命名空间：Momoka.Core.Commands（Command/Builder/Context/Manager/Syntax/Result）
//           ├─ .Arguments  Argument / Argument<T> / ArgumentType / 具体参数类型（逐文件平铺）
//           └─ .Parsing    ArgumentParser / ArgumentQueryResult / CommandParser / CommandQueryResult
public delegate Task CommandExecutor(CommandContext context, CancellationToken ct);
public sealed class CommandContext { Name; RawArguments; Arguments; Get<T>/Get(Argument)/Has/TryGet; }
public sealed class CommandManager { Register/Unregister/GetCommand/Commands/ExecuteAsync × 2; }
public enum CommandResult { Success, Unknown, InvalidSyntax, ExecutorException, Cancelled }

// 构建器（Minestom 风格，类型化参数）
public sealed class CommandBuilder
{
    CommandBuilder(string name);
    CommandBuilder Alias/Aliases/Description/DefaultExecutor/Subcommand;
    CommandBuilder Syntax(CommandExecutor, params Argument[]);         // 类型化参数表
    CommandBuilder Syntax(string format, CommandExecutor);             // 迷你语言糖
    Command Build();
}
public static class ArgumentType { Literal/Word/String/StringArray/Boolean/Integer/Double/Enum<T>; }
public abstract class Argument : ArgumentParser { Id; IsOptional; DefaultValue; SyntaxString; }
public abstract class Argument<T> : Argument { bool TryParse(string, out T); Optional(); WithDefaultValue(T?); }
public sealed class CommandSyntax { Executor; Arguments; SyntaxString; }

// 解析层（Momoka.Core.Commands.Parsing）
public abstract class ArgumentParser { ArgumentQueryResult Parse(string input); }   // token → 类型化值
public readonly record struct ArgumentQueryResult(bool Matched, object? Value, string? Error);
public static class CommandParser
{
    (string Name, string[] Arguments) ParseLine(string line);          // 词法：整行分词
    CommandQueryResult Query(IReadOnlyList<CommandSyntax>, string[]);  // 依序尝试语法 → 命中结果
}
public sealed class CommandQueryResult { Matched; Syntax; Arguments; RawArguments; Hit(...) / NoMatch; }
```

- **类型化参数**（对应 Minestom `Argument<T>` 家族，终端向裁剪）：`LiteralArgument`（固定文本、不产生值）、`WordArgument`/`StringArgument`（token → string）、`StringArrayArgument`（变长，消费剩余全部 token）、`BooleanArgument`（true/false）、`IntegerArgument`/`DoubleArgument`（可选 Min/Max 区间）、`EnumArgument<TEnum>`。执行器经 `ctx.Get(argument)` / `ctx.Get<T>(argument)` 取类型化值。`FlagArgument` 已去除（终端以位置布尔 / 子命令表达开关，不用 `--` 标志）。
- **可选参数**：`Optional()` / `WithDefaultValue(v)`（同时标记可选）；可选参数须全部尾随（否则 `IllegalCommandStructureException` fail-fast），同一语法内参数 id 不得重复。
- **迷你语言糖**：`<必需> [可选]`，`<x...>`/`[x...]` 变长；`CommandSyntax.FromFormat` 展开为类型化参数表，与手工构建同路径。输入分词支持单/双引号；任何 `--` 前缀 token 视为未知语法 → `InvalidSyntax`。
- **执行流水**：查找（本名/别名，忽略大小写）→ 未注册 → `Unknown` → 子命令分派（首 token 命中）→ 依声明序尝试语法（匹配失败尝试下一条）→ 命中执行器（异常隔离 → `ExecutorException`；取消 → `Cancelled`）→ `Success`；全不匹配 → 默认执行器，无默认执行器 → `InvalidSyntax`。
- **终端向定案（2026-08-26）**：无发起者抽象——`ICommandSender`/`ConsoleSender`/`Roles`/`RequiredRole`/`CommandCondition`/`CanExecute`/`PreconditionFailed` 全部去除。纯本地终端只有一个调用方；权限鉴权归宿主（Security 期），输出通道由执行器自行捕获（如宿主传入的 `TextWriter`/日志器），命令模型只管解析 + 分派 + 返回 `CommandResult`。
- **与 Minestom 对应**：`CommandBuilder`/`Command` ≈ `builder.Command`、`CommandSyntax` ≈ `CommandSyntax`、`Argument`/`ArgumentType` ≈ `arguments` 家族、`CommandExecutor` ≈ executor 回调、`CommandContext` ≈ 其 `CommandContext`、`CommandResult` ≈ `ExecutableCommand.Result`、`CommandManager` ≈ `CommandManager`。
- 内置 `help`/`plugins`/`status`、CLI/终端/AI 工具面接入：宿主接线期实现（契约延续）。

## 10. 目标能力面（后续迭代契约草图）

> 本期只定义契约，不实现。
- **Scheduling**：`IScheduler.ScheduleOnce/ScheduleRecurring`；任务持久化；可包装 Command 调用（「8 点执行 lights.on」）
- **Notifications**：`INotificationService.NotifyAsync`；severity/dedupeKey/目标；通道抽象（终端=网关通道）；离线入队待送达
- **Profiles**：`IProfileService`（家庭成员 id/name/avatar/role/preferences/presence）；与连接身份、Security、Ai 记忆联动
- **State/Context**：`IStateStore.Publish(entityId, key, value[, ttl])` / Get / Subscribe；不透明类型化值；Home 发布设备状态、Profiles 发布在场、Sense 汇入传感器，Ai 消费（决策依据）
- **Security**（定案）：`ISecurityGuard.RegisterPolicy` / `AuthorizeAsync`；机制在 Core、规则由插件注册；危险操作拦截

**数据流**：Sense 采集→标准化→State；Home 设备状态→State；用户指令/LLM 意图→Commands（经 Security 校验）→设备执行；Scheduling→定时触发 Command/事件；事件→Notifications→终端（网关通道）；Profiles↔连接身份↔Security↔Ai 记忆。

**边界**：Memory/LLM/Agentic→Ai；家庭模型/设备语义→Home；感知采集→Sense；传输（网关设施）→下一期。

## 11. Ui 传输范式（Core 网关设施）

**单路由（通用操作路由）** 已落地（`Momoka.Core/Gateway/`，命名空间 `Momoka.Core`）：一个 SignalR Hub 承载操作与事件，不采用插件内手写传输中间件（Envelope 已删除的定案不变）。网关设施本身位于 Core（宿主设施），插件只注册操作与订阅事件。

```csharp
// 信封与线协议（STJ 全局 snake_case，GatewayJson.Options 一统：信封 + 载荷）
public sealed record OperationRequest(string OperationId, JsonNode? Payload);       // 操作（客户端→服务器 request/response）
public sealed record OperationResponse(bool Success, JsonNode? Payload, string? Error);
public sealed record ClientEvent(string EventId, JsonNode? Payload);                // 线上事件（双向 fire-and-forget）
public sealed record TerminalInfo(string ConnectionId, string TerminalId, string Role, DateTimeOffset ConnectedAt);
public sealed record OperationContext(string OperationId, TerminalInfo Caller);
public sealed class GatewayOptions { string Token; }                                 // appsettings Gateway:Token（缺省空 = 全部拒绝）

public sealed class Gateway
{
    IDisposable RegisterOperation<TRequest, TResponse>(string operationId,
        Func<OperationContext, TRequest, CancellationToken, Task<TResponse>> handler);
    IDisposable RegisterOperation<TRequest>(string operationId,
        Func<OperationContext, TRequest, CancellationToken, Task> handler);          // void 操作
    Task<OperationResponse> InvokeAsync(string operationId, JsonNode? payload,
        TerminalInfo caller, CancellationToken ct = default);
    IReadOnlyCollection<TerminalInfo> Terminals { get; }                            // 终端注册表（connectionId→TerminalInfo）
    void OnConnected(TerminalInfo terminal);  void OnDisconnected(string connectionId);
    internal Task BroadcastClientEvent(string eventId, object? payload, ...);       // EventHub wire-sender 钩子
    internal Task HandleClientEventAsync(string eventId, JsonNode? payload, ...);   // wire-in 协调
}

public sealed class GatewayHub : Hub<IGatewayClient>                                // MapHub<GatewayHub>("/hubs/gateway")
{
    Task<OperationResponse> InvokeOperation(OperationRequest request);              // 取调用者 → Gateway.InvokeAsync
    Task SendEvent(ClientEvent @event);                                             // 客户端上报 → wire-in
}
public interface IGatewayClient { Task ClientEvent(string eventId, JsonNode? payload); }  // Clients.All（v1 全员）
```

- **三通道**：操作（request/response）+ 线上事件双向（fire-and-forget，仅服务器有 EventHub 为真相）+ 进程内事件（EventHub 服务端唯一总线）。
- **鉴权与身份**：握手 query `?terminalId=&role=&token=`；token 恒定时间比较（`Gateway:Token` 缺省空 = 全部拒绝）；`TerminalRegistry` 断连清理；角色本期仅记录（Security 期授权）；操作处理器经 `OperationContext.Caller` 取调用者。
- **操作 fail-soft**：未知 operationId / handler 异常 / 反序列化失败 → 错误响应；取消 → "Cancelled"；重复注册 operationId fail-fast；插件 OnEnable 注册、OnDisable 释放令牌。
- **事件全自动化**：发布经 `[EventRouter]` + `EventHub.InvokeAsync`；监听经 `[EventSubscribe]` + `EventHub.AddSubscribers`；wire-in（`SendEvent`）经 eventId→Type 注册表反查 + `FromClients` 校验 + 反序列化进总线，**绝不自动广播回客户端**（插件处理后按需生成新事件发回）；`EventRecorder` 为被动审计 sink（ILogger）。
- **宿主接线**：`Program.cs` 为 WebApplication（`AddSignalR` + snake_case JSON 协议 + 单例 DI：ServiceRegistry/EventHub/EventRecorder/Gateway/PluginService/PluginLoader），EventHub 的 wire-sender / recorder 经 DI 工厂闭包注入；`PluginLoader.Load` 扫描 `[EventRouter]` 类型填充注册表（重复 Id fail-fast）。
- **后续**：按终端/档案定向广播（Profiles 期）、按角色授权（Security 期）、Home 领域事件挂属性 + 网关面 DTO STJ 化（HomePlugin 转换期）。

## 12. 与其它模块关系

| 模块 | 关系 |
|------|------|
| **Home** | 纯模型库，实现 `Plugin`（`HomePlugin`）作为插件由宿主托管；适配器下一期 |
| **Ai** | Agentic / Memory / LLM，独立模块（不进 Core）；亦作为插件注册 |
| **Sense** | 感知采集，输出标准化状态到 State；作为插件注册 |
| **Voice** | Python HTTP 服务（TTS），经 Commands / 网关调用 |
| **Ui** | 唯一远程边界（Godot C# .NET），下一期经 Core 网关设施单路由连接 |

## 13. 设计原则

- **SOLID**：接口 + 抽象基类 + 单一职责子模块
- **fail-fast**：校验一律前置、错误信息清晰、细分异常（InvalidPlugin / InvalidInfo / UnknownDependency）统一出口
- **零业务语义**：Core 只提供机制，语义全在模块
- **服务定位 vs DI 分工**：宿主设施走 DI，插件服务走 Registry
- **编译期统一**：同解决方案编译期类型安全；Roslyn 运行期编译 / 第三方动态加载 / 热插拔推迟（待插件生态需要时再评估）
- **简单优先**：无独立 IPluginContext、无空扩展方法、无过度抽象
