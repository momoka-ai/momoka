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
- **更新类型**：见下方「更新类型」表（核心 + 扩展）。
- **更改信息**：一句话描述改动内容。

### 更新类型（核心 · 强制约定）

| 更新类型 | 含义 | 示例 |
|----------|------|------|
| `Feature Update` | 新增功能 | `[Home]: Feature Update; Create type BlockCompositionEntity and several interfaces to abstract block entities contained abilities.` |
| `Fix Issues` | 修复 Bug（附 Issue 号） | `[Sense]: Fix Issues #17, #323; Add collection non-null checking to solve unexpected null reference exceptions` |
| `Refactor` | 重构，行为不变 | `[Core]: Refactor; Extract tool dispatcher into separate service` |
| `Docs Update` | 文档（含双语翻译同步） | `[Docs]: Docs Update; Add English README` |
| `Unit Test` | 新增/修改测试 | `[Home]: Unit Test; Cover PlacementService edge cases` |

### 更新类型（扩展 · 按需使用）

| 更新类型 | 含义 | 何时使用 |
|----------|------|----------|
| `Build Tools` | 构建系统改动 | `.csproj`、`CMakeLists.txt`、Godot 导出配置 |
| `CI/CD` | CI/CD 工作流 | `.github/workflows/*.yml` |
| `Dependency` | 依赖升级/降级 | NuGet / pip / vcpkg 版本变更 |
| `Config Update` | 配置变更 | `.editorconfig`、`.gitignore`、设备配置 JSON |
| `Security Update` | 安全修复 | 敏感修复，先走 [SECURITY.md](SECURITY.md) 私有流程 |
| `Optimize` | 性能优化 | 明确以性能为目标的改动 |
| `Asset Update` | 资源文件 | Live2D 模型、纹理、音频、glTF 户型 |
| `Release` | 版本发布 | 打 tag + 更新 CHANGELOG |
| `Revert` | 回滚 | 撤销之前的提交 |

### 配套规则

1. **一个提交只做一件事**（single responsibility），便于回溯与 Review；
2. **跨模块改动**：按影响最大的模块标记 `[项目名]`，或拆分为多个提交；
3. **`Fix Issues` 必须附 Issue 号**；没有号则写清现象与根因；
4. **`Security Update` 敏感**：先通过 [SECURITY.md](SECURITY.md) 的私有渠道沟通；
5. **类型不够用**：先用语义最接近的类型并在描述中说明；新增类型需在讨论中确认；
6. **CHANGELOG 联动**：`CHANGELOG.md` 按模块分节，分类与此类型体系对应。

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

> 当前 `Tests/Momoka.Home/`（xUnit）已有 **305 个测试**且全绿；提交前必须通过 `dotnet test Tests/Momoka.Home/Momoka.Home.Tests.csproj`。约定如下：

- 测试项目命名：`<Module>.Tests`（如 `Momoka.Home.Tests`），使用 xUnit。
- 添加到 `Momoka.sln`，并放入各模块目录下（现有测试在 `Tests/Momoka.Home/`）。
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
