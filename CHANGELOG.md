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

- 坐标原语：`Int2` / `Int3` / `Float3` / `Key` / `Bound`
- 属性系统：`Property<T>` 及 6 种子类型（布尔 / 枚举 / 浮点 / 整数 / 字符串 / 纹理）、`PropertyValueObject`
- 实体系统：`Entity` 继承链与 `Component` 行为脚本；`Wall` / `Door` / `Window` / `Appliance` / `Curtain` / `Human` / `Pet` 等
- 空间结构：`Home → Level → LevelChunk`（20×20×Y 分块）、`PalettedContainer`、`BlockGraph`、`Region`、`Canvas`
- 服务层：`PlacementService` / `RegionService` / `WallBuildingService` / `SelectionService`
- 编辑器：`EditorCommand` / `MoveEntityCommand` + `CommandHistory`（undo / redo）

### Voice

#### 新增

- FastAPI 骨架：提供 `GET /health` 与 `POST /tts` 占位接口

### Ui

#### 新增

- GDExtension 入口骨架（`main.cpp`）与 `project.godot`、`CMakeLists.txt`

### Ai / Core / Sense / Stage

- 脚手架已建立（`Program.cs` 占位 / 平台目录），核心功能待实现（见 [ROADMAP.md](ROADMAP.md)）

### 计划中（见 [ROADMAP.md](ROADMAP.md)）

- Momoka.Home：设备抽象层（HA / GIIC）、安全约束（L3–L4）、Build 管线、存档序列化
- Momoka.Ai / Core / Sense：核心功能实现
- Momoka.Ui：Live2D / 3D 场景 / VAD / ASR / 音频链路
- Momoka.Voice：接入 GPT-SoVITS / IndexTTS2
- 测试项目与 CI 完善

---

## 版本约定

- `Unreleased`：尚未发布的变更。
- 正式版本号：`MAJOR.MINOR.PATCH`（SemVer）。

> 首个正式版本 `0.1.0` 将在核心功能可运行后发布。
