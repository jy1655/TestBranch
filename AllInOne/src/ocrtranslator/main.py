from __future__ import annotations

import argparse
import os
import time

from .config import AppConfig, CaptureConfig, LogConfig, OCRConfig, OverlayConfig, TranslationConfig
from .logger import TranscriptLogger


def _wait_for_first_frame(capture, timeout_sec: float = 8.0):
    end = time.monotonic() + timeout_sec
    while time.monotonic() < end:
        frame = capture.get_latest_frame()
        if frame is not None:
            return frame
        time.sleep(0.05)
    return None


def _build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description="Capture-card OCR translator overlay")

    parser.add_argument("--device", type=int, default=0, help="Video capture device index")
    parser.add_argument("--width", type=int, default=1280)
    parser.add_argument("--height", type=int, default=720)
    parser.add_argument("--fps", type=int, default=30)

    parser.add_argument("--roi", type=str, default=None, help="Dialogue ROI as x,y,w,h")
    parser.add_argument("--select-roi", action="store_true", help="Open ROI selection UI")

    parser.add_argument("--source-lang", type=str, default="ja")
    parser.add_argument("--target-lang", type=str, default="ko")
    parser.add_argument("--translator", type=str, default="google", choices=["google", "deepl", "none"])
    parser.add_argument("--deepl-api-key", type=str, default=None)

    parser.add_argument("--ocr-interval", type=float, default=0.35)
    parser.add_argument("--pre-scale", type=float, default=2.0)
    parser.add_argument("--threshold", type=int, default=170)
    parser.add_argument("--min-confidence", type=float, default=0.45)

    parser.add_argument("--overlay-x", type=int, default=60)
    parser.add_argument("--overlay-y", type=int, default=540)
    parser.add_argument("--overlay-width", type=int, default=1160)
    parser.add_argument("--font-size", type=int, default=28)
    parser.add_argument("--show-source", action="store_true")
    parser.add_argument("--no-click-through", action="store_true")

    parser.add_argument("--log-dir", type=str, default="logs")
    parser.add_argument("--no-log", action="store_true")
    parser.add_argument("--log-source-only", action="store_true")

    return parser


def main() -> int:
    args = _build_parser().parse_args()

    from .capture import CaptureWorker
    from .ocr_engine import OCRProcessor
    from .overlay import run_overlay_app
    from .pipeline import PipelineWorker
    from .roi import clamp_roi, default_dialogue_roi, parse_roi, select_roi
    from .state import SharedOverlayState
    from .translator import build_translator

    capture_config = CaptureConfig(
        device_index=args.device,
        width=args.width,
        height=args.height,
        fps=args.fps,
    )

    ocr_config = OCRConfig(
        source_lang=args.source_lang,
        ocr_interval_sec=args.ocr_interval,
        pre_scale=args.pre_scale,
        threshold=args.threshold,
        min_confidence=args.min_confidence,
    )

    translation_config = TranslationConfig(
        engine=args.translator,
        source_lang=args.source_lang,
        target_lang=args.target_lang,
        deepl_api_key=args.deepl_api_key or os.getenv("DEEPL_API_KEY"),
    )

    overlay_config = OverlayConfig(
        x=args.overlay_x,
        y=args.overlay_y,
        width=args.overlay_width,
        font_size=args.font_size,
        show_source=args.show_source,
        click_through=not args.no_click_through,
    )

    log_config = LogConfig(
        enabled=not args.no_log,
        directory=args.log_dir,
        source_only=args.log_source_only,
    )

    capture = CaptureWorker(capture_config)
    capture.start()

    frame = _wait_for_first_frame(capture)
    if frame is None:
        capture.stop()
        raise RuntimeError("No frame received from capture device. Check capture card connection.")

    if args.select_roi:
        roi = select_roi(frame)
    else:
        roi = parse_roi(args.roi) or default_dialogue_roi(frame)
    roi = clamp_roi(roi, frame)

    app_config = AppConfig(
        capture=capture_config,
        ocr=ocr_config,
        translation=translation_config,
        overlay=overlay_config,
        log=log_config,
        roi=roi,
    )

    transcript_logger = None
    if app_config.log.enabled:
        transcript_logger = TranscriptLogger(
            log_dir=app_config.log.directory,
            source_lang=app_config.translation.source_lang,
            target_lang=app_config.translation.target_lang,
            source_only=app_config.log.source_only,
        )

    print(
        "Starting OCR translator",
        f"device={app_config.capture.device_index}",
        f"roi={app_config.roi}",
        f"translator={app_config.translation.engine}",
        f"log_dir={transcript_logger.session_dir if transcript_logger else 'disabled'}",
    )

    ocr_processor = OCRProcessor(app_config.ocr)
    translator = build_translator(app_config.translation)
    state = SharedOverlayState()

    pipeline = PipelineWorker(
        capture=capture,
        ocr=ocr_processor,
        translator=translator,
        state=state,
        roi=app_config.roi,
        ocr_config=app_config.ocr,
        logger=transcript_logger,
    )

    pipeline.start()

    try:
        return run_overlay_app(app_config.overlay, state.get_snapshot)
    finally:
        pipeline.stop()
        capture.stop()
        if transcript_logger is not None:
            transcript_logger.close()


if __name__ == "__main__":
    raise SystemExit(main())
