# Momoka 开发与协作规则（AGENTS）

本文件是仓库级**强制约定**的唯一权威来源，面向 AI 编程助手与协作者。
面向人类的完整规范见 `CONTRIBUTING.md`；此处为可执行要点。
（同内容的关键部分也写入 `.github/instructions/momoka.instructions.md`，由编辑器注入每次会话上下文。）

## 项目
- 开源 AI 家庭伴侣系统；monorepo：`Momoka.Ai` / `Momoka.Core` / `Momoka.Sense` / `Momoka.Home`（C#/.NET 8）+ `Momoka.Ui`（Godot+C++）+ `Momoka.Voice`（Python）
- 根 `Momoka.sln`；主开发模块当前为 `Momoka.Home`（实体/布局/几何/状态/序列化）

## 构建与测试
- 构建：`dotnet build Momoka.sln` — **必须 0 错误 0 警告**
- 测试：`dotnet test Tests/Momoka.Home/Momoka.Home.Tests.csproj` — 必须全绿
- 提交前两者都须通过

## 提交规范（强制）
提交信息格式：
```
[项目名]: 更新类型; 更改信息
```
- **项目名**：`Ai` / `Core` / `Sense` / `Home` / `Ui` / `Stage` / `Voice` / `Docs` / `Ci`
- **核心类型**（强制约定）：
  - `Feature Update` 新增功能
  - `Fix Issues` 修复 Bug（附 Issue 号）
  - `Refactor` 重构，行为不变
  - `Docs Update` 文档
  - `Unit Test` 新增/修改测试
- **扩展类型**（按需）：`Build Tools` / `CI/CD` / `Dependency` / `Config Update`（配置 JSON 等）/ `Security Update` / `Optimize` / `Asset Update` / `Release` / `Revert`
- 示例：`[Home]: Refactor; Ground becomes a PlaneLayout placement surface`
- **禁止** conventional commits 风格（如 `refactor(scope): ...`）
- 一个提交只做一件事；跨模块按影响最大的模块标记，或拆分多个提交

## 工作流纪律（强制）
1. **先方案后动工**：重构/大规模改动先给方案，用户确认后再执行；**用户只要评估时绝不执行**
2. **分 commit 提交**：`git add <明确文件清单>...`，**禁用 `git add -A` / `git add .`**
3. **git mv 保 rename 历史**：文件改名/移动必须用 `git mv`
4. **0 警告 0 错误 + 测试全绿**：否则不提交
5. **用户指定不动的文件不动**（如 `Documentation/midea/...json`），除非用户明确要求
6. **commit 即提交**：提交创建后直接推送，除非有特殊情况需确认

## 环境注意（本机 / zsh）
- **heredoc 陷阱**：`<<'EOF' ... EOF` 结尾后**不能**再接 `&&`（zsh parse error）；把 `git commit -F - <<EOF ... EOF` 单独作为命令末尾，`git push` 单独跑
- 终端一律 async 模式（sync 会被交互命令卡死）；交互输入用 `printf 'y\n...' | cmd` 管道
