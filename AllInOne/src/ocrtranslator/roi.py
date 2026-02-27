from __future__ import annotations

from typing import Optional, Tuple

import cv2
import numpy as np

Rect = Tuple[int, int, int, int]


def parse_roi(value: Optional[str]) -> Optional[Rect]:
    if not value:
        return None
    parts = [p.strip() for p in value.split(",")]
    if len(parts) != 4:
        raise ValueError("ROI must be formatted as x,y,w,h")
    x, y, w, h = map(int, parts)
    if w <= 0 or h <= 0:
        raise ValueError("ROI width and height must be positive")
    return x, y, w, h


def clamp_roi(roi: Rect, frame: np.ndarray) -> Rect:
    fh, fw = frame.shape[:2]
    x, y, w, h = roi
    x = max(0, min(x, fw - 1))
    y = max(0, min(y, fh - 1))
    w = max(1, min(w, fw - x))
    h = max(1, min(h, fh - y))
    return x, y, w, h


def default_dialogue_roi(frame: np.ndarray) -> Rect:
    h, w = frame.shape[:2]
    roi_h = int(h * 0.28)
    return 0, h - roi_h, w, roi_h


def select_roi(frame: np.ndarray) -> Rect:
    roi = cv2.selectROI("Select Dialogue Box ROI", frame, showCrosshair=True, fromCenter=False)
    cv2.destroyWindow("Select Dialogue Box ROI")
    x, y, w, h = roi
    if w <= 0 or h <= 0:
        raise RuntimeError("ROI selection was cancelled")
    return int(x), int(y), int(w), int(h)


def crop(frame: np.ndarray, roi: Rect) -> np.ndarray:
    x, y, w, h = roi
    return frame[y : y + h, x : x + w]
