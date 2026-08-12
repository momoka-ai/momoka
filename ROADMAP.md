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
| Momoka.Home | 🟡 ~60% | 空间模型 / 属性 / 序列化核心完成；设备层 / 安全层未实现 |
| Momoka.Ui | 🔴 <10% | 仅 GDExtension 入口骨架 |
| Momoka.Stage | 🔴 <5% | 仅目录与占位 README |
| Momoka.Voice | 🟡 ~20% | HTTP 骨架完成；TTS 引擎未集成 |
| Momoka.Ai / Core / Sense | 🔴 <10% | 仅程序入口骨架 |
| 测试 / CI | 🟢 ~60% | 125 个测试全绿；CI = dotnet 构建+测试 / Godot 检查 / Python ruff |

---

## 架构决策（2026-08）

> 系统拓扑与模块通信的既定决策，落地时参照。**当前只做 Home 模块，暂不写 Core 相关代码。**

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
- **Ui = 唯一远程边界**：Godot/C++ 无法进程内引用 .NET，经 WebSocket/MessagePack 连 Core 的 Ui 网关（网关本身也按模块注册）
- **Sense 数据不属 Home**：心率/体温/心情等用户状态仅供 LLM 推理决策，不写入 Home 孪生模型
- **Home = 纯模型库**：零外部依赖、零网络，作为模块由 Core 托管

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

> **2026-08 大重构完成**：空间模型扁平化为单一 3D 根（`UnitLayout`）；删除旧层级
> （`Home → Level → Building`）、`Region`、`BlockGridEntity`、薄壳实体与中间件；
> 序列化管线贯通，`Layouts/` 收敛为纯运算 / 数学布局。详见 `Documentation/DESIGN_HOME.md`。

已实现：

- [x] 坐标原语：`Int2` / `Int3` / `Float3` / `Key` / `Bound`（`Primitives/`）
- [x] 属性系统：`Property` 基类 + 6 种子类型（`Boolean` / `Int` / `Float` / `String` / `Literal` / `Enum`）+ `PropertyValueChangedEventArgs`；`TextureProperty` 已删除（并入 `String`）
- [x] 实体系统（非泛型重构）：`Entity`（Id / Key / Position(Float3) / Volume / 属性 / 组件）+ `EntityTemplate` / `EntityTemplateFactory` 配置管线 + `IEntitySource`；薄壳 `Wall` / `Door` / `Window` / `Appliance` / `Curtain` 已删除，由配置模板实例化
- [x] 空间根：`UnitLayout`（`sealed`，单一扁平 3D 根，`IEntitySource` + `IVoxelGeometry3D`）+ `Residence`（总容器：Name / Address / Type / Layout / Entities / Surfaces / Components）+ `UnitType` 枚举
- [x] 体素存储：`VoxelLayout<T>`（Minecraft 式 16³ 区块 + paletted）、`GridLayout<T>`、`Subdivision<T>`（`Face` 面实体支持：`AssignEntity` / `EntityOf`）、`Graph2D`、`Palette` / `PackedBitStorage` / `PalettedContainer(RO)`；`Layouts/` 只剩纯运算 / 数学布局
- [x] 几何：`Volume` / `Shape` + `IVoxelGeometry2D/3D` + `Box3D` / `Line3D` / `Curve3D` / `Polygon3D` / `Prism3D` / `Conic3D` / `Spherical3D` / `Extruded3D` / `Composite3D` / `Rect2D` / `Polygon2D` / `Circular2D` / `Composite2D`（带 `[JsonTypeName]`）
- [x] 序列化管线：`JsonTypeNameRegistry` + `JsonTypeConverter` + `JsonGeometryConverter` + `JsonPropertyConverter`；`MideaVerifyTests` 用真实配置验证
- [x] 组件：`Component` + `IComponentSource` + `CommandTarget` / `DataSource` / `EventSource` / `PlacementLayoutSource`
- [x] 测试：125 个全绿（Layouts / Regions / Serialization / Shapes）
- [x] 旧容器清理与迁移：`Home` / `Level` / `Building`、旧 2D `Region`、`IVoxelSpaceRoot`、`PlaneLayout`、`TextureProperty`、`FloorPlanLayout` 已删除；`Region` 迁主命名空间（`Momoka.Home`），`UnitLayout.Regions`（`ColumnLayout<Region>`）取代 `Floors`

待实现（按当前优先级）：

- [x] **3D Region 自动生成**：站立格（Up 放置面 + 净高过滤）→ `ColumnLayout` 引擎标 span（阻隔即占用，家具成洞）；`Region.BuildLayout()` + `UnitLayout.RebuildRegions()`（§5.6）；门开关 Portal 与 wall-extension 后续
- [ ] **传播图 `Propagation`（远期，2026-08-12 规划）**：Region 图上的加权 Dijkstra（节点=房间 Region，边=门户开/关/墙，代价=距离 + 穿墙损耗），一次算完供三类感知：① 灯可见性（同 Region 或开着的门连通 → 可感知，用于自动关用户不可见的灯）② 声音传播（最短路径长度 + 墙衰减 → 按用户位置自动调音量）③ WiFi 信号（log-distance path loss + 穿墙惩罚 → 每房间信号表）。**不依赖体素 LOS**——光/声/无线电能绕弯、是连续衰减而非二值可见；`CanSee`/`Occlusion` 仅留给摄像头视野/投影遮挡等真二值场景
- [ ] **具体编辑命令**：`PlaceEntityCommand` / `RemoveEntityCommand`（含开口级联）/ `BuildWallCommand` / `PaintTileCommand`（刷材质面），接入 `CommandHistory`
- [ ] **门洞渲染**：开门时渲染覆盖墙并允许连通性计算
- [ ] **参数化 `Shape` 体系**：屋顶形状 `Flat / Shed / Gable / Hip / Conical`（`Pitch` / `Overhang` 参数化）；网格生成归 Momoka.Ui
- [ ] **设备抽象层 `Providers`**：`IDeviceProvider` + `ProviderRegistry` + HomeAssistant 实现、GIIC 协议桥接
- [ ] **设备配置 JSON**：`/devices/` 目录，以 JSON 声明第三方设备（实体配置管线已就绪，Provider 驱动待接入）
- [ ] **安全约束 `Security`（L3–L4）**：Blackboard + 规则评估，拦截燃气 / 门锁 / 高压等危险操作
- [ ] **DSL 安全规则**：复杂约束的表达式解析
- [ ] **Build 管线**：视频流 → 3D 重建 → 网格（消费结构化户型数据）
- [ ] **墙体开口宿主 + 级联删除**：门窗 / 吊灯挂载到墙 / 天花板实体（`Entity` 父子层级）；删除墙 → 级联删除
- [ ] **多形态设备（低优先级）**：`DeviceShell` + 具象形态（静态网格占用 ↔ 移动连续位置），`Activate` / `Deactivate` 生命周期，身份贯穿形态切换（如扫地机器人）
- [ ] **Palette 策略减法**：`Int2/Int3ChunkStrategy` 等暂留，待稳定后清理未用策略
- [ ] **编辑器撤销/重做**：`EditorCommand` / `CommandHistory` 已删除（未到编辑器阶段），待 Phase 2 前重建
- [ ] **Palette / PalettedContainer / PackedBitStorage 直接序列化**：四个类型各自挂 `[JsonConverter]`，产出 `palette_json + bits + data` 填表载荷，去掉手写字节 codec
- [x] **Sqlite 存储层 · residence/entities（`Data/Sqlite`）**：`linq2db` + `Microsoft.Data.Sqlite`，单文件存档 `Saves/<Name>.db`；`Residence`（Id + Json 整存）+ `Entities`（Id + Json 每实体一行），PascalCase；`SqliteStore` 持有连接（IDisposable），全函数式 API（`CreateTable` / `InsertOrReplace` / `Insert` / `GetTable`），替代文件夹的 `Residence.json` / `Entities.json`
- [ ] **Sqlite 存储层 · 体素层**：`regions`（id + name）/ `chunks` + `chunk_sections`（x, z, sy + palette JSON + bits + data BLOB，按 (x,z) 键查询）；一次事务原子写入，替代 `LayoutChunkCodec` / `RegionsCodec` 的文件层
- [ ] **区块数据压缩**：section words（`ulong[]`）gzip 压缩后存 BLOB（对齐 Minecraft region 文件的 zlib 做法，稀疏区块收益大）
- [ ] **物业 / 管理方引用层（推迟）**：统一管理多 Unit 的引用式封装（住户 Residence 默认全权，物业另层且不可见住户内容）
- [ ] 补充单元测试覆盖上述功能

## Phase 2 — Momoka.Ui 家庭管理终端（未开始）

> 目标：先把家庭管理系统跑起来——终端可视化户型与设备控制。AI 伴侣相关的渲染能力（Live2D / VAD / ASR / 音频）见 Phase 4。

- [ ] 3D 家庭场景：glTF 户型加载、网格化墙壁 / 地板渲染
- [ ] **网格生成**：从 Home 的 `Shape` / `Volume`（10cm 碰撞箱）生成三角网格与材质面；Home 不保存模型 / 贴图
- [ ] 场景编辑交互：家具放置 / 选中 / 拖拽 / 旋转、材质编辑、撤销重做
- [ ] 2D UI 叠加层：设备控制面板、设置、调试 HUD
- [ ] 设备状态展示与控制（消费 Momoka.Home 数据）
- [ ] 与主机建立 WebSocket + MessagePack 通信

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
- [ ] 与终端建立 WebSocket + MessagePack 通信
- [ ] 终端侧配套（Momoka.Ui）：Live2D 渲染（Cubism → GDExtension）、情绪参数 → 动画映射、VAD、ASR（whisper.cpp）、摄像头 / 人脸检测（ONNX）、音频 I/O（miniaudio）

## Phase 5 — Momoka.Core 中枢 / 插件宿主（未开始 · 在 Home 完成后实施）

> 依据「架构决策（2026-08）」：Core 是插件宿主 + 服务注册 + 事件总线，不再承担 Agent 逻辑（Agentic 独立）。

- [ ] `IMomokaModule` 契约 + `ModuleHost`：`AssemblyLoadContext` 加载模块 DLL，反射发现，生命周期管理（启动 / 停止 / 热插拔）
- [ ] 共享 Contracts 层：能力接口（`IHomeService` / `IAgenticService` / ...）+ 消息 DTO + 事件类型
- [ ] 服务注册表 + 事件订阅表（内存发布 / 订阅，本地函数调用）
- [ ] Ui 网关：WebSocket / MessagePack 远程边界，按模块注册
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
