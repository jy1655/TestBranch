from __future__ import annotations

import threading
import time
from typing import Optional

from .capture import CaptureWorker
from .config import OCRConfig
from .logger import TranscriptLogger
from .ocr_engine import OCRProcessor
from .roi import Rect, clamp_roi, crop
from .state import SharedOverlayState
from .text_filter import TextDeduplicator
from .translator import BaseTranslator


class PipelineWorker:
    def __init__(
        self,
        capture: CaptureWorker,
        ocr: OCRProcessor,
        translator: BaseTranslator,
        state: SharedOverlayState,
        roi: Rect,
        ocr_config: OCRConfig,
        logger: Optional[TranscriptLogger] = None,
    ) -> None:
        self._capture = capture
        self._ocr = ocr
        self._translator = translator
        self._state = state
        self._roi = roi
        self._ocr_config = ocr_config
        self._logger = logger

        self._dedupe = TextDeduplicator(similarity_threshold=0.93, min_interval_sec=0.15)
        self._stop_event = threading.Event()
        self._thread: threading.Thread | None = None

    def start(self) -> None:
        if self._thread and self._thread.is_alive():
            return
        self._thread = threading.Thread(target=self._run, name="pipeline-worker", daemon=True)
        self._thread.start()

    def stop(self) -> None:
        self._stop_event.set()
        if self._thread:
            self._thread.join(timeout=2.0)

    def _run(self) -> None:
        last_tick = 0.0

        while not self._stop_event.is_set():
            now = time.monotonic()
            elapsed = now - last_tick
            if elapsed < self._ocr_config.ocr_interval_sec:
                time.sleep(max(0.01, self._ocr_config.ocr_interval_sec - elapsed))
                continue
            last_tick = now

            frame = self._capture.get_latest_frame()
            if frame is None:
                time.sleep(0.02)
                continue

            roi = clamp_roi(self._roi, frame)
            dialogue_img = crop(frame, roi)
            source_text = self._ocr.recognize(dialogue_img)
            if not self._dedupe.should_emit(source_text):
                continue

            translated = self._translator.translate(source_text)
            self._state.update(source_text=source_text, translated_text=translated)
            if self._logger is not None:
                try:
                    self._logger.log(source_text=source_text, translated_text=translated)
                except Exception as exc:
                    print(f"Log write failed: {exc}")
