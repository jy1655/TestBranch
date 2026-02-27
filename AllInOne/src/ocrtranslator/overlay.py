from __future__ import annotations

import platform
import sys

from PySide6 import QtCore, QtWidgets

from .config import OverlayConfig
from .state import OverlaySnapshot


class OverlayWindow(QtWidgets.QWidget):
    def __init__(self, config: OverlayConfig) -> None:
        super().__init__()
        self._config = config
        self._last_rendered = ""
        self._build_ui()

    def _build_ui(self) -> None:
        self.setWindowFlags(
            QtCore.Qt.WindowType.FramelessWindowHint
            | QtCore.Qt.WindowType.WindowStaysOnTopHint
            | QtCore.Qt.WindowType.Tool
        )
        self.setAttribute(QtCore.Qt.WidgetAttribute.WA_TranslucentBackground)

        container = QtWidgets.QFrame()
        container.setStyleSheet(
            "QFrame {"
            "background-color: rgba(5, 10, 18, 170);"
            "border: 2px solid rgba(80, 170, 255, 200);"
            "border-radius: 12px;"
            "}"
        )

        self._label = QtWidgets.QLabel("Waiting for OCR...")
        self._label.setWordWrap(True)
        self._label.setStyleSheet(
            f"QLabel {{ color: #F5F8FF; font-size: {self._config.font_size}px;"
            "font-family: 'Segoe UI'; font-weight: 600; padding: 14px; }}"
        )

        layout = QtWidgets.QVBoxLayout(self)
        layout.setContentsMargins(0, 0, 0, 0)

        inner_layout = QtWidgets.QVBoxLayout(container)
        inner_layout.setContentsMargins(0, 0, 0, 0)
        inner_layout.addWidget(self._label)

        layout.addWidget(container)

        self.setGeometry(
            self._config.x,
            self._config.y,
            self._config.width,
            max(120, int(self._config.font_size * 3.4)),
        )

    def enable_click_through(self) -> None:
        if not self._config.click_through:
            return
        if platform.system() != "Windows":
            return

        hwnd = int(self.winId())
        if hwnd == 0:
            return

        import ctypes

        GWL_EXSTYLE = -20
        WS_EX_LAYERED = 0x00080000
        WS_EX_TRANSPARENT = 0x00000020
        WS_EX_TOOLWINDOW = 0x00000080

        user32 = ctypes.windll.user32
        current_style = user32.GetWindowLongW(hwnd, GWL_EXSTYLE)
        user32.SetWindowLongW(
            hwnd,
            GWL_EXSTYLE,
            current_style | WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW,
        )

    def update_from_snapshot(self, snapshot: OverlaySnapshot) -> None:
        if self._config.show_source:
            text = f"JP: {snapshot.source_text}\nKO: {snapshot.translated_text}"
        else:
            text = snapshot.translated_text

        text = text.strip() or "Waiting for OCR..."
        if text == self._last_rendered:
            return

        self._last_rendered = text
        self._label.setText(text)


def run_overlay_app(
    config: OverlayConfig,
    get_snapshot,
    refresh_ms: int = 120,
) -> int:
    app = QtWidgets.QApplication.instance() or QtWidgets.QApplication(sys.argv)

    window = OverlayWindow(config)
    window.show()
    window.enable_click_through()

    timer = QtCore.QTimer()
    timer.setInterval(refresh_ms)
    timer.timeout.connect(lambda: window.update_from_snapshot(get_snapshot()))
    timer.start()

    return app.exec()
