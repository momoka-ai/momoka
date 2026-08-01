# Momoka.Sense

后台感知层（C# / .NET 8）。

## 职责

- 可穿戴设备数据桥接（心率、睡眠，BLE 或厂商 Web API）
- GPS 定位（系统 API 或 HTTP 端点）
- 环境传感器数据（HomeAssistant API）
- 数据收集与标准化（不操作任何设备）

## 接口

- **输入**：BLE 设备数据、GPS 坐标、HA API 响应
- **输出**：标准化感知数据 → Momoka.Core

## 命名空间

`Momoka.Sense`
