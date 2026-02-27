from __future__ import annotations

from dataclasses import dataclass
from typing import Optional, Tuple


Rect = Tuple[int, int, int, int]


@dataclass(frozen=True)
class CaptureConfig:
    device_index: int = 0
    width: int = 1280
    height: int = 720
    fps: int = 30


@dataclass(frozen=True)
class OCRConfig:
    source_lang: str = "ja"
    ocr_interval_sec: float = 0.35
    pre_scale: float = 2.0
    threshold: int = 170
    min_confidence: float = 0.45


@dataclass(frozen=True)
class TranslationConfig:
    engine: str = "google"
    source_lang: str = "ja"
    target_lang: str = "ko"
    deepl_api_key: Optional[str] = None


@dataclass(frozen=True)
class OverlayConfig:
    x: int = 60
    y: int = 540
    width: int = 1160
    font_size: int = 28
    show_source: bool = False
    click_through: bool = True


@dataclass(frozen=True)
class LogConfig:
    enabled: bool = True
    directory: str = "logs"
    source_only: bool = False


@dataclass(frozen=True)
class AppConfig:
    capture: CaptureConfig
    ocr: OCRConfig
    translation: TranslationConfig
    overlay: OverlayConfig
    log: LogConfig
    roi: Optional[Rect] = None
