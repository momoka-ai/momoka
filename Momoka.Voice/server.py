"""Momoka.Voice - TTS 微服务骨架"""

from fastapi import FastAPI
from fastapi.responses import StreamingResponse
import io

app = FastAPI(title="Momoka.Voice")


@app.get("/health")
async def health():
    return {"status": "ok"}


@app.post("/tts")
async def tts(text: str, speaker: str = "default"):
    """接收文本，返回 WAV 音频流。

    TODO: 集成 GPT-SoVITS 或 IndexTTS2 推理引擎。
    """
    # 占位：返回空 WAV
    audio_data = io.BytesIO()
    return StreamingResponse(audio_data, media_type="audio/wav")


if __name__ == "__main__":
    import uvicorn
    uvicorn.run(app, host="0.0.0.0", port=8100)
