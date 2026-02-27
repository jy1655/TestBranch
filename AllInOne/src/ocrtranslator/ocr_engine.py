from __future__ import annotations

import re
from typing import Any

import cv2
import numpy as np

from .config import OCRConfig

_LANG_MAP = {
    "ja": "japan",
    "ko": "korean",
    "en": "en",
    "ch": "ch",
}


class OCRProcessor:
    def __init__(self, config: OCRConfig) -> None:
        self._config = config
        lang = _LANG_MAP.get(config.source_lang.lower(), config.source_lang)

        try:
            from paddleocr import PaddleOCR
        except ImportError as exc:
            raise RuntimeError(
                "paddleocr is not installed. Run `pip install -r requirements.txt`."
            ) from exc

        self._ocr = PaddleOCR(use_angle_cls=False, lang=lang, show_log=False)

    def preprocess(self, frame: np.ndarray) -> np.ndarray:
        gray = cv2.cvtColor(frame, cv2.COLOR_BGR2GRAY)

        if self._config.pre_scale > 1.0:
            gray = cv2.resize(
                gray,
                None,
                fx=self._config.pre_scale,
                fy=self._config.pre_scale,
                interpolation=cv2.INTER_CUBIC,
            )

        _, binary = cv2.threshold(
            gray,
            self._config.threshold,
            255,
            cv2.THRESH_BINARY,
        )
        return cv2.medianBlur(binary, 3)

    def recognize(self, frame: np.ndarray) -> str:
        processed = self.preprocess(frame)
        result = self._ocr.ocr(processed, cls=False)
        if not result:
            return ""

        lines = result[0] if isinstance(result, list) else result
        texts = []

        for item in lines:
            text, score = self._extract_text_score(item)
            if text and score >= self._config.min_confidence:
                texts.append(text)

        return _normalize_text(" ".join(texts))

    @staticmethod
    def _extract_text_score(item: Any) -> tuple[str, float]:
        if not item or len(item) < 2:
            return "", 0.0
        text_score = item[1]
        if not isinstance(text_score, (list, tuple)) or len(text_score) < 2:
            return "", 0.0

        text = str(text_score[0]).strip()
        try:
            score = float(text_score[1])
        except (TypeError, ValueError):
            score = 0.0
        return text, score


def _normalize_text(value: str) -> str:
    value = re.sub(r"\s+", " ", value)
    return value.strip()
