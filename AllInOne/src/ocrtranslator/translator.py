from __future__ import annotations

from dataclasses import dataclass

import requests

from .config import TranslationConfig


class BaseTranslator:
    def translate(self, text: str) -> str:
        raise NotImplementedError


class IdentityTranslator(BaseTranslator):
    def translate(self, text: str) -> str:
        return text


class GoogleWebTranslator(BaseTranslator):
    def __init__(self, source_lang: str, target_lang: str) -> None:
        try:
            from deep_translator import GoogleTranslator
        except ImportError as exc:
            raise RuntimeError(
                "deep-translator is not installed. Run `pip install -r requirements.txt`."
            ) from exc

        self._translator = GoogleTranslator(source=source_lang, target=target_lang)

    def translate(self, text: str) -> str:
        if not text:
            return ""
        try:
            return self._translator.translate(text)
        except Exception:
            return text


@dataclass
class DeepLTranslator(BaseTranslator):
    api_key: str
    source_lang: str
    target_lang: str

    def __post_init__(self) -> None:
        self._url = (
            "https://api-free.deepl.com/v2/translate"
            if self.api_key.endswith(":fx")
            else "https://api.deepl.com/v2/translate"
        )

    def translate(self, text: str) -> str:
        if not text:
            return ""

        payload = {
            "auth_key": self.api_key,
            "text": text,
            "source_lang": self.source_lang.upper(),
            "target_lang": self.target_lang.upper(),
        }

        try:
            response = requests.post(self._url, data=payload, timeout=8)
            response.raise_for_status()
            data = response.json()
            translations = data.get("translations", [])
            if not translations:
                return text
            return translations[0].get("text", text)
        except Exception:
            return text


def build_translator(config: TranslationConfig) -> BaseTranslator:
    engine = config.engine.lower()

    if engine == "none":
        return IdentityTranslator()

    if engine == "deepl":
        if not config.deepl_api_key:
            raise ValueError("DeepL engine requires --deepl-api-key")
        return DeepLTranslator(
            api_key=config.deepl_api_key,
            source_lang=config.source_lang,
            target_lang=config.target_lang,
        )

    if engine == "google":
        return GoogleWebTranslator(
            source_lang=config.source_lang,
            target_lang=config.target_lang,
        )

    raise ValueError(f"Unknown translation engine: {config.engine}")
