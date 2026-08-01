# Momoka.Ui

渲染与交互引擎（Godot 4.x + C++ GDExtension）。

## 职责

- Live2D 角色渲染（Cubism Native SDK → GDExtension → TextureRect）
- 2D UI 叠加层（Godot Control 节点）
- 3D 家庭场景（glTF 加载、网格渲染、家具交互）
- VAD（语音活动检测，C++ 集成）
- ASR（whisper.cpp，C++ GDExtension）
- 摄像头采集与人脸检测（ONNX Runtime C++）
- 音频 I/O（miniaudio / Godot AudioServer）
- 情绪参数 → Live2D 动画参数映射

## 与主机通信

WebSocket + MessagePack

## 构建

```bash
cmake -B build -DCMAKE_TOOLCHAIN_FILE=$VCPKG_ROOT/scripts/buildsystems/vcpkg.cmake
cmake --build build
```

## 依赖

- godot-cpp（GDExtension 绑定）
- Cubism Native SDK（Live2D）
- whisper.cpp（ASR）
- ONNX Runtime（人脸检测）
- miniaudio（音频）
