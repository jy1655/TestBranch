from __future__ import annotations

import time
from dataclasses import dataclass
from difflib import SequenceMatcher


@dataclass
class TextDeduplicator:
    similarity_threshold: float = 0.92
    min_interval_sec: float = 0.15

    def __post_init__(self) -> None:
        self._last_text = ""
        self._last_emit_time = 0.0

    def should_emit(self, text: str) -> bool:
        text = text.strip()
        if not text:
            return False

        now = time.monotonic()
        if now - self._last_emit_time < self.min_interval_sec:
            return False

        if not self._last_text:
            self._remember(text, now)
            return True

        ratio = SequenceMatcher(None, self._last_text, text).ratio()
        if ratio >= self.similarity_threshold:
            return False

        self._remember(text, now)
        return True

    def _remember(self, text: str, now: float) -> None:
        self._last_text = text
        self._last_emit_time = now
