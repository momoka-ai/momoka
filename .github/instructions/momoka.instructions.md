---
    description: Momoka 仓库强制规范（提交格式、工作流纪律），每次会话必须遵守
    applyTo: '**'
---
# Momoka 强制规范

完整版见根目录 `AGENTS.md`；以下为每次会话必须遵守的关键规则。

## 提交格式（强制，禁止 conventional commits）
格式：`[项目名]: 更新类型; 更改信息`
- 项目名：`Ai` / `Core` / `Sense` / `Home` / `Ui` / `Stage` / `Voice` / `Docs` / `Ci`
- 核心类型：`Feature Update` / `Fix Issues` / `Refactor` / `Docs Update` / `Unit Test`
- 扩展类型：`Build Tools` / `CI/CD` / `Dependency` / `Config Update` / `Security Update` / `Optimize` / `Asset Update` / `Release` / `Revert`
- 示例：`[Home]: Refactor; Ground becomes a PlaneLayout placement surface`
- 一个提交只做一件事；跨模块按影响最大的模块标记或拆分

## 工作流纪律（强制）
1. **先方案后动工**：重构先给方案，用户确认后再执行；**用户只要评估时绝不执行**
2. **分 commit**：`git add <明确文件清单>`，**禁用 `git add -A` / `git add .`**
3. **git mv 保 rename 历史**：文件改名必须用 `git mv`
4. **构建 0 警告 0 错误 + 测试全绿** 后才提交
5. **用户指定不动的文件不动**（如 `Documentation/midea/...json`）
6. **commit 即提交**：创建后直接推送，除非特殊情况

## 环境（本机 zsh）
- heredoc（`<<'EOF'`）结尾后不能接 `&&`；commit 用 heredoc 单独结尾，push 单独跑
- 终端一律 async 模式
