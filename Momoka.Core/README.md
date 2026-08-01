# Momoka.Core

Agent 执行层（C# / .NET 8）。

## 职责

- 意图识别（轻量 LLM via Ollama HTTP API）
- 快慢通道路由
- Agent 推理循环（独立上下文）
- 工具集成与调度（MCP 风格：HA API、日历、天气、Web 等）
- 知识记忆（用户行为偏好，LiteDB）

## 接口

- **输入**：用户指令 / 上下文
- **输出**：工具调用结果、执行计划
- **依赖**：Ollama HTTP API

## 命名空间

`Momoka.Core`
