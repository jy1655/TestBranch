from __future__ import annotations

import json
import threading
import time
from dataclasses import dataclass
from datetime import datetime
from difflib import SequenceMatcher
from pathlib import Path


@dataclass(frozen=True)
class LogEntry:
    entry_id: int
    window_id: int
    timestamp: str
    source_text: str
    translated_text: str


class TranscriptLogger:
    def __init__(
        self,
        log_dir: str,
        source_lang: str,
        target_lang: str,
        source_only: bool = False,
        window_merge_sec: float = 1.6,
        same_window_similarity: float = 0.72,
    ) -> None:
        self._source_lang = source_lang
        self._target_lang = target_lang
        self._source_only = source_only
        self._window_merge_sec = window_merge_sec
        self._same_window_similarity = same_window_similarity

        self._lock = threading.Lock()
        self._entry_id = 0
        self._window_id = 0
        self._last_text = ""
        self._last_event_mono = 0.0

        started_at = datetime.now()
        session_name = f"session_{started_at:%Y%m%d_%H%M%S}"
        self.session_dir = Path(log_dir) / session_name
        self.session_dir.mkdir(parents=True, exist_ok=True)

        self.text_log_path = self.session_dir / "transcript.txt"
        self.jsonl_log_path = self.session_dir / "transcript.jsonl"

        self._write_text_header(started_at)

    def log(self, source_text: str, translated_text: str) -> LogEntry:
        source_text = source_text.strip()
        translated_text = translated_text.strip()
        if not source_text:
            raise ValueError("Cannot log empty source text")

        with self._lock:
            now_dt = datetime.now()
            now_mono = time.monotonic()
            is_new_window = self._is_new_window(source_text, now_mono)
            if is_new_window:
                self._window_id += 1

            self._entry_id += 1
            entry = LogEntry(
                entry_id=self._entry_id,
                window_id=self._window_id,
                timestamp=now_dt.isoformat(timespec="seconds"),
                source_text=source_text,
                translated_text=translated_text,
            )

            self._append_jsonl(entry)
            self._append_text(entry, is_new_window)

            self._last_text = source_text
            self._last_event_mono = now_mono
            return entry

    def close(self) -> None:
        return

    def _is_new_window(self, text: str, now_mono: float) -> bool:
        if not self._last_text:
            return True

        elapsed = now_mono - self._last_event_mono
        if elapsed > self._window_merge_sec:
            return True

        prev = self._last_text
        if text in prev or prev in text:
            return False

        similarity = SequenceMatcher(None, prev, text).ratio()
        if similarity >= self._same_window_similarity:
            return False

        return True

    def _write_text_header(self, started_at: datetime) -> None:
        with self.text_log_path.open("a", encoding="utf-8") as fp:
            fp.write("OCR Translator Transcript\n")
            fp.write(f"Started At: {started_at.isoformat(timespec='seconds')}\n")
            fp.write(f"Source Lang: {self._source_lang}\n")
            fp.write(f"Target Lang: {self._target_lang}\n")
            fp.write("=" * 72)
            fp.write("\n")

    def _append_jsonl(self, entry: LogEntry) -> None:
        payload = {
            "entry_id": entry.entry_id,
            "window_id": entry.window_id,
            "timestamp": entry.timestamp,
            "source_lang": self._source_lang,
            "target_lang": self._target_lang,
            "source_text": entry.source_text,
            "translated_text": entry.translated_text,
        }

        with self.jsonl_log_path.open("a", encoding="utf-8") as fp:
            fp.write(json.dumps(payload, ensure_ascii=False))
            fp.write("\n")

    def _append_text(self, entry: LogEntry, is_new_window: bool) -> None:
        with self.text_log_path.open("a", encoding="utf-8") as fp:
            if is_new_window:
                fp.write("\n")
                fp.write("-" * 72)
                fp.write("\n")
                fp.write(f"[WINDOW {entry.window_id:04d}]\n")

            fp.write(f"[ENTRY {entry.entry_id:05d}] {entry.timestamp}\n")
            fp.write(f"SRC({self._source_lang}): {entry.source_text}\n")
            if not self._source_only:
                fp.write(f"TRN({self._target_lang}): {entry.translated_text}\n")
            fp.write("\n")
