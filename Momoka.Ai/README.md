# Momoka.Ai

角色交互层（C# / .NET 8）。

## 职责

- 角色引擎（对话生成 + 角色一致性管理）
- 角色记忆系统（对话历史、情感事件，LiteDB）
- 情感状态机
- 对话安全过滤（L0-L2）
- TTS 协调（HTTP → Momoka.Voice）

## 接口

- **输入**：来自终端的用户文本（ASR 完成后）
- **输出**：对话文本 + 情感参数 → 终端
- **依赖**：Momoka.Core（Agent 推理）、Momoka.Voice（TTS HTTP）

## 命名空间

`Momoka.Ai`
