from __future__ import annotations

import platform
import threading
import time
from typing import Optional

import cv2
import numpy as np

from .config import CaptureConfig


class CaptureWorker:
    def __init__(self, config: CaptureConfig) -> None:
        self._config = config
        self._stop_event = threading.Event()
        self._thread: Optional[threading.Thread] = None
        self._lock = threading.Lock()
        self._latest_frame: Optional[np.ndarray] = None
        self._capture: Optional[cv2.VideoCapture] = None

    def start(self) -> None:
        if self._thread and self._thread.is_alive():
            return
        self._capture = self._open_capture()
        self._thread = threading.Thread(target=self._run, name="capture-worker", daemon=True)
        self._thread.start()

    def stop(self) -> None:
        self._stop_event.set()
        if self._thread:
            self._thread.join(timeout=2.0)
        if self._capture:
            self._capture.release()

    def get_latest_frame(self) -> Optional[np.ndarray]:
        with self._lock:
            if self._latest_frame is None:
                return None
            return self._latest_frame.copy()

    def _open_capture(self) -> cv2.VideoCapture:
        backend = cv2.CAP_DSHOW if platform.system() == "Windows" else 0
        cap = cv2.VideoCapture(self._config.device_index, backend)
        cap.set(cv2.CAP_PROP_FRAME_WIDTH, self._config.width)
        cap.set(cv2.CAP_PROP_FRAME_HEIGHT, self._config.height)
        cap.set(cv2.CAP_PROP_FPS, self._config.fps)

        if not cap.isOpened():
            raise RuntimeError(
                f"Failed to open video capture device index={self._config.device_index}."
            )
        return cap

    def _run(self) -> None:
        assert self._capture is not None
        sleep_time = 1.0 / max(float(self._config.fps), 1.0)

        while not self._stop_event.is_set():
            ok, frame = self._capture.read()
            if not ok:
                time.sleep(0.01)
                continue

            with self._lock:
                self._latest_frame = frame

            time.sleep(sleep_time * 0.2)
