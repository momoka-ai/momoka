# 更新日志（Changelog）

本项目的所有显著变更都将记录在此文件中。

格式基于 [Keep a Changelog](https://keepachangelog.com/zh-CN/1.1.0/)，版本号遵循 [语义化版本](https://semver.org/lang/zh-CN/)。

## [未发布]（Unreleased）

### 新增

- 初始化 monorepo 项目结构：
  - `Momoka.Ai` / `Momoka.Core` / `Momoka.Sense` / `Momoka.Home`（C# / .NET 8）
  - `Momoka.Ui`（Godot 4.x + C++ GDExtension 骨架）
  - `Momoka.Stage`（Desktop / Mobile / Panel 平台目录）
  - `Momoka.Voice`（Python FastAPI TTS 微服务骨架）
- 建立基础配置：`Momoka.sln`、`.editorconfig`、`.gitignore`、`LICENSE`（AGPLv3）、CI 工作流 `.github/workflows/ci.yml`
- **Momoka.Home 核心数据模型**：
  - 坐标原语：`Int2` / `Int3` / `Float3` / `Key` / `Bound`
  - 属性系统：`Property<T>` 及 6 种子类型（布尔 / 枚举 / 浮点 / 整数 / 字符串 / 纹理）、`PropertyValueObject`
  - 实体系统：`Entity` 继承链与 `Component` 行为脚本；`Wall` / `Door` / `Window` / `Appliance` / `Curtain` / `Human` / `Pet` 等
  - 空间结构：`Home → Level → LevelChunk`（20×20×Y 分块）、`PalettedContainer`、`BlockGraph`、`Region`、`Canvas`
  - 服务层：`PlacementService` / `RegionService` / `WallBuildingService` / `SelectionService`
  - 编辑器：`EditorCommand` / `MoveEntityCommand` + `CommandHistory`（undo / redo）
- **Momoka.Voice**：FastAPI 骨架，提供 `GET /health` 与 `POST /tts` 占位接口
- **Momoka.Ui**：GDExtension 入口骨架（`main.cpp`）与 `project.godot`、`CMakeLists.txt`
- 新增项目文档：`ROADMAP.md`、`CONTRIBUTING.md`、`CODE_OF_CONDUCT.md`、`SECURITY.md`、`CHANGELOG.md`，以及 `README.md`（中文）/ `README.en.md`（英文）

### 变更

- 重构根目录 `README.md`：新增「当前进度」「系统架构（Mermaid）」等章节，明确已实现与规划中内容

### 修复

- 无

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
