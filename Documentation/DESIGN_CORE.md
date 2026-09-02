# Momoka.Core 架构设计

## 1. 总览

Momoka.Core 是**插件宿主 + 核心能力库**：提供一组**通用机制**（机制在 Core、业务语义在模块），支撑项目主旨「AI 协助/接管用户生活」。

| 子系统 | 职责 | 状态 |
|--------|------|------|
| **Plugins** | 插件契约（plugin.toml + 静态 `Build(Plugin)` 声明面）、Service\<T\>/注入、加载/启停/依赖图（PluginLoader） | ✅ 本期完成 |
| **Events** | 事件中心（EventHub，Bukkit 风格 · CRTP）：每事件类型一张静态处理器表（volatile 复制写，发布无锁）+ 注册期一次反射、触发期强类型直调；`ICancellable` 事件可被其它插件否决（置 IsCancelled；ignoreCancelled 处理器跳过）；仅服务端插件间通信，绝不跨线 | ✅ 本期完成；After 语义设计中 |
| **Registry** | 插件间服务发现表：同类型多注册、优先级/来源插件追踪 | ✅ 本期完成 |
| **Configurations** | 统一配置 + 版本迁移：不透明值树 + 版本键 + 迁移链（文件 / 二进制 / 数据库三后端） | ✅ 本期完成 |
| **Commands** | 指令定义 / 解析 / 执行（迷你语言 + 类型化参数，Minestom 风格，终端向） | ✅ 本期完成 |
| **Gateway** | Ui 网关设施：连接握手（token）+ 设备注册表（clientId 主表）；三层通信模型（Events 进程内 / Post 控制面 / Packet 数据面）契约已定案 | 🟡 连接与注册表已实现；Post/Packet 契约定案，实现下一期 |
| **Scheduling / Notifications / Profiles / State / Security** | 定时 / 通知 / 家庭成员 / 状态发布订阅 / 安全守卫 | 📋 后续迭代（契约见 §8） |

> 依赖方向：**子模块引用 Core**；Core 不引用任何子模块。本期删除 `Core→Home` 工程引用与 Home 专属网关存根（`HomeService`/`IHomeClient`），依赖方向反转。

## 2. 定位与边界

- **插件宿主**：加载运行期扩展单元（插件），管理生命周期与插件间通信机制。
- **能力库**：提供通用设施（注册表/事件中心/后续的配置/指令/调度/通知/档案/状态/安全）。
- **Core 零业务语义**（2026-08 修订）：设施可持有不透明数据（注册表/状态表/调度表/档案），但**不解释业务语义**；语义全在模块。不持有零业务状态的承诺——改为语义边界。
- **不进 Core 的清单**：Agentic / Memory / LLM（归 Momoka.Ai）；家庭模型与设备语义（归 Home）；感知采集（归 Sense）。
- **通信 = 本地函数调用**：模块间通过服务接口直接调用（服务注册表解析），事件走内存发布/订阅；无序列化、无状态副本。

## 3. 核心概念与命名（三词分层）

| 词 | 含义 | 形态 |
|----|------|------|
| **插件 Plugin** | 运行期扩展单元 | `Plugin` 子类，经 manifest 声明 |
| **模块 Module** | 静态子工程 | 如 `Momoka.Home` / `Momoka.Ai` / `Momoka.Sense`，实现插件契约即被宿主托管 |
| **服务 Service** | 能力接口 | 插件注册进服务注册表，供其它插件解析调用 |

命名空间规划：本期 `Momoka.Core.Plugins`（含服务注册表）/ `Momoka.Core.Events`（事件中心）/ `Momoka.Core.Configurations` / `Momoka.Core.Commands`（含 `Arguments` / `Parsing` 子命名空间）；网关设施位于根命名空间 `Momoka.Core`；目标另含 `Scheduling` / `Notifications` / `Profiles` / `State` / `Security`。

## 4. 依赖方向与工程结构

```
Momoka.Home ─┐
Momoka.Ai   ─┤──▶ Momoka.Core  （只依赖框架与 Tomlyn）
Momoka.Sense┘
```

```
Momoka.Core/
├── Program.cs / GatewayHostBuilder.cs      # 宿主入口：WebApplication + SignalR 网关（插件引导接线待重建）
├── Momoka.Core.csproj                      # Microsoft.AspNetCore.App + Tomlyn（无子模块引用）
├── Plugins/                                # 插件子系统（声明式生命周期：静态 Build(Plugin)）
│   ├── Plugin.cs                           # 插件声明面（身份/日志/目录 + 服务/指令/事件监听器声明）
│   ├── PluginInfo.cs / PluginState.cs / PluginExceptions.cs    # manifest / 状态 / 异常（含依赖图 PluginDependencyGraph）
│   ├── PluginLoader.cs                     # 加载（manifest → 主类静态 Build）→ 启用/停用
│   ├── ServiceInjector.cs / ServiceUsageGraph.cs   # [ServiceInjection] 注入 pass 与使用图（disable 守卫）
├── Services/                               # Service<T> 泛型静态注册表 + 注入标记
│   └── Service.cs / ServiceInjectionAttribute.cs
├── Events/                                 # 事件中心（进程内 · Bukkit 风格 · CRTP；IEventHandler<T> 单方法契约）
│   └── Event.cs / EventHub.cs / IEventHandler.cs / RegisteredHandler.cs / EventPriority.cs / ICancellable.cs / SubscribeAttribute.cs / PublishAttribute.cs
├── Gateway/                                # Ui 网关设施（Client / GatewayHub；连接握手 + 设备注册表）
├── Configurations/                         # Configuration / Migration + File/Binary/Database 三种后端
└── Commands/                               # Command / CommandExecutor / CommandManager / CommandParser
```

## 5. 插件系统

### 5.1 契约：声明式 `Plugin` + 静态 Build 入口

- **插件 = 声明数据，不控制生命周期**：`plugin.toml` 的 `main` 指向**静态入口类型**，其上须声明 `public static void Build(Plugin plugin)`。宿主构造声明面（注入身份/环境）后回调一次，插件只做声明（AddService / AddCommand / AddEventHandler）；生命周期（启用/停用/注入）完全由 `PluginLoader` 接管，无 OnEnable/OnDisable。
- **服务**：`AddService<T>(provider, overwrite: false)` 立即写入 `Service<T>` 泛型注册表（来源 = 本插件）。默认**先到先得**（后续同类型注册成为可选提供商）；`overwrite: true` 显式替换当前提供商。
- **注入**：`[ServiceInjection]` 属性注入仅作用于服务提供者实例（可空性即硬失败开关：`T?` 缺失留 null、`T` 缺失 fail-fast）；注入时记录服务使用边（disable 守卫，见 §5.4）。
- **指令与监听器是 Core 管理对象**：`AddCommand(Command)` / `AddEventHandler(object listener)`（listener 实现 ≥1 个 `IEventHandler<TEvent>`）；不参与 [ServiceInjection]。
- 专属能力按自身名称派生：`Logger`（类别 = 插件名）/ `GetPluginFolder()`（Plugins/&lt;name&gt;/）/ `GetPluginConfig()`。

```csharp
public sealed class Plugin   // 宿主注入身份与环境后的声明面
{
    public PluginInfo Info { get; }                       // manifest（身份）
    public string Name => Info.Name;  public string Version => Info.Version;
    public ILogger Logger { get; }                        // 专属日志器（类别 = 插件名）
    public IList<Command> Commands { get; }               // 指令（Core 管理）
    public IList<object> EventHandlers { get; }           // 监听器（实现 ≥1 IEventHandler<TEvent>）

    public Plugin AddService<T>(T provider, bool overwrite = false);  // → Service<T>（来源 = 本插件）
    public Plugin AddCommand(Command command);
    public Plugin AddEventHandler(object listener);
    public DirectoryInfo GetPluginFolder();               // Plugins/<name>/，按需即时生成
    public FileInfo GetPluginConfig();                    // Plugins/<name>/config.toml，按需即时生成
}

// plugin.toml main 指向的静态入口（示例）：
public static class HomePlugin
{
    public static void Build(Plugin plugin)
    {
        plugin.AddService<INavigationService>(new NavService());
        plugin.AddCommand(new MoveCommand());
        plugin.AddEventHandler(new MovementListener());   // class MovementListener : IEventHandler<PlacedEvent>
    }
}
```

### 5.2 plugin.toml（只读内嵌元数据，一个程序集 = 一个插件）

```toml
name = "home"                  # 必填，全局唯一
version = "1.2.3"              # 必填，SemVer 风格（string：可含预发布/构建元数据）
main = "Momoka.Home.HomePlugin, Momoka.Home"   # 必填，静态 Build(Plugin) 入口类型全名（string：程序集加载后惰性解析）
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

不再有顶层 `Config/` / `Data/` 目录；插件根目录硬编码于基目录（`AppContext.BaseDirectory`，可经配置 `Plugins:BaseDirectory` 覆写）之下；`Plugins/` 目录启动时自动创建。无 `PluginLoaderOptions`。

### 5.4 PluginLoader 流程

**Load(path)**：加载单个插件（出错抛 `InvalidPluginException`）：
1. `Assembly.LoadFrom(path)`（失败 → fail-fast）
2. 读内嵌 `plugin.toml` → `PluginInfo`；无 → 非插件程序集（fail-fast）
3. 重复 name → fail-fast；按 `main` 解析入口类型并校验静态 `void Build(Plugin)` 签名
4. 构造 `Plugin` 声明面（注入 Info / 插件根目录 / 日志工厂）→ 调用 `Build(plugin)` 填充声明（**不实例化插件类、不调用生命周期**）→ 记录（State=Loaded）

**EnableAsync(plugin)**：服务确保注册（disable 后重建）→ `[ServiceInjection]` 注入本插件服务提供者并记录使用边 → 逐个注册 `EventHandlers` 进事件总线 → State=Enabled；注入/注册抛异常 → 回滚已生效部分并置 Failed、返回 false。

**DisableAsync(plugin)**：**disable 守卫**——本插件提供的服务仍被已启用消费者（ServiceUsageGraph 记录）使用时 fail-fast 抛 `InvalidOperationException`（须先停用消费者）；否则逐个反注册监听器 → 按服务声明类型整组 `Service<T>.Remove(plugin)` → State=Disabled。

**EnableAsync() / DisableAsync()**：构建依赖图（硬前置 `dependency` 引用未知 → fail-fast；软前置 `dependencyOptional` 可解析则构成排序边；检测环 → fail-fast）→ 拓扑排序依序启用；任一失败 → 逆序回滚并返回 false。停用走逆拓扑序。

**状态**：`Loaded → Enabled ↔ Disabled / Failed`，记于 `PluginLoader`（`GetState`）。加载器无内置状态机，**生命周期与主程序同步**；运行期单插件启停（热插拔 = 运行时启用/停用）受使用图守卫。

**实现约定**：`AssemblyResolve` 兜底探询**所有插件子目录**（跨插件引用与插件本地依赖解析）；`PluginLoader` 构造器注入插件根目录 + `EventHub`（进程内注册插件经 `RegisterPlugin`，磁盘路径经 `Load`）。

### 5.5 打包约定

- 插件项目：`<EmbeddedResource Include="plugin.toml" />` + `<IsPlugin>true</IsPlugin>` + `<PluginId>home</PluginId>`
- 仓库根 `Directory.Build.targets`：IsPlugin 项目拷贝 `$(OutDir)` 产物到 `Plugins/<PluginId>/`（子目录避免同名依赖冲突）
- `Plugins/` 目录入 `.gitignore`（含可写运行时内容）

### 5.6 校验规则与异常（全部 fail-fast）

| 异常 | 场景 |
|------|------|
| `InvalidPluginException` | 插件结构不合法：DLL 不可加载 / 入口类型不存在或未声明静态 `Build(Plugin)` / Build 抛异常 / 重复插件名 / 依赖环 |
| `InvalidInfoException` | 插件信息不合法：plugin.toml 缺失或不可读 / TOML 格式非法 / 缺关键字段 / 类型不符 |
| `UnknownDependencyException` | `dependency`（硬前置）引用未知或当前不可用（禁用）的插件 |

另：进程内 `RegisterPlugin` 重复注册同 name 抛 `InvalidPluginException`；EnableAsync 单插件注入/注册失败不包装，置 Failed 返回 false，disable 守卫直接上抛。

## 6. 服务（Service<T> 泛型静态注册表，位于 `Momoka.Core.Services`）

与事件系统同构（`Event<T>` 的镜像）：每服务类型一张静态表，volatile 复制写，解析热路径无锁直读。

```csharp
public static class Service<T> where T : class
{
    public static T? Current { get; }                                    // 当前提供商（先到先得或显式覆盖）
    public static T Resolve();  public static T? TryResolve();
    public static IReadOnlyList<T> All { get; }                          // 当前 + 可选提供商（注册序）
    public static ServiceRegistration<T>? CurrentRegistration { get; }   // 含来源（插件实例）
    public static bool TryRegister(T provider, object? source = null);   // 先到先得；后续同型注册成为 fallback
    public static void Register(T provider, object? source = null);      // 显式覆盖当前提供商（原当前降级）
    public static int Remove(object source);                             // 按来源整组移除；当前被移除自动提升 fallback
}
public sealed record ServiceRegistration<T>(T Provider, object? Source) where T : class;
```

- **语义**：首个注册成为当前提供商（先到先得）；后续注册作为可选提供商（fallback）保留，当前被移除（disable）时自动提升。`Plugin.AddService` 默认走 `TryRegister`（先到先得），`overwrite: true` 走 `Register`（显式设置服务提供者）。
- **来源 = 插件实例**：disable 时按来源整组移除，防静态表滞留。

### 6.1 注入 `[ServiceInjection]`（仅服务提供者）

```csharp
[AttributeUsage(AttributeTargets.Property)]
public sealed class ServiceInjectionAttribute : Attribute { }

// 服务提供者类内（可空性即硬失败开关）：
[ServiceInjection] public INavigationService Nav { get; set; }   // 缺失 → 注入 pass fail-fast
[ServiceInjection] public ISenseApi? Sense { get; set; }         // 缺失 → 留 null 不炸
```

- 注入时机：**全部插件 Build 完成后**、启用时（`EnableAsync` 内），跨插件服务已可解析（两相：Build 只注册不解析）。
- 仅扫描 `AddService` 注册的**服务提供者**；Command / 监听器等 Core 管理对象不参与。
- 注入时记录使用边到 `ServiceUsageGraph`（消费者插件 → 提供商插件，自注入不成边），供 disable 守卫与将来 enable 排序。

## 7. 事件中心（Events · Bukkit 风格 / CRTP）

> 进程内通信，**仅供服务端插件间通知与阻断**；不序列化、绝不跨线，跨客户端/服务端的传输由 Packet 承担（见 §11）。2026-09 定案：**CRTP 泛型事件基类 + `IEventHandler<T>` 单方法处理器接口**——签名由编译器静态保证，注册期仅一次接口枚举反射，触发期接口方法直调；每事件类型一张静态处理器表（volatile 复制写，发布热路径无锁）。

```csharp
public abstract record class Event<T> where T : Event<T>      // 事件基类（CRTP）：身份 = 类型本身，无 Name 字符串
{
    public static volatile RegisteredHandler<T>[] Handlers = ...;  // 每类型静态条目表（复制写：写侧整体换数组，读侧无锁）
    public static void Add(RegisteredHandler<T> handler);          // 复制写 + 优先级降序稳定排序（同级按注册序）
    public static void Register(IEventHandler<T> handler, object source,
        EventPriority priority = Normal, bool ignoreCancelled = false);  // EventHub 反射路由调用
    public static void Remove(object source);                      // 按来源监听者移除（复制写）
}

public interface IEventHandler<in TEvent> where TEvent : Event<TEvent>  // 处理器契约（单方法接口）
{ Task OnInvoke(TEvent e); }                                        // 实现 N 个接口 = 监听 N 类事件

public sealed class RegisteredHandler<TEvent> where TEvent : Event<TEvent>  // 注册条目（Bukkit RegisteredListener 对应物）
{   // Source(object 监听者) / Handler(IEventHandler<TEvent>) / Priority / IgnoreCancelled / InvokeAsync(TEvent) 直调 }

public interface ICancellable { bool IsCancelled { get; set; } }   // 阻断门控（Before 语义）

[AttributeUsage(AttributeTargets.Class)]
public sealed class SubscribeAttribute { EventPriority Priority; bool IgnoreCancelled; }  // 类级选项，作用于该类全部处理器接口

public sealed class EventHub
{
    void Register(object listener);     // 枚举实现的 IEventHandler<TEvent> → Event<TEvent>.Register；零接口 / 重复实例 fail-fast
    void Unregister(object listener);   // 同路径反向 → Event<TEvent>.Remove（幂等）
    Task Publish<TEvent>(TEvent e, CancellationToken ct = default) where TEvent : Event<TEvent>;
}
```

- **每事件类型一张静态处理器表，发布无锁**：`Event<T>` 泛型静态字段按构造泛型各占一份（`Event<EntityPlacedEvent>.Handlers` 独立于 `Event<WallBrokenEvent>`）；表为 **volatile 复制写**——发布直接读 volatile 数组引用并遍历（无锁、无快照分配），注册/退订整体换新数组（写侧竞争极小，插件启用由 Loader 依序串行）。并发集合只在真正合适处用：重复检测 `ConcurrentDictionary`。
- **编译期保证 + 零热路径反射**：接口化后无方法级 `[Subscribe]` 反射与签名/CRTP 运行期校验（`IEventHandler<TEvent>` 的约束与 `Task` 返回由编译器保证）；注册仅一次 `GetInterfaces()` 枚举定位事件类型；触发期 `OnInvoke` 接口直调，无反射、无装箱。
- **订阅（插件侧）**：Build 内 `AddEventHandler(listener)` 声明，`PluginLoader` 启用时 `EventHub.Register`、停用时 `Unregister`（监听器 GC）；底层机制测试与宿主直接使用。零处理器接口 / 重复实例 → fail-fast。
- **退订同路径反向**：`Unregister(listener)` 在**同一监听者实例**上枚举处理器接口 → 定位事件表 → 逐个 `Event<T>.Remove`（按来源过滤复制写）。无需全局索引——注册进哪些表，反扫就能定位哪些表（幂等）。
- **阻断（Before）**：事件实现 `ICancellable` 时，监听者置 `IsCancelled = true` 即表达否决；标记 `IgnoreCancelled` 的处理器对已取消事件跳过，**其余照常接收**（全部否决意见都能被听到，非短路），发布方在返回后检查标志决定提交/回滚。
- **异常隔离**：handler 异常一律隔离记录，绝不向发布方传播；每次发布写审计日志（Debug，EventHub 内建）。
- **传输契约退役**：原 `[Publish]` 可传输标记与 wire-sender 广播已随收口删除——事件不再跨线，跨线传输归 Packet（`PublishAttribute` 保留待 Packet 期重新定义）。
- 事件类型由插件自声明（派生自 `Event<T>`）；**Core 不定义业务事件**。

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
public static class ArgumentType { Literal/String/StringArray/Boolean/Integer/Double/Enum<T>; }
public abstract class Argument : ArgumentParser { Id; DefaultValue; WithDefaultValue(...); }
public abstract class Argument<T> : Argument { bool TryParse(string, out T); WithDefaultValue(T?); }
public sealed class CommandSyntax { Executor; Arguments; }

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

- **类型化参数**（对应 Minestom `Argument<T>` 家族，终端向裁剪）：`LiteralArgument`（固定文本）、`StringArgument`（token → string，**值内空格由输入引号控制**）、`StringArrayArgument`（**类 JSON 数组字面量** `[a, b, c]` → string[]，逗号分隔去空白，`[]` 空数组、无方括号单词为单元素）、`BooleanArgument`（true/false）、`IntegerArgument`/`DoubleArgument`（可选 Min/Max 区间）、`EnumArgument<TEnum>`。执行器经 `ctx.Get(argument)` / `ctx.Get<T>(argument)` 取类型化值。`FlagArgument` 已去除（终端以位置布尔 / 子命令表达开关，不用 `--` 标志）。
- **Argument 极简面（2026-08-30，严格对齐 Minestom）**：仅 `Id` + 缺省值 `DefaultValue`。已删除 `IsOptional`/`HasDefaultValue`/`Optional()`/`PublishesValue`/`SyntaxString`/`ConsumesRemainingTokens`/`AllowSpace`/`UseRemaining`——值内空格由引号控制（同 Minestom `ArgumentString` 的引号语义），`WordArgument` 并入 `StringArgument`。
- **固定元数（定案 2026-08-30）**：不用 `useRemaining` 式“消费剩余”——那会让多余参数的行为随命令漂移（忽略/报错/吞掉），破坏类型契约一致性（Minestom 此处设计不佳）。改为每个槽位恰好消费一个 token，token 数须落在 [必需参数数, 槽位总数] 区间，多余/缺失一律 `InvalidSyntax`；多值经类 JSON 数组字面量表达（含空格时引号包裹）。
- **可选性由 CommandSyntax 控制**：`DefaultValue ≠ null` 即可选（对应 Minestom 的 `isOptional ≔ defaultValue ≠ null`）；可选参数须全部尾随（否则 `IllegalCommandStructureException` fail-fast），缺失时由语法注入缺省值；同一语法内参数 id 不得重复。
- **迷你语言糖**：`<必需> [可选]`，`<x...>`/`[x...]` 后缀标记数组槽位（→ `StringArrayArgument`）；`CommandSyntax.FromFormat` 展开为类型化参数表，与手工构建同路径。输入分词支持单/双引号；任何 `--` 前缀 token 视为未知语法 → `InvalidSyntax`。
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

**边界**：Memory/LLM/Agentic→Ai；家庭模型/设备语义→Home；感知采集→Sense；Packet 层传输 → 下一期（见 §11）。

## 11. Ui 传输范式（三层通信模型）

> **2026-08-31 定案**。传输原语由 Core 网关设施（`Momoka.Core/Gateway/`，命名空间 `Momoka.Core`）承载；三层各有归属，不重叠、不互相模拟。行为模型（`Behavior` / `IntentSource` / `GatewayRequest` / `GatewayResponse`）与网关操作分发表（`RegisterOperation` / `RegisterQuery`）已整体删除。

### 11.1 模型总览

| 层 | 方向 | 用途 | 形态 | 关键不变量 |
|----|------|------|------|-----------|
| **Events** | 服务端进程内 | 插件间通知与阻断 | `EventHub` + `ICancellable`（§7）；After 语义设计中 | 不序列化、绝不跨线 |
| **Post/Reply** | 客户端 → 服务端 | 查询 / 快照 / 控制（控制面） | Minimal API（HTTP） | 只读，读上一 tick 一致快照 |
| **Packet** | 双向 | 状态变更与广播（数据面） | 统一信封 Send + Status，寻址 Target / Except / All | 所有写入经 Tick 队列串行 |

- **写入唯一入口 = Tick 队列**：所有包（含 `POST /api/packet` 桥接）按到达序进入 bounded 队列，由 Tick 循环串行排空——世界状态单写入点，**校验与提交、handler 与 After 订阅者、不同类型包对同一实体**的并发全部消解（无锁、无原子性顾虑）。
- **默认全量同步（无兴趣域）**：任何已提交变更一律 Except(发送者) 转发；派生更新 All 广播。
- **顺序保证归协议层**：服务端 Tick 队列保证处理顺序，出站包带 tick/seq 保证客户端应用顺序。

### 11.2 连接与身份（已实现）

- 握手 query `?clientId=&role=&token=`；token 恒定时间比较（`Gateway:Token` 缺省空 = 全部拒绝）；角色本期仅记录（Security 期授权）。
- **设备注册表**：`_devices`（clientId → `Client` 主表）+ `_connections`（connectionId → clientId 索引）；`Client` 为纯设备记录（`ClientId / Role / ConnectedAt / ConnectionId`），ConnectionId 只是当前可达路径（重连即变），"谁在使用该设备"由后续 Profile 模型承载。
- **重连竞态**：`OnDisconnected` 仅当断开连接是设备当前路径时才移除设备；同 clientId 重连即替换路径。
- 网关单例 `OnConnected / OnDisconnected / GetClient / Clients`；`GatewayHostBuilder` 走 DI 接线（token 直读配置），Hub 仅做握手 + 连接注册（无业务请求方法）。

### 11.3 Packet 数据面（契约定案，实现下期）

**Packet = 权威状态变更**：客户端收到即应用（无后续协商）。统一信封：

```csharp
public sealed record Packet
{
    string Id;              // 客户端生成，请求关联
    string Type;            // 契约名（= 类型 FullName 或显式注册名）
    JsonNode? Data;         // 载荷
}
public enum PacketStatus { Ok, Rejected, NotFound, InvalidArgument, Unauthorized, … }
public sealed record PacketStatusResult(PacketStatus Status, IReadOnlyList<string> Reasons, JsonNode? Payload);
```

**通用流水**（所有写操作包一致，①②③④ 为 §11.4 示例的引用点）：

```
客户端 → 网关（Target 请求包）
  → Tick 队列入队（bounded channel 背压，防单客户端刷爆）
  → Tick 循环串行排空 → MapPacket<T> 路由到插件 handler
      ① 校验（entity 有效 / position 在 bound 内 / 无 uuid 冲突…）
      ② Before 事件（ICancellable，同步 Publish，任一订阅者置 IsCancelled = true 即否决 → Rejected）
      ③ 提交（世界状态本 tick 内原子）
      ④ After 反应（新事件系统定案后接入；仍在 tick 内串行）
  → tick 末批量 flush 出站（全部带 tick/seq）：
      Target：Status 回请求者（可带权威结果 payload，如吸附后位置 / 分配的 id）
      Except：转发原包给其它客户端（统一策略，handler 无需声明 Forward）
      All：派生广播（体素等；变更多时批量合并为 VoxelChunkPacket）
客户端：按 tick/seq 顺序应用权威数据（apply 幂等；检测空洞 → Post 补基线）
```

handler 是 Minimal API / MediatR 风格（`MapPacket<T>` 类型化注册，插件 Build 内声明，届时挂载 Packet 处理器接口）：

```csharp
gateway.MapPacket<EntityPlacePacket>(async (PacketContext ctx, EntityPlacePacket p) =>
{
    var before = new EntityPlaceBefore(p.Entity, p.Position);
    await bus.Publish(before);                              // ② Before：可被其它插件否决
    if (before.IsCancelled) return PacketOutcome.Rejected();

    store.Place(p.Entity, p.Position);                      // ③ 提交（本 tick 内原子）
    // ④ After 反应（新事件系统定案后接入）

    return PacketOutcome.Ok(payload: store.Snapshot(p.Entity)); // Target 回执
    // + 自动 Except(发送者) 转发 + 声明式 All 派生广播
});
```

### 11.4 完整示例：Home 插件墙体操作（拉墙与放置）

> 客户端从一个现有墙体节点**拉出新的墙并放置**，拆为多个原子操作。每个操作都走 §11.3 通用流水；下表只列各操作的语义（①-④ 对应通用流水步骤）。

**① 实体模板实例化（从节点拉出墙体模板）**

| # | 参与方 | 动作 |
|---|--------|------|
| 1 | 客户端 → 服务端 | Target：`EntityTemplateInstantiationPacket(template = 墙体模板)` |
| 2-3 | 网关 → Home | 入队 → Tick 串行 → `MapPacket` 路由到 Home 执行器 |
| 4-5 | Home | ① 校验（无 uuid 冲突、template 有效）→ ② Before（可阻断）→ ③ 提交 |
| 6 | 服务端 → 客户端 | Target：`Status = Ok`（可带实例化后的权威实体 id） |
| 7 | 服务端 → 其它客户端 | Except：转发 `EntityTemplateInstantiationPacket` |
| 8 | 客户端 | 收到转发包，权威数据直接应用 |

**② 实体放置（点墙体节点 → 原地新建墙体）**

| # | 参与方 | 动作 |
|---|--------|------|
| 1 | 客户端 → 服务端 | Target：`EntityPlacePacket(entity = 新墙体, position = 节点位置)` |
| 2-3 | 网关 → Home | 入队 → Tick 串行 → 路由 |
| 4 | Home | ① 校验（entity 有效、position 在 bound 中） |
| 5 | Home | ② Before `EntityPlaceBefore`（可阻断）→ ③ 提交 |
| 6 | Home | ④ After `EntityPlacedEvent`（After 语义，随新事件系统定案；放置已确定、不可取消） |
| 7 | 服务端 → 客户端 | Target：`Status = Ok` |
| 8 | 服务端 → 其它客户端 | Except：转发 `EntityPlacePacket` |
| 9 | 客户端 | 权威数据直接应用 |
| 10 | Home → 服务端 | After 异步派生 `VoxelChunkEvent` → **All** 广播 `VoxelPacket`（变更多则批量 `VoxelChunkPacket`） |
| 11 | 客户端 | 权威数据直接应用 |

**③ 实体重新放置（Relocate）**

- 与 ② 同一流水，但**合并为一个原子操作**：单个 `EntityRelocatePacket` + 单个 `EntityRelocateBefore`（一次阻断覆盖全体）→ ③ 提交 → ④ After `EntityRelocateEvent`。客户端也只收一条 Relocate 包——避免 Break + Place 两次事件/两次包造成的部分生效与状态闪烁。派生体素更新同 ② 步骤 10-11。

**④ 实体组件更新（如更新形状组件）**

- 同一流水，使用 `EntityComponentPacket`（与 ② 同构；组件派生更新视需要走 All 广播）。

**示例体现的模型要点**

- 每个"原子操作" = 一个 Packet：校验、Before 阻断、提交、After 反应、Target 回执、Except 转发、All 派生广播全部收束在同一 tick 内 → 因果序天然成立。
- 客户端永远只做两件事：发 Target 请求、按 tick/seq 应用权威数据（自己的回执 + 别人转发的包 + 派生广播），无本地裁决。

### 11.5 Post/Reply 控制面（契约定案，实现下期）

Minimal API（HTTP），主要面向第三方跨语言程序；语义为"读 / 控制"而非"写 / 变更"：

- `POST /api/world/snapshot` → `{ tick, state }`（读**上一 tick 提交后**的一致快照，绝不在 handler 执行中途读）
- `POST /api/world/replay?fromTick=N` → Packet 日志（断线重连补包：快照 + 追包）
- `POST /api/packet` → 任意 Packet 桥接进**同一 Tick 队列**（第三方客户端与 WebSocket 客户端等权）

### 11.6 断线重连

客户端重连 = POST 快照（`{tick, state}`）+ replay 追包（`fromTick`），回到 tick/seq 正常应用；重连期间不产生状态分歧（服务端权威、客户端只追）。

### 11.7 实现状态

- ✅ 已实现：连接握手 / 设备注册表 / 网关 HostBuilder 接线；事件总线收口为最小发布/订阅基座（订阅 / 顺序分发 / 异常隔离 / 审计日志 / `ICancellable` 阻断）。
- 📋 契约定案、下期实现：Events After 语义（新事件系统设计）、Packet 信封与 `MapPacket<T>` 路由、Tick 队列、Post 控制面端点、客户端 tick/seq 与重连追包。

## 12. 与其它模块关系

| 模块 | 关系 |
|------|------|
| **Home** | 纯模型库，实现 `Plugin`（`HomePlugin`）作为插件由宿主托管；适配器下一期 |
| **Ai** | Agentic / Memory / LLM，独立模块（不进 Core）；亦作为插件注册 |
| **Sense** | 感知采集，输出标准化状态到 State；作为插件注册 |
| **Voice** | Python HTTP 服务（TTS），经 Commands / 网关调用 |
| **Ui** | 唯一远程边界（Godot C# .NET），经 Core 网关设施连接（`/hubs/gateway`，三层通信模型：Packet 数据面 + Post 控制面） |

## 13. 设计原则

- **SOLID**：接口 + 抽象基类 + 单一职责子模块
- **fail-fast**：校验一律前置、错误信息清晰、细分异常（InvalidPlugin / InvalidInfo / UnknownDependency）统一出口
- **零业务语义**：Core 只提供机制，语义全在模块
- **服务定位 vs DI 分工**：宿主设施走 DI，插件服务走 Registry
- **编译期统一**：同解决方案编译期类型安全；Roslyn 运行期编译 / 第三方动态加载 / 热插拔推迟（待插件生态需要时再评估）
- **简单优先**：无独立 IPluginContext、无空扩展方法、无过度抽象
