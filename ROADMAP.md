# 路线图（Roadmap）

> 本文档详细列出 Momoka 的**当前进度**与**未来计划**。每完成一项即勾选 `[x]`。
>
> 图例：`[x]` 已完成 · `[ ]` 未开始 / 进行中（括号内标注）。

> **开发优先级**：完成 Momoka.Home 后，先实现 Momoka.Ui + Momoka.Stage 把家庭管理系统跑起来，再进入 AI 伴侣阶段（Ai / Core / Sense / Voice）。

- [x] 建立 monorepo 项目骨架（`.sln`、CI、`.gitignore`、`.editorconfig`、`LICENSE`）
- [ ] Phase 0 — 基础设施
- [ ] Phase 1 — 完善 Momoka.Home
- [ ] Phase 2 — Momoka.Ui 家庭管理终端
- [ ] Phase 3 — Momoka.Stage 平台适配
- [ ] Phase 4 — Momoka.Ai 角色引擎（AI 伴侣）
- [ ] Phase 5 — Momoka.Core Agent 框架（AI 伴侣）
- [ ] Phase 6 — Momoka.Sense 感知层（AI 伴侣）
- [ ] Phase 7 — Momoka.Voice TTS 集成（AI 伴侣）
- [ ] 远期目标

---

## 当前状态一览（2026-08）

| 模块 | 完成度 | 说明 |
|------|--------|------|
| Momoka.Home | 🟡 ~70% | 空间模型 / 属性 / 序列化 / 空间查询 / 放置与附着 / 编辑命令层完成；GraphLine3D 墙体图模型、设备层 / 安全层未实现 |
| Momoka.Ui | 🔴 <10% | 仅 GDExtension 入口骨架 |
| Momoka.Stage | 🔴 <5% | 仅目录与占位 README |
| Momoka.Voice | 🟡 ~20% | HTTP 骨架完成；TTS 引擎未集成 |
| Momoka.Ai / Core / Sense | 🔴 <10% | 仅程序入口骨架 |
| 测试 / CI | 🟢 ~80% | 383 个测试全绿；CI = dotnet 构建+测试 / Godot 检查 / Python ruff |

---

## 架构决策（2026-08）

> 系统拓扑与模块通信的既定决策，落地时参照。**当前主做 Home 模块；Core 仅建了 SignalR 网关存根（`HomeService`/`IHomeClient`），宿主待 Phase 5。**

- **Momoka.Core = 插件宿主**：通过 `AssemblyLoadContext` 加载模块（每次启动加载 / 运行期热插拔），反射发现 `IMomokaModule`
- **Core 只认识通用自描述契约** `IMomokaModule`（能力表 / 事件 / 依赖 / 生命周期）；**不内置任何模块能力契约**
- **模块契约由模块自声明**：每个模块的源码包含 `contract.cs`（接口 + DTO + 事件），Core 零编写；模块对外只暴露门面接口（如 `IHomeService`），内部实现 `internal`
- **类型安全分层**：
  - 全源码模块（官方 / 核心）：Core 启动时 Roslyn 一次编译「全部契约 + 全部模块」→ 类型统一、编译期安全；编译结果缓存 DLL（源码哈希未变直接加载缓存，变了重编译）
  - 仅契约源码模块（第三方）：Roslyn 编译契约做参考类型 + 加载时逐成员签名校验 + 动态适配器转发 → 调用时安全（fail-fast，拒绝加载不合规模块）
  - 黑盒二进制模块：拒绝加载（或退回 schema 校验）
- **类型身份陷阱规避**：不"重编译接口制造类型副本"；契约程序集与模块在同一编译批次内统一
- **通信 = 本地函数调用**：模块间通过服务接口直接调用，事件走内存发布/订阅；无序列化、无状态副本
- **Core 零业务状态**：只维护「服务注册表 + 事件订阅表」两张表，数据全在模块内
- **Security 内嵌 Core**：危险命令（燃气/门锁/高压）拦截逻辑与 Core 强耦合，不作为可裁剪插件
- **Agentic 独立模块**：LLM 推理/意图/工具调用/记忆独立于 Core（或并入 Momoka.Ai）；Core 不依赖 LLM
- **Ui = 唯一远程边界**：Godot C# .NET 客户端经 **SignalR**（强类型 `Hub<IHomeClient>`）连 Core 的 Ui 网关（网关本身也按模块注册）；客户端调用端强类型安全用自写门面（`nameof` 对齐 Hub 方法名）
- **Sense 数据不属 Home**：心率/体温/心情等用户状态仅供 LLM 推理决策，不写入 Home 孪生模型
- **Home = 纯模型库**：零外部依赖、零网络，作为模块由 Core 托管
- **互操作 = SignalR（2026-08-20 定案）**：强类型 `Hub<IHomeClient>`（方式一：单 Hub + 组合客户端契约 + 服务端操作服务类）；**不手写传输中间件**——`Envelope` / `FrameRegistry` / 帧事件 / `ISubscriber` 已全部删除；跨语言非目标（C# 主语言，Ui = Godot C# .NET）
- **C# 主语言（2026-08-20 定案）**：Godot C# .NET 版可用——GDExtension（C++）插件与 C# 共存；Live2D 用自写 Cubism GDExtension 胶水（Phase 4），不阻塞选型
- **撤销 = 客户端本地（2026-08-20 定案）**：历史归客户端（记录操作参数 + 重发逆操作请求，逐个回滚、无合并），服务器不记录不重放；`CommandHistory` / `CoalescedCommand` 已删除
- **命令层 → 模型操作（方向）**：叶子命令（validate-then-apply）是模型操作的中间形态；GraphLine3D 落地时溶解为 `LevelData` / `LevelLayout` 方法（如 `BuildWall → SetVolume`）
- **Home 并发模型**：`_gate` 操作级串行化（模型非线程安全；编辑 token 保证单写者，锁隔离读写竞态）+ 变更事件一律锁外触发（网关转发 `IHomeClient`）

---

## Phase 0 — 基础设施（规划中）

- [x] 初始化 Git 仓库并完成首次提交
- [x] 创建 GitHub 仓库（momoka-ai/momoka）与 CI 工作流（`.github/workflows/ci.yml`）；`main` 分支保护规则待确认
- [x] 引入测试框架（xUnit）：`Tests/Momoka.Home/` 110 个测试；`Benchmarks/Momoka.Home/` 基准项目未建
- [ ] 完善 CI（进行中）：Godot 项目存在性检查已接入；真实 Godot 导出检查、C++ 构建（vcpkg）未接入
- [ ] 校验进 CI（进行中）：`ruff check Momoka.Voice/` 已接入；`dotnet format` 未接入
- [ ] 添加 Issue / PR 模板与自动化标签
- [ ] 配置依赖机器人（如 Dependabot）
- [ ] 首个正式版本 `0.1.0` 发布流程（tag + CHANGELOG）

## Phase 1 — 完善 Momoka.Home（进行中）

> **2026-08 大重构完成**：空间模型扁平化为单一 3D 根（`LevelLayout`）；删除旧层级
> （`Home → Level → Building`）、`Region`、`BlockGridEntity`、薄壳实体与中间件；
> 序列化管线贯通，`Layouts/` 收敛为纯运算 / 数学布局。详见 `Documentation/DESIGN_HOME.md`。

已实现：

- [x] 坐标原语：`Int2` / `Int3` / `Float3` / `Key` / `Bound`（`Primitives/`）
- [x] 属性系统：`Property` 基类 + 6 种子类型（`Boolean` / `Int` / `Float` / `String` / `Literal` / `Enum`）+ `PropertyValueChangedEventArgs`；`TextureProperty` 已删除（并入 `String`）
- [x] 实体系统（非泛型重构）：`Entity`（Id / Key / Position(Float3) / Volume / 属性 / 组件）+ `EntityTemplate` / `EntityTemplateFactory` 配置管线 + `IEntitySource`；薄壳 `Wall` / `Door` / `Window` / `Appliance` / `Curtain` 已删除，由配置模板实例化
- [x] 空间根：`LevelLayout`（`sealed`，单一扁平 3D 根，`IEntitySource` + `IVoxelSource<Entity>`）+ `LevelData`（`Momoka.Home.Levels`：类型 + 布局 + 全实体注册表，服务器/客户端共同基类）+ `LevelType` 枚举
- [x] **命名空间域化重排（2026-08-22）**：`Momoka.Home.Levels/`（域根：LevelData / LevelLayout / LevelType / Region / ChangeSet / VolumePunch + 扁平 `Collision` / `Occlusion` / `Visibility` / `Pathfinding` / `Traverse` + `Entities(+Components/Properties)` / `Volumes`（原 Geometry）/ `Layouts`（含 Graph/Subdivision）/ `Commands`）、`Momoka.Home.Runtime/`（ServerLevelData / ClientLevelData / EditorSession / IEditorCommand + `Protocol` wire DTO）、`Momoka.Home.Data/`（Json / Sqlite / Settings）、`Momoka.Home.Primitives/`；`UnitLayout`→`LevelLayout`、`UnitType`→`LevelType`；`Algorithms/` 平铺入域根
- [x] **Residence → LevelData 重构（2026-08-20）**：`Residence` 删除——名称/地址归云端账号管理（本地仅 Home 实体 address 属性）；`Type`/`Layout`/`Entities` 入 `LevelData`；`ServerLevelData`/`ClientLevelData` 共同继承（服务器权威 / 客户端镜像）；隐藏 **Home 实体**（key=home、无 Volume、生成时创建、无增删渠道）承载地址与类型（unit_type 为 Type 持久化真相）；快照协议删 `ResidenceMeta`，`Type` 直接入 `SnapshotEvent`
- [x] 体素存储：`VoxelLayout<T>`（Minecraft 式 16³ 区块 + paletted）、`GridLayout<T>`、`Subdivision<T>`（`Face` 面实体支持：`AssignEntity` / `EntityOf`）、`Graph2D`、`Palette` / `PackedBitStorage` / `PalettedContainer(RO)`；`Layouts/` 只剩纯运算 / 数学布局
- [x] 几何：`Volume` 层次 + `Box` / `Line` / `LineGraph` / `Curve` / `Polygon` / `Circle` / `Ellipse` / `Ring` / `Cylinder` / `Triangle` / `Cone` / `Pyramid` / `Sphere` / `Ellipsoid` / `Extruded` / `Composite`（带 `[JsonTypeName]`）
- [x] **2D 几何退役（2026-08-22）**：`Shape` / `Rect2D` / `Circular2D` / `Polygon2D` / `Composite2D` / `IVoxelGeometry2D/3D` 与全部 `Cells2D()` / `IsCollided(shape)` 删除；2D 光栅化降为内部 `Rasterizer`（even-odd ContainsCenter），只产 `Int2[]` 数据；`Extruded.Footprint: Shape` → `SectionCells: List<Int2>`；测试 383 全绿（+LineGraph 4 测试）
- [x] **`IVoxelSet` 格契约 + VoxelChunk 拆分（2026-08-22）**：`Layouts/VoxelSet.cs` 新增 `IVoxelSet.GetVoxelSet()`（局部占用格枚举）替代 `Volume.Cells3D()`——`Volume : IVoxelSet`，全部形状与调用点改走接口方法；`VoxelChunk` / `VoxelChunkSection` 从 `VoxelLayout.cs` 拆出到 `Layouts/VoxelChunk.cs`
- [x] 序列化管线：`JsonTypeNameRegistry` + `JsonTypeConverter` + `JsonGeometryConverter` + `JsonPropertyConverter`；`MideaVerifyTests` 用真实配置验证
- [x] 组件：`Component` + `IComponentSource` + `CommandTarget` / `DataSource` / `EventSource` / `PlacementLayoutSource`
- [x] 测试：383 个全绿（Layouts / Serialization / Shapes / Algorithms / Primitives / Properties / 放置链 / Level 编辑操作与 DTO 往返）
- [x] 旧容器清理与迁移：`Home` / `Level` / `Building`、旧 2D `Region`、`IVoxelSpaceRoot`、`PlaneLayout`、`TextureProperty`、`FloorPlanLayout` 已删除；`Region` 迁入域根（`Momoka.Home.Levels`），`LevelLayout.Regions`（`ColumnLayout<Region>`）取代 `Floors`
- [x] **Level 命名空间 + 传输层移除（2026-08-20）**：`Editing` → `Level`（含 `Commands`/`Protocol`）；手工 C/S 中间件全删——`Envelope`/`Frames`/`FrameRegistry`/`FrameTypeAttribute`/`ISubscriber`/`Topics`/帧事件/`PubSub`；`Protocol/` 只剩纯 DTO（Requests / Result / EntityDelta / SnapshotEvent）；`ServerLevelData` 从 `HandleRequest` 通用路由改为 **12 个类型化操作** + Action 式变更事件（`LayoutChanged` / `EntityCreated` / `SaveCompleted`，锁外触发）；`Core/Gateway` 建 `HomeService`（Hub）+ `IHomeClient`（契约）存根
- [x] **命令层收敛（2026-08-20）**：删除 `CommandHistory` / `CoalescedCommand` / `ICompositeCommand` / `CompositeCommand` / `RegisterEntityCommand` / `UndoRedoCommand`；`IEditorCommand` 单方法（`Execute` → ChangeSet）；9 个叶子命令只留正向 validate-then-apply；`BuildWall` / `BuildOpening` 改单命令 **create-即-place**（无暂存态）；`EditorSession` 收敛为薄执行器（无 History / Undo / Redo）；撤销归客户端本地

待实现（按当前优先级）：

- [x] **墙系统容器（2026-08-22）**：**方案 3 定案**——墙体 = 墙系统容器实体（无体积 marker，`ChildrenSource` 挂载成员）+ 独立墙段实体（Box/Line，自带高度 / 纹理 / 拆除粒度）；转角边邻接、角格归转弯段。`ChildrenSource : Component`（`List<Entity> Children` 内存真相 + `List<Guid> ChildrenIds` 持久化，装载时 `RestorePlacementFromGrid(registry)` 按 Id 重链）；`PlacementLayoutSource : ChildrenSource`（表面物件即子实体，几何推导改为 Id 重链）；级联删除 / 随移 / 反向索引统一走 `ChildrenSource`；`ClientLevelData` 对 null-volume marker 写格保护。**`LineGraph` 标记 `[Obsolete]`（暂留待退役）**——图语义上移容器组件，共享 Height 无法表达矮墙等异质段。
- [ ] **LineGraph 墙体图模型（已弃用方向）**：连续墙体 = 一个实体的 `LineGraph : Composite`（共享 Height 挤出 + 节点/边表）。**2026-08-22 定案转向方案 3（墙系统容器）后标记 `[Obsolete]`，暂留待退役**——矮墙 / 逐段纹理 / 单段拆除 / 局部增量均无法在单一实体上表达。待实现（方案 3 路径）：① 容器级操作（整墙移动 = 成员整体平移、整墙删除 = 级联销毁，复用 `CascadeOf`）② 墙段模型操作（首段建实体 / 延伸 = 加成员）③ 开洞打在单段 Box 上（现有 `VolumePunch` 直接可用）④ 挂载面逐段生成（`BuildWallSurface` 原地保留）

- [x] **3D Region 自动生成**：站立格（Up 放置面 + 净高过滤）→ `ColumnLayout` 引擎标 span（阻隔即占用，家具成洞）；`Region.BuildLayout()` + `LevelLayout.RebuildRegions()`（§5.6）；门开关 Portal 与 wall-extension 后续
- [x] **空间查询层**（`IVoxelSource<T>` 扩展 + 纯算法层）：`Algorithms/` 五类型 `Traverse`（OnLine/InCone/InFrustum）/ `Visibility`（Project/IsInView）/ `Occlusion`（四档阻挡）/ `Collision.Result` / `Pathfinding.AStar`；查询扩展 `CanSee`×3 / `FindItemsInView`×3（射线/圆锥/视锥，严格遮挡）/ `IsCollided`×3 / `IsOccluded` / `GetItemsInBound` / `FindPath`（失败统一 null，无 Reachable）
- [x] **体素空间负坐标支持**：`VoxelChunk` section 偏移（`_baseSy`，负 Y 可写可读）+ `LayoutChunkCodec` 世界 section Y 编码（旧文件字节兼容）；Bound 三维 ±16384 格全支持
- [x] **放置 API 重构**：`LevelLayout.Add(entity[, position])` / `Remove(entity)` / `Remove(Position)` / `Find(id|Position)` 取代 `PlaceAt`/`DestroyAt`/`FindEntity`；删除 = 回落"未放置"池（实体保留于 LevelData 注册表）且**连带回落表面物件**（实体不能悬空；删除前确认是编辑器 UI 的职责）；`PlacementLayoutSource.Entities` 表面宿主登记（回落同步清理）；实体对碰撞下沉 `Volume.Intersects`；自动寻位 `Add(Entity)` 已实现（Bound 内扫描：不碰撞 + 下方有支撑——地面或 immutable 结构）
- [x] **表面附着与朝向体系**：`Rotation`（yaw/pitch/roll，内旋 YXZ 与 Godot 一致，零转换映射）/ `Transform`（位置 + 姿态）/ `RotationAlignment`（缺省 `Upside`——未配置物件只可放朝上水平面；`Matches` 匹配规则：精确匹配 + Horizontal 接受上下）/ `PlacementLayoutSource`（Layout + Transform + 运行时登记表）；`Add(entity, pos, source)` 显式附着（编辑器经视线探测提取表面）——斜表面可附着（`Tilted` 期望生效，太阳能板放坡顶），贴合姿态由表面方向推导（渲染端处理），体素占位恒轴对齐
- [x] **具体编辑命令**：`Place` / `Remove`（含开口级联）/ `Move` / `Rotate` / `SetProperty`（含贴图，createIfMissing）/ `BuildWall` / `BuildOpening` 叶子命令，validate-then-apply、无历史（撤销归客户端本地）；级联删除基础（`Remove(entity, cascade)` + 表面宿主登记）已就绪
- [ ] **`LevelLayout.Rebuild` 重写（已弃用）**：低层直接写格改为受控通道（事件 / 脏格跟踪），不再事后全量重栅格化；重写后移除
- [ ] **门洞/Portal 连通性**：门开度属性 → Region 连通性重算（Home 侧空间语义）；**渲染归 Momoka.Ui**（Z-Index + 剖分，Home 不做）
- [ ] **参数化 `Shape` 体系**：屋顶 = 实体模板（`key=rooftop`）+ 现有 Shape 族（`Extruded` 坡面 / `Conic3D` 锥顶 / `Composite` 组合），Pitch/Overhang 即截面顶点参数——无需新类型体系，模板配置示例即可；网格生成归 Momoka.Ui
- [ ] **设备抽象层 `Providers`**：`IDeviceProvider` + `ProviderRegistry` + HomeAssistant 实现、GIIC 协议桥接
- [ ] **设备配置 JSON**：`/devices/` 目录，以 JSON 声明第三方设备（实体配置管线已就绪，Provider 驱动待接入）
- [ ] **安全约束 `Security`（L3–L4）**：Blackboard + 规则评估，拦截燃气 / 门锁 / 高压等危险操作
- [ ] **DSL 安全规则**：复杂约束的表达式解析
- [ ] **Build 管线**：视频流 → 3D 重建 → 网格（消费结构化户型数据）
- [x] **墙体开口宿主 + 级联删除**：门窗 / 吊灯挂载到墙 / 天花板实体；删除宿主 → 连带回落表面物件。**实现**：不引入实体树——`PlacementLayoutSource.Entities` 登记"摆在表面上的物件"，`Add` 拒绝已放置实体（宿主即自身 / 重复放置）保证 Items 恒为森林（无环不变量），`Remove(entity)` 递归回落依赖链；编辑命令层入口（`PlaceEntityCommand` 等）随命令层实现
- [ ] **多形态设备（低优先级）**：`DeviceShell` + 具象形态（静态网格占用 ↔ 移动连续位置），`Activate` / `Deactivate` 生命周期，身份贯穿形态切换（如扫地机器人）
- [x] **Palette 策略减法**：`Int2/Int3ChunkStrategy` 等暂留，待稳定后清理未用策略（2026-08-13：删除 `Int3ColumnSpanStrategy` / `Int3DenseStrategy` / `Int2DenseStrategy` 三个零引用策略）
- [x] **撤销/重做定案**：**客户端本地历史**（记录操作参数 + 重发逆操作请求，服务器不记录不重放、逐个回滚无合并）；Phase 2 Ui 实现客户端侧
- [x] **Palette / PalettedContainer / PackedBitStorage 直接序列化**：四个类型各自挂 `[JsonConverter]`，产出 `palette_json + bits + data` 填表载荷，去掉手写字节 codec（2026-08-13 简化落地：`Palette<T>` 挂 `JsonPaletteConverter`（Entity 写 Guid 引用）+ `PackedBitStorage.ToBytes/FromBytes` little-endian BLOB；`LayoutChunkCodec` 退役随 Sqlite 体素层）
- [x] **Sqlite 存储层 · 三表合一（`Data/Sqlite`，2026-08-20）**：`linq2db` + `Microsoft.Data.Sqlite`，单文件存档（每服务器唯一，`ListSaves` 删除）；一次事务原子写入 **`Entities`**（Id + Json 每实体一行，含 Home 实体）+ **`Chunks`**（x, z + 整 chunk 载荷——paletted sections + region spans 一体编码，复用 `LayoutChunkCodec.Encode/Decode`）+ **`RegionNames`**（id + name；几何从 chunk spans 重算——单一真相）；`LayoutChunkCodec`/`RegionsCodec` 文件层退役；`LevelData.Type` 经 Home 实体 unit_type 属性持久化
- [ ] **区块数据压缩**：section words（`ulong[]`）gzip 压缩后存 BLOB（对齐 Minecraft region 文件的 zlib 做法，稀疏区块收益大）
- [ ] **物业 / 管理方引用层（推迟）**：统一管理多 Unit 的引用式封装（住户 Level 默认全权，物业另层且不可见住户内容）
- [ ] **传播图 `Propagation`（远期 · AI 伴侣阶段，低优先级）**：Region 图上的加权 Dijkstra（节点=房间 Region，边=门户开/关/墙，代价=距离 + 穿墙损耗），一次算完供三类感知：① 灯可见性（同 Region 或开着的门连通 → 可感知，用于自动关用户不可见的灯）② 声音传播（最短路径长度 + 墙衰减 → 按用户位置自动调音量）③ WiFi 信号（log-distance path loss + 穿墙惩罚 → 每房间信号表）。**不依赖体素 LOS**——光/声/无线电能绕弯、是连续衰减而非二值可见；`CanSee`/`Occlusion` 仅留给摄像头视野/投影遮挡等真二值场景。用于光线 / 声音 / 无线信号推断，属 AI 伴侣阶段的感知增强，非初期工程
- [ ] 补充单元测试覆盖上述功能

## Phase 2 — Momoka.Ui 家庭管理终端（未开始）

> 目标：先把家庭管理系统跑起来——终端可视化户型与设备控制。AI 伴侣相关的渲染能力（Live2D / VAD / ASR / 音频）见 Phase 4。

- [ ] 3D 家庭场景：glTF 户型加载、网格化墙壁 / 地板渲染
- [ ] **网格生成**：从 Home 的 `Shape` / `Volume`（10cm 碰撞箱）生成三角网格与材质面；Home 不保存模型 / 贴图
- [ ] 场景编辑交互：家具放置 / 选中 / 拖拽 / 旋转、材质编辑、撤销重做
- [ ] 2D UI 叠加层：设备控制面板、设置、调试 HUD
- [ ] 设备状态展示与控制（消费 Momoka.Home 数据）
- [ ] 与主机建立 SignalR 通信（Godot C# .NET 客户端，`Microsoft.AspNetCore.SignalR.Client`；`HomeService` Hub + `IHomeClient` 契约已定案）

## Phase 3 — Momoka.Stage 平台适配（未开始）

- [ ] Desktop：Windows / macOS / Linux 导出配置，窗口管理、系统托盘
- [ ] Mobile：Android / iOS 导出配置，推送通知、后台服务
- [ ] Panel：中控屏（Android 嵌入）适配

## Phase 4 — Momoka.Ai 角色交互层（未开始 · AI 伴侣阶段）

- [ ] 角色引擎：对话生成 + 角色一致性管理
- [ ] 记忆系统：对话历史、情感事件（LiteDB）
- [ ] 情感状态机：情感参数输出 → 终端 Live2D
- [ ] 对话安全过滤（L0–L2：脏话 / 角色一致性 / 情感边界）
- [ ] TTS 协调：HTTP 调用 Momoka.Voice
- [ ] 与终端建立 SignalR 通信（Godot C# .NET 客户端）
- [ ] 终端侧配套（Momoka.Ui）：Live2D 渲染（Cubism → GDExtension）、情绪参数 → 动画映射、VAD、ASR（whisper.cpp）、摄像头 / 人脸检测（ONNX）、音频 I/O（miniaudio）

## Phase 5 — Momoka.Core 中枢 / 插件宿主（未开始 · 在 Home 完成后实施）

> 依据「架构决策（2026-08）」：Core 是插件宿主 + 服务注册 + 事件总线，不再承担 Agent 逻辑（Agentic 独立）。

- [ ] `IMomokaModule` 契约 + `ModuleHost`：`AssemblyLoadContext` 加载模块 DLL，反射发现，生命周期管理（启动 / 停止 / 热插拔）
- [ ] 共享 Contracts 层：能力接口（`IHomeService` / `IAgenticService` / ...）+ 消息 DTO + 事件类型
- [ ] 服务注册表 + 事件订阅表（内存发布 / 订阅，本地函数调用）
- [ ] Ui 网关：**SignalR**（`HomeService : Hub<IHomeClient>`，按模块注册；`HomeService` / `IHomeClient` 存根已建在 `Momoka.Core/Gateway`），函数实现 + 宿主接线（`AddSignalR` + `MapHub`）待 Phase 5
- [ ] Security 内嵌：危险命令拦截（规则评估）
- [ ] 会话 / 身份 / 鉴权：Ui 连接鉴权、用户会话
- [ ] Agentic 模块（或并入 Ai）：意图识别（Ollama）、快慢通道、Agent 推理循环、工具集成（MCP 风格）、知识记忆（LiteDB）

## Phase 6 — Momoka.Sense 后台感知层（未开始 · AI 伴侣阶段）

- [ ] 可穿戴设备桥接：心率、睡眠（BLE 或厂商 Web API）
- [ ] GPS 定位（系统 API / HTTP 端点）
- [ ] 环境传感器数据（HomeAssistant API 间接获取）
- [ ] 数据标准化并输出到 Momoka.Core

## Phase 7 — Momoka.Voice TTS 集成（进行中 · AI 伴侣阶段）

- [x] FastAPI 骨架：`GET /health`、`POST /tts` 占位
- [ ] 封装 GPT-SoVITS 推理
- [ ] 封装 IndexTTS2 推理
- [ ] 返回 WAV / opus 音频流
- [ ] 多说话人 / 音色管理
- [ ] 单元测试与压力测试

---

## 远期目标

- [ ] 家庭管理系统 MVP：终端可视化户型 + 设备控制（Home + Ui + Stage）
- [ ] AI 伴侣 MVP：角色对话 + 语音 + Live2D 反馈（Ai + Core + Sense + Voice）
- [ ] 插件生态：第三方设备 / 工具 / 角色扩展
- [ ] 多语言支持（界面与语音）
- [ ] 端侧模型优化与离线能力增强
- [ ] 社区化治理：维护者体系、贡献者激励

---

## 贡献

想参与某项工作？请阅读 [CONTRIBUTING.md](CONTRIBUTING.md) 并在对应 Issue 中认领。
