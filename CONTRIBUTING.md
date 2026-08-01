# 贡献指南（Contributing Guide）

感谢你对 **Momoka** 感兴趣！无论是提交 Issue、改进文档、修复 Bug 还是实现新功能，我们都非常欢迎。

> 🌐 本文档为中文版。English version: [CONTRIBUTING.md](CONTRIBUTING.md)（本文件同时面向国际贡献者，正文以中文为主，代码与命令保持通用）。

## 目录

- [项目概览](#项目概览)
- [开发环境](#开发环境)
- [从哪里开始](#从哪里开始)
- [代码规范](#代码规范)
- [提交规范](#提交规范)
- [分支与 PR 流程](#分支与-pr-流程)
- [CI 检查](#ci-检查)
- [测试要求](#测试要求)
- [文档与翻译](#文档与翻译)
- [行为准则](#行为准则)

---

## 项目概览

- **架构**：主机（C# / .NET 8）+ 终端（Godot 4.x + C++）+ 独立微服务（Python）。
- **仓库结构**：monorepo，每个模块一个目录（`Momoka.Ai/`、`Momoka.Core/`、`Momoka.Sense/`、`Momoka.Home/`、`Momoka.Ui/`、`Momoka.Stage/`、`Momoka.Voice/`）。
- **项目状态**：早期开发阶段，多数模块仍为骨架。请先阅读 [README.md](README.md) 的「当前进度」与 [ROADMAP.md](ROADMAP.md)，选择合适的工作项。

---

## 开发环境

### 必需依赖

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Godot 4.x](https://godotengine.org/download)（仅终端 UI 需要）
- [CMake](https://cmake.org/download/) 3.20+（仅 GDExtension 需要）
- [vcpkg](https://github.com/microsoft/vcpkg)（仅 C++ 依赖需要）
- Python 3.11+（仅 Voice 微服务需要）

### 验证环境

```bash
# C# 主机
dotnet build Momoka.sln

# Python 微服务
cd Momoka.Voice
pip install -r requirements.txt
ruff check .
```

---

## 从哪里开始

1. 阅读 [ROADMAP.md](ROADMAP.md) 与各模块 `README.md`，了解现状。
2. 在 [Issues](../../issues) 中找到标有 `good first issue` 的条目，或提出新 Issue 讨论方案。
3. 认领任务后，在评论中说明「我来处理」，避免多人重复工作。

---

## 代码规范

- 遵循根目录 `.editorconfig`（UTF-8、4 空格缩进、LF 换行）。
- **C#**：`dotnet format` 风格；命名空间与 `Momoka.*` 保持一致；启用 `nullable` 与 `implicit usings`。
- **Python**：使用 [ruff](https://docs.astral.sh/ruff/) 检查；类型标注（type hints）可选但推荐。
- **C++**：C++17；遵循 Godot GDExtension 编码风格。
- **提交前必须保证**：代码可构建、无新增警告（如可行）。

---

## 提交规范

提交信息**必须**采用以下格式（强制要求）：

```
[项目名]: 更新类型; 更改信息
```

- **项目名**：改动所属模块，如 `Ai`、`Core`、`Sense`、`Home`、`Ui`、`Stage`、`Voice`、`Docs`、`Ci` 等。
- **更新类型**：如 `Feature Update`（新功能）、`Fix Issues`（修复，可附 Issue 编号）、`Docs Update`（文档）、`Project Setup`（项目搭建）、`Refactor`（重构）等。
- **更改信息**：一句话描述改动内容。

示例：

```
[Home]: Feature Update; Create type BlockCompositionEntity and several interfaces to abstract block entities contained abilities.
[Sense]: Fix Issues #17, #323; Add collection non-null checking to solve unexpected null reference exceptions
[Docs]: Docs Update; Add bilingual README and roadmap documentation
```

> 一条提交只做一件事（single responsibility），便于回溯与 Review。

---

## 分支与 PR 流程

1. 从最新的 `main` 创建分支：`git checkout -b feat/<your-feature>` 或 `fix/<your-fix>`。
2. 提交变更（遵循[提交规范](#提交规范)）。
3. 推送分支并创建 Pull Request，PR 标题与描述请说明：
   - 改动内容与动机；
   - 相关的 Issue 编号（如 `Closes #123`）；
   - 自测结果。
4. 通过 CI 检查并收到至少一位维护者的 Review 后即可合并。

### 合并约定

- 使用 **Squash and merge** 保持 `main` 历史整洁。
- 小改动（拼写、格式）允许维护者直接 `docs` 提交，无需 PR。

---

## CI 检查

`.github/workflows/ci.yml` 会在 `push` / `pull_request` 时运行：

1. **C#**：`dotnet build Momoka.sln --configuration Release` + `dotnet test`
2. **Godot**：校验 `Momoka.Ui/project.godot` 存在
3. **Python**：`ruff check Momoka.Voice/`

提交前请本地至少通过 `dotnet build` 与 `ruff check`，减少 CI 返工。

---

## 测试要求

> ⚠️ 目前仓库**尚无测试项目**（Phase 0 规划中）。引入测试时应遵循以下约定：

- 测试项目命名：`<Module>.Tests`（如 `Momoka.Home.Tests`），使用 xUnit。
- 添加到 `Momoka.sln`，并放入各模块目录下。
- 新功能尽量附带单元测试；修复 Bug 建议附带回归测试。

---

## 文档与翻译

- 根目录 `README.md` 为**中文主版本**，`README.en.md` 为英文版，二者通过顶部的语言切换器互相引用，请保持同步。
- 修改架构、接口或模块职责时，请同步更新：
  - `README.md` / `README.en.md` 的「模块总览」「系统架构」；
  - `Documentation/DESIGN_HOME.md`（Momoka.Home 设计）；
  - `Documentation/PROJECT_GUIDELINE.md`（项目总纲）。
- 图表请使用 **Mermaid** 而非 ASCII 图。

---

## 行为准则

参与本项目即表示同意遵守 [CODE_OF_CONDUCT.md](CODE_OF_CONDUCT.md) 中定义的贡献者行为准则。请保持友善、尊重与建设性。

---

再次感谢你的贡献！❤️
