# Momoka.Voice

TTS 微服务（Python 3.11+）。

## 职责

- 封装 GPT-SoVITS 或 IndexTTS2 推理
- HTTP 接口：接收文本，返回 WAV/opus 音频流
- 独立部署，不参与 Momoka.sln 构建

## 接口

- `GET /health` — 健康检查
- `POST /tts?text=...&speaker=...` — TTS 合成，返回音频流

## 启动

```bash
pip install -r requirements.txt
python server.py
```

默认监听 `0.0.0.0:8100`。

## 依赖

- FastAPI + Uvicorn
- GPT-SoVITS / IndexTTS2（需额外安装）
