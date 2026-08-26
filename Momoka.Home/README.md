# Momoka.Home

家庭数字孪生（C# / .NET 8，零外部运行依赖、零网络）。

> 完整架构设计见 [Documentation/DESIGN_HOME.md](../Documentation/DESIGN_HOME.md)；进度与规划见 [ROADMAP.md](../ROADMAP.md)。

## 职责

- **空间数据模型**：单一扁平 3D 空间根（`UnitLayout`）+ 总容器（`Residence`）——地板 / 天花板 / 墙 / 家具全是 `Entity`，坐标一律根绝对（10cm 体素格）
- **放置与附着**：`Add(entity[, position])` 根放置 / `Add(entity)` 自动寻位（Bound 内不碰撞 + 下方支撑）/ `Add(entity, pos, source)` 表面附着（编辑器视线探测提取表面，`RotationAlignment` 期望类别校验，`PlacementLayoutSource` 宿主登记）；`Remove(entity[, cascade])` 级联回落未放置池（无环不变量：Items 恒为森林）
- **属性系统**：`Property` 基类 + 6 种子类型，配置驱动，值存于每实例 `Property.Value`
- **体素存储**：Minecraft 式 16³ 区块 + paletted（`VoxelLayout` / `PalettedContainer`）、2D 网格（`GridLayout`）、面实体（`Subdivision`）、图（`Graph` / `Graph2D` / `Graph3D`）
- **几何体系**：`Volume` / `Shape` + 3D / 2D 一族（`[JsonTypeName]` 注册，snake_case 配置直绑）
- **Region 层**：`LevelLayout.Regions`（`VoxelLayout<Region>`）承载房间 / 可行走区域值；自动推导（原 `ColumnLayout` 时代 `BuildLayout`）已删除，待按 VoxelLayout 重写
- **空间查询**：视线 / 视野内目标 / 碰撞 / 寻路（`IVoxelSource<T>` + 扩展；`Traverse` / `Visibility` / `Occlusion` / `Pathfinding` 纯几何基础）
- **持久化**：SQLite 单文件存档（`SqliteStore`）——三表原子写入：`Entities`（每实体一行，含隐藏 Home 实体）/ `Chunks`（体素块，paletted 编码）/ `RegionNames`（id + name）；体素与 Region 文件层（`LayoutChunkCodec` / `RegionsCodec`）已退役
- 设备抽象层（HA / GIIC）与安全约束校验为**规划中**，未实现

## 接口

- **输入**：结构化户型数据（配置模板 / `EntityTemplate`）、空间查询与编辑操作请求
- **输出**：空间状态、查询结果、存档文件（`Saves/<Name>.db`）
- **依赖**：仅 `Newtonsoft.Json`、`linq2db`、`Microsoft.Data.Sqlite`（NuGet）

## 命名空间

| 命名空间 | 内容 |
|----------|------|
| `Momoka.Home` | 根：`UnitLayout` / `Residence` / `Region` / `Agent` / `UnitType` / `Settings` |
| `Momoka.Home.Primitives` | `Int2` / `Int3` / `Float3` / `Key` / `Bound` / `Position` / `Rotation` / `Transform` / `RotationAlignment` |
| `Momoka.Home.Entities` | 实体系统：`Entity` / `EntityTemplate` / `EntityTemplateFactory` / `IEntitySource` / `IEntityRelationSource` / `.Components`（行为组件族）/ `.Properties`（Property 与 6 种子类型） |
| `Momoka.Home.Level` | 数据载荷与编辑：`LevelData`（基类）/ `ServerLevelData` / `ClientLevelData` / `EditorSession` / 命令层 / 协议（`Protocol/`） |
| `Momoka.Home.Geometry` | `Volume` / `Shape` / `IVoxelGeometry2D/3D` 及 3D / 2D 形状族 |
| `Momoka.Home.Layouts` | `VoxelLayout` / `GridLayout` / `Palette` 族 / `Graph` / `Subdivision` / `IVoxelSource` |
| `Momoka.Home.Algorithms` | `Traverse` / `Visibility` / `Occlusion` / `Collision` / `Pathfinding` |
| `Momoka.Home.Data` | JSON 转换器与注册表、`LayoutChunkCodec` / `RegionsCodec` |
| `Momoka.Home.Data.Sqlite` | `SqliteStore`（linq2db 持久化） |
| `Momoka.Home.Helpers` | 整数数学助手（`ValueHelper`：floor 除法 / 取模） |
