# OCR Translator MVP (Capture Card)

Windows 기준으로 `캡쳐보드 화면 -> OCR -> 번역 -> 오버레이`를 실행하는 최소 구현입니다.

## 1) 준비

- Python 3.10+
- Switch + 캡쳐보드 + HDMI 출력
- 캡쳐보드가 Windows에서 장치로 인식되어야 함

## 2) 설치

```powershell
python -m venv .venv
.\.venv\Scripts\activate
python -m pip install --upgrade pip
pip install -r requirements.txt
```

`paddlepaddle` 설치가 실패하면 공식 가이드에 맞는 wheel 버전으로 먼저 설치한 뒤 다시 `pip install -r requirements.txt`를 실행하세요.

## 3) 실행

```powershell
.\.venv\Scripts\activate
python run.py --device 0 --select-roi --translator google --source-lang ja --target-lang ko
```

설명:

- `--select-roi`: 실행 직후 ROI 선택 창에서 대사창만 드래그
- `--translator google`: 무료 웹 번역(테스트 용도)
- `--translator deepl --deepl-api-key <KEY>`: DeepL API 사용
- `--show-source`: 오버레이에 원문+번역 동시 표시
- `--no-click-through`: 오버레이 클릭 가능 모드
- `--log-dir logs`: OCR 인식 로그 저장 폴더
- `--log-source-only`: 로그에 원문만 저장
- `--no-log`: 로그 저장 비활성화

## 4) 추천 튜닝 (파이어레드/리프그린 대화창)

- `--ocr-interval 0.30`
- `--pre-scale 2.2`
- `--threshold 165`
- ROI는 하단 대사창만 타이트하게 지정

예시:

```powershell
python run.py --device 0 --select-roi --ocr-interval 0.30 --pre-scale 2.2 --threshold 165 --translator google --source-lang ja --target-lang ko
```

## 5) 문제 해결

- 화면이 안 잡힘: `--device` 값을 0,1,2 순서로 변경
- 번역이 늦음: `--ocr-interval`을 0.4~0.6으로 올리고 ROI를 더 작게 설정
- OCR 품질 낮음: `--threshold`를 140~200 범위에서 조정
- 일본어 인식 약함: 캡쳐 해상도를 높이고 ROI를 더 정확히 맞춤

## 6) 로그 구조 (창 구분)

기본적으로 실행할 때마다 `logs/session_YYYYMMDD_HHMMSS/`가 생성됩니다.

- `transcript.txt`: 사람이 읽기 좋은 로그
- `transcript.jsonl`: 후처리/분석용 구조화 로그

로그는 아래 기준으로 구분됩니다.

- `WINDOW`: 같은 대사창에서 이어지는 OCR 업데이트 묶음
- `ENTRY`: 실제 OCR 인식 이벤트 1건

예시(`transcript.txt`):

```text
[WINDOW 0007]
[ENTRY 00042] 2026-02-27T11:03:22
SRC(ja): オーキドはかせ
TRN(ko): 오박사
```
