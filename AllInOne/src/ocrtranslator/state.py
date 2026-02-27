from __future__ import annotations

import threading
from dataclasses import dataclass


@dataclass
class OverlaySnapshot:
    source_text: str
    translated_text: str


class SharedOverlayState:
    def __init__(self) -> None:
        self._lock = threading.Lock()
        self._source_text = ""
        self._translated_text = ""

    def update(self, source_text: str, translated_text: str) -> None:
        with self._lock:
            self._source_text = source_text
            self._translated_text = translated_text

    def get_snapshot(self) -> OverlaySnapshot:
        with self._lock:
            return OverlaySnapshot(
                source_text=self._source_text,
                translated_text=self._translated_text,
            )
