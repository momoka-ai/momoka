# Momoka.Stage

终端适配层（Godot 导出配置 + 平台特定胶水代码）。

## 子模块

| 子模块 | 平台 | 职责 |
|--------|------|------|
| **Momoka.Stage.Desktop** | Windows / macOS / Linux | 窗口管理、系统托盘 |
| **Momoka.Stage.Mobile** | Android / iOS | 推送通知、后台服务 |
| **Momoka.Stage.Panel** | 中控屏 (Android) | 中控屏适配 |

Stage 不写渲染代码，只管理 Godot 项目导出预设和平台特定行为。
