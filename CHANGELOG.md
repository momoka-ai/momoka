# 更新日志（Changelog）

本项目的所有显著变更都将记录在此文件中。

格式基于 [Keep a Changelog](https://keepachangelog.com/zh-CN/1.1.0/)，版本号遵循 [语义化版本](https://semver.org/lang/zh-CN/)。

## [未发布]（Unreleased）

> 本版本变更**按模块分节**记录，分类与提交类型体系对应（见 [CONTRIBUTING.md](CONTRIBUTING.md)）。

### Docs（文档与工程基础设施）

#### 新增

- 建立 monorepo 项目脚手架与基础配置：`Momoka.sln`、`.editorconfig`、`.gitignore`、`LICENSE`（AGPLv3）、CI 工作流 `.github/workflows/ci.yml`
- 新增文档套件：`README.md`（中文）/ `README.en.md`（英文）、`ROADMAP.md`、`CONTRIBUTING.md`、`CODE_OF_CONDUCT.md`、`SECURITY.md`、`CHANGELOG.md`
- 新增 Issue / PR 模板（`.github/`）

#### 变更

- 重写根目录 `README.md`：新增「当前进度」「系统架构（Mermaid）」等章节，明确已实现与规划中内容
- 提交信息格式统一为 `[项目名]: 更新类型; 更改信息`，并建立核心 + 扩展类型体系

### Home

#### 新增

- 坐标原语：`Int2` / `Int3` / `Float3` / `Key` / `Bound` / `Position`（自描述坐标，携带单位尺度，`Absolute()` 恒返回真实 cm）
- 属性系统：`Property` 基类 + 6 种子类型（`Boolean` / `Int` / `Float` / `String` / `Literal` / `Enum`）+ 内置属性（如 `is_structural`）+ 每实例值（get/set/事件/序列化）
- 实体系统：非泛型 `Entity`（Id / Key / Pos / Volume / 属性 / 组件）+ `EntityTemplate` / `EntityTemplateFactory` 配置管线 + 源接口（`IEntitySource` / `IComponentSource` / `IPropertySource`）
- 空间根：`UnitLayout`（单一扁平 3D 根，`sealed`）+ `Residence`（总容器）+ `UnitType`
- 体素存储：`VoxelLayout<T>`（Minecraft 式 16³ 区块 + paletted）、`GridLayout<T>`、`Subdivision<T>`（面实体）、`Graph` / `Graph2D` / `Graph3D`、`Palette` / `PackedBitStorage` / `PalettedContainer(RO)`
- 几何体系：`Volume` / `Shape` + 3D / 2D 形状族（`[JsonTypeName]` 注册，snake_case 配置直绑）
- 3D Region 自动生成：站立格（`PlacementLayoutSource` Up 面 + 净高过滤）→ `ColumnLayout` span 引擎 → 连通 flood-fill 聚合（`Region.BuildLayout` / `UnitLayout.RebuildRegions`）
- 空间查询层：`IVoxelSource<T>` + 扩展查询（`CanSee` 视线 / `FindItemsInView` 视野内目标（射线 / 圆锥 / 视锥）/ `IsCollided` 碰撞 / `FindPath` A* 寻路）；`Traverse` / `Visibility` / `Occlusion` / `Collision` / `Pathfinding` 纯几何基础
- 序列化管线：`JsonTypeNameRegistry` + 全套转换器（Geometry / Property / Component / Key / GridLayout / Palette）+ `Settings.JsonSerialization`（snake_case 统一入口）
- Sqlite 存储：`SqliteStore`（linq2db + Microsoft.Data.Sqlite，单文件存档 `Saves/<Name>.db`；`Residence` 整存 + `Entities` 每实体一行）
- Palette 直接 JSON 序列化：`palette_json + bits + data` 填表载荷

#### 变更

- 2026-08 大重构：空间模型扁平化为单一 3D 根（`UnitLayout`）；删除旧层级 `Home → Level → Building`、旧 2D `Region`、`BlockGraph`、`Canvas`、薄壳实体（`Wall` / `Door` / `Window` / `Appliance` / `Curtain`）与中间件
- 删除服务层（`PlacementService` / `RegionService` / `WallBuildingService` / `SelectionService`）与编辑器（`EditorCommand` / `MoveEntityCommand` / `CommandHistory`）——放置 / 拆除 / 撤销等具体编辑命令待 Phase 2 前重建
- Palette 策略减法：删除 `Int3ColumnSpanStrategy` / `Int3DenseStrategy` / `Int2DenseStrategy` 三个零引用策略
- 序列化：`LayoutChunkCodec` 随 Sqlite 体素层规划退役（当前体素层仍走 `LayoutChunkCodec` / `RegionsCodec` 文件层）

### Voice

#### 新增

- FastAPI 骨架：提供 `GET /health` 与 `POST /tts` 占位接口

### Ui

#### 新增

- GDExtension 入口骨架（`main.cpp`）与 `project.godot`、`CMakeLists.txt`

### Ai / Core / Sense / Stage

- 脚手架已建立（`Program.cs` 占位 / 平台目录），核心功能待实现（见 [ROADMAP.md](ROADMAP.md)）

### 计划中（见 [ROADMAP.md](ROADMAP.md)）

- Momoka.Home：设备抽象层（HA / GIIC）、安全约束（L3–L4）、Build 管线、具体编辑命令与撤销重做、门洞渲染与开口级联删除、Sqlite 体素层与区块压缩、DSL 安全规则
- Momoka.Ai / Core / Sense：核心功能实现
- Momoka.Ui：Live2D / 3D 场景 / VAD / ASR / 音频链路
- Momoka.Voice：接入 GPT-SoVITS / IndexTTS2
- CI 完善（Godot 真实导出、C++ 构建、`dotnet format` 校验）

---

## 版本约定

- `Unreleased`：尚未发布的变更。
- 正式版本号：`MAJOR.MINOR.PATCH`（SemVer）。

> 首个正式版本 `0.1.0` 将在核心功能可运行后发布。
