# 路线图（Roadmap）

> 本文档详细列出 Momoka 的**当前进度**与**未来计划**。每完成一项即勾选 `[x]`。
>
> 图例：`[x]` 已完成 · `[ ]` 未开始 / 进行中（括号内标注）。

- [x] 建立 monorepo 项目骨架（`.sln`、CI、`.gitignore`、`.editorconfig`、`LICENSE`）
- [ ] Phase 0 — 基础设施
- [ ] Phase 1 — 完善 Momoka.Home
- [ ] Phase 2 — Momoka.Ai 角色引擎
- [ ] Phase 3 — Momoka.Core Agent 框架
- [ ] Phase 4 — Momoka.Sense 感知层
- [ ] Phase 5 — Momoka.Ui 渲染与语音
- [ ] Phase 6 — Momoka.Stage 平台适配
- [ ] Phase 7 — Momoka.Voice TTS 集成
- [ ] 远期目标

---

## 当前状态一览（2026-08）

| 模块 | 完成度 | 说明 |
|------|--------|------|
| Momoka.Home | 🟡 ~50% | 空间数据模型核心完成；设备层 / 安全层未实现 |
| Momoka.Voice | 🟡 ~20% | HTTP 骨架完成；TTS 引擎未集成 |
| Momoka.Ai / Core / Sense | 🔴 <10% | 仅程序入口骨架 |
| Momoka.Ui | 🔴 <10% | 仅 GDExtension 入口骨架 |
| Momoka.Stage | 🔴 <5% | 仅目录与占位 README |
| 测试 / CI | 🔴 <5% | 无测试项目，CI 为骨架 |

---

## Phase 0 — 基础设施（规划中）

- [ ] 初始化 Git 仓库并完成首次提交
- [ ] 创建 GitHub 仓库、分支保护规则（`main` 需 PR + CI 通过）
- [ ] 引入测试框架（xUnit）：`Momoka.Home.Tests` 作为首个测试项目
- [ ] 完善 CI：接入真实 Godot 导出检查、C++ 构建（vcpkg）
- [ ] 引入 `dotnet format` 与 `ruff` 校验进 CI
- [ ] 添加 Issue / PR 模板与自动化标签
- [ ] 配置依赖机器人（如 Dependabot）
- [ ] 首个正式版本 `0.1.0` 发布流程（tag + CHANGELOG）

## Phase 1 — 完善 Momoka.Home（进行中）

已实现：

- [x] 坐标原语：`Int2` / `Int3` / `Float3` / `Key` / `Bound`
- [x] 属性系统：`Property<T>` + 6 种子类型、`PropertyValueObject`
- [x] 实体系统：`Entity` 继承链 + `Component` 脚本；`Wall` / `Door` / `Window` / `Appliance` / `Curtain` / `Human` / `Pet` 等
- [x] 空间结构：`Home → Level → LevelChunk`、`PalettedContainer`、`BlockGraph`、`Region`、`Canvas`
- [x] 服务层：`PlacementService` / `RegionService` / `WallBuildingService` / `SelectionService`
- [x] 编辑器：`EditorCommand` / `MoveEntityCommand` + `CommandHistory`

待实现：

- [ ] **设备抽象层 `Providers`**：`IDeviceProvider` 接口、`ProviderRegistry`、HomeAssistant 实现、GIIC 协议桥接
- [ ] **安全约束 `Security`（L3–L4）**：Blackboard + 规则评估，拦截燃气 / 门锁 / 高压等危险操作
- [ ] **Build 管线**：视频流 → 3D 重建 → 网格（消费结构化户型数据）
- [ ] **存档 `HomeSerializer`**：Home / Level / 实体序列化与反序列化
- [ ] **设备配置 JSON**：`/devices/` 目录，以 JSON 声明第三方设备（无需写代码）
- [ ] **DSL 安全规则**：复杂约束的表达式解析
- [ ] **墙体开口级联删除**：删除墙 → 级联删除门窗
- [ ] **空气流体模拟（未来）**：房间粒度分段混合模型 → 自然通风建议
- [ ] 补充单元测试覆盖上述服务

## Phase 2 — Momoka.Ai 角色交互层（未开始）

- [ ] 角色引擎：对话生成 + 角色一致性管理
- [ ] 记忆系统：对话历史、情感事件（LiteDB）
- [ ] 情感状态机：情感参数输出 → 终端 Live2D
- [ ] 对话安全过滤（L0–L2：脏话 / 角色一致性 / 情感边界）
- [ ] TTS 协调：HTTP 调用 Momoka.Voice
- [ ] 与终端建立 WebSocket + MessagePack 通信

## Phase 3 — Momoka.Core Agent 执行层（未开始）

- [ ] 意图识别：调用轻量 LLM（Ollama HTTP API）
- [ ] 快慢通道路由：读取 Ai 情感状态，选择快速指令映射或 Agent 推理
- [ ] Agent 推理循环：独立上下文，不污染角色对话链
- [ ] 工具集成与调度（MCP 风格）：HA API、日历、天气、Web 等
- [ ] 知识记忆：用户行为偏好（LiteDB）

## Phase 4 — Momoka.Sense 后台感知层（未开始）

- [ ] 可穿戴设备桥接：心率、睡眠（BLE 或厂商 Web API）
- [ ] GPS 定位（系统 API / HTTP 端点）
- [ ] 环境传感器数据（HomeAssistant API 间接获取）
- [ ] 数据标准化并输出到 Momoka.Core

## Phase 5 — Momoka.Ui 渲染与交互（未开始）

- [ ] Live2D 角色渲染：Cubism Native SDK → GDExtension → TextureRect
- [ ] 2D UI 叠加层：设置面板、调试 HUD、对话气泡
- [ ] 3D 家庭场景：glTF 加载、网格渲染、家具放置 / 选中 / 拖拽 / 旋转、材质编辑、撤销重做
- [ ] VAD（语音活动检测，C++）
- [ ] ASR（whisper.cpp，C++ GDExtension）
- [ ] 摄像头采集与人脸检测（ONNX Runtime C++）
- [ ] 音频 I/O（miniaudio / Godot AudioServer）
- [ ] 情绪参数 → Live2D 动画参数映射
- [ ] 与主机建立 WebSocket + MessagePack 通信

## Phase 6 — Momoka.Stage 平台适配（未开始）

- [ ] Desktop：Windows / macOS / Linux 导出配置，窗口管理、系统托盘
- [ ] Mobile：Android / iOS 导出配置，推送通知、后台服务
- [ ] Panel：中控屏（Android 嵌入）适配

## Phase 7 — Momoka.Voice TTS 集成（进行中）

- [x] FastAPI 骨架：`GET /health`、`POST /tts` 占位
- [ ] 封装 GPT-SoVITS 推理
- [ ] 封装 IndexTTS2 推理
- [ ] 返回 WAV / opus 音频流
- [ ] 多说话人 / 音色管理
- [ ] 单元测试与压力测试

---

## 远期目标

- [ ] 全链路可运行的 MVP：一句话控制家中设备 + Live2D 反馈
- [ ] 空气流体模拟与智能通风建议（见 Phase 1）
- [ ] 插件生态：第三方设备 / 工具 / 角色扩展
- [ ] 多语言支持（界面与语音）
- [ ] 端侧模型优化与离线能力增强
- [ ] 社区化治理：维护者体系、贡献者激励

---

## 贡献

想参与某项工作？请阅读 [CONTRIBUTING.md](CONTRIBUTING.md) 并在对应 Issue 中认领。
