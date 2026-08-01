请为我生成一个 monorepo 项目结构，项目名为 **Momoka**，采用 AGPLv3 协议。

## 项目概述

Momoka 是一个开源的 AI 家庭伴侣系统，采用主机+终端分离架构。主机运行 Agent 循环和角色引擎，终端负责 Live2D 角色渲染和语音 I/O。

## 解决方案与项目结构

根目录放置 `Momoka.sln`（仅包含 C# 项目）。每个项目使用自己的目录名作为项目名，不使用 `src/` 前缀。

Momoka/
├── Documentation/ # 文档
├── Momoka.sln
├── Momoka.Ai/ # C# 项目
├── Momoka.Core/ # C# 项目
├── Momoka.Sense/ # C# 项目
├── Momoka.Home/ # C# 项目
├── Momoka.Ui/ # Godot 项目（C++ GDExtension）
├── Momoka.Stage/ # Godot 导出配置 + 平台特定代码
├── Momoka.Voice/ # Python 微服务
├── docs/
├── .github/workflows/
├── .editorconfig
├── .gitignore
├── LICENSE
└── README.md


## 各模块详细职责

### Momoka.Ai（C# · .NET 8）

角色交互层：
- 角色引擎（对话生成 + 角色一致性管理）
- 角色记忆系统（对话历史、情感事件，使用 LiteDB 嵌入式数据库）
- 情感状态机
- 对话安全过滤（L0-L2：脏话过滤、角色一致性校验、情感边界）
- TTS 协调（通过 HTTP 调用 `Momoka.Voice` 微服务）
- 接收来自终端的用户文本输入（ASR 完成后的文本流），产出对话文本 + 情感参数给终端

命名空间：`Momoka.Ai`

### Momoka.Core（C# · .NET 8）

Agent 执行层：
- 意图识别（调用轻量 LLM via Ollama HTTP API）
- 快慢通道路由（读取 Ai 的情感状态，决定快速指令映射还是 Agent 推理）
- Agent 推理循环（复杂任务，独立上下文，不污染角色对话链）
- 工具集成与调度（MCP 风格接口：HA API、日历、天气、Web 等）
- 知识记忆（用户行为偏好数据，使用 LiteDB 嵌入式数据库）

命名空间：`Momoka.Core`

### Momoka.Sense（C# · .NET 8）

后台感知层（非直接对话状态下的数据收集）：
- 可穿戴设备数据桥接（心率、睡眠等，BLE 或厂商 Web API）
- GPS 定位（系统 API 或 HTTP 端点）
- 环境传感器数据（通过 HomeAssistant API 间接获取）
- 不操作任何设备，只收集并标准化数据

命名空间：`Momoka.Sense`

### Momoka.Home（C# · .NET 8）

家庭数字孪生：
- 3D 户型数据结构定义（房间、墙壁、门窗、设备位置）
- 家庭设备抽象层（统一设备模型，支持 HA 和 GIIC 协议）
- GIIC 协议桥接
- 设备指令分发
- 物理安全约束校验（L3-L4：门锁、燃气、高压设备等危险操作拦截）
- 接收 Buildx 扫描输出的结构化户型数据

命名空间：`Momoka.Home`

### Momoka.Ui（Godot 4.x 项目 + C++ GDExtension）

渲染与交互引擎：
- **Live2D 角色渲染**：通过 GDExtension（C++）封装 Cubism Native SDK，输出 RGBA 纹理到 Godot 的 TextureRect
- **2D UI 叠加层**：使用 Godot 原生 Control 节点（设置面板、调试 HUD、对话气泡等）
- **3D 家庭场景**：使用 Godot 原生 3D 节点
  - glTF 户型模型加载（由 Buildx 生成）
  - 网格化墙壁/地板渲染
  - 家电模型放置、选中、拖拽、旋转变换
  - 材质编辑（墙壁颜色、地板纹理等）
  - 场景层级管理（房间节点 → 墙壁节点 → 家具节点）
  - 操作历史与撤销/重做
- VAD（语音活动检测，C++ 集成）
- 摄像头采集与人脸检测（ONNX Runtime C++ API）
- ASR（whisper.cpp 集成，C++ GDExtension）
- 音频 I/O（通过 miniaudio 或 Godot AudioServer）
- 情绪参数 → Live2D 动画参数映射

与主机通信：WebSocket + MessagePack

### Momoka.Stage（Godot 导出配置 + 平台特定胶水代码）

终端适配：
- `Momoka.Stage.Desktop/`：Godot 导出 Windows/Mac/Linux 配置，窗口管理，系统托盘
- `Momoka.Stage.Mobile/`：Godot 导出 Android/iOS 配置，推送通知集成，后台服务
- `Momoka.Stage.Panel`：中控屏适配（Android 嵌入配置）

Stage 不写渲染代码，只管理 Godot 项目导出预设和平台特定行为（如移动端后台唤醒、桌面端系统托盘）。

### Momoka.Voice（Python 3.11+）

TTS 微服务：
- 约 300 行的 Python 壳
- 封装 GPT-SoVITS 或 IndexTTS2 推理
- 通过 HTTP 接收文本，返回 WAV/opus 音频流
- 使用 `requirements.txt` 管理依赖
- 不参与 Momoka.sln 构建，独立启动

## 全局约束

- C# 项目使用 `Microsoft.NET.Sdk`，目标 `net8.0`，通过 `<ProjectReference>` 在 `.sln` 中互引
- Godot 项目（Ui）包含一个 `CMakeLists.txt` 用于构建 GDExtension（C++ 部分），依赖通过 vcpkg 管理
- Python 项目使用 `requirements.txt`
- 模块间通信：
  - 同进程内的方法调用（C# 项目间）
  - 主机 ↔ 终端：WebSocket + MessagePack
  - Ai ↔ Voice：HTTP
  - Core ↔ Ollama：HTTP
- 每个项目目录下放置自己的 `README.md`，说明职责、接口和依赖
- 根目录 `README.md` 包含：项目概述、ASCII 架构图、各模块简介、快速构建指南、贡献指南
- 根目录放置 `LICENSE`（AGPLv3 全文）、`.gitignore`（覆盖 C#/Python/Godot 常见忽略项）、`.editorconfig`（UTF-8 + 4 空格缩进）
- `.github/workflows/` 包含基础 CI：C# 构建 + 测试、Godot 导出检查、Python lint

## 生成内容

请生成完整的目录树和以下关键文件的初始内容：

1. 根目录：`README.md`、`LICENSE`、`.gitignore`、`.editorconfig`、`Momoka.sln`
2. 每个 C# 项目：`.csproj`、入口 `Program.cs`（骨架）、项目 `README.md`
3. `Momoka.Ui/`：`project.godot`、`CMakeLists.txt`（GDExtension 骨架）、GDExtension 入口 `.cpp`、项目 `README.md`
4. `Momoka.Stage/`：各平台子目录与 `README.md`
5. `Momoka.Voice/`：`requirements.txt`、`server.py`（骨架）、`README.md`
6. `.github/workflows/ci.yml`

不要生成详细的业务逻辑实现，只生成结构和占位文件。README 中的架构描述需要清晰反映主机（C# 模块）与终端（Godot + C++）的分离以及数据流方向。
