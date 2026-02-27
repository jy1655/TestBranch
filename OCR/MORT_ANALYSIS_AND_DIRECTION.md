# MORT 분석 및 OCR 전용 개발 방향

## 분석 범위

- Repo: `https://github.com/killkimno/MORT`
- 기준 브랜치: `main`
- 확인 일자: 2026-02-27

## 핵심 구조 요약

- 플랫폼: Windows 전용 WinForms + 일부 WPF(`PopupScreenCapture`)
- 런타임: `.NET 9` (`net9.0-windows10.0.22621.0`)
- OCR 엔진:
  - Windows OCR
  - OneOcr(Snipping Tool OCR DLL 기반)
  - EasyOCR(Python 임베딩)
  - Google Cloud Vision
  - Tesseract 계열(MORT_CORE.dll 의존)
- 번역 엔진:
  - Papago/Google/DeepL/Gemini/Custom API/EzTrans 등 다수
- 캡처:
  - Windows Graphics Capture(윈도우 선택형)
  - 자체 ScreenCapture 모듈 존재

## 우리 목적 관점의 진단(OCR-only + OBS 사용)

목표:
- OBS 같은 상용 캡처는 그대로 사용
- OCR만 별도 수행
- 원문 로그를 창/장면 단위로 쌓아 용어집 제작

현 상태에서 바로 쓰기 어려운 이유:
- OCR 파이프라인이 `MORT_CORE.dll` 함수(`processGetImgData`, `ProcessGetImgDataFromByte`)에 강하게 결합됨
- UI/번역/핫키/리모컨/옵션 기능이 단일 Form 중심으로 얽혀 있음
- 실행에 필요한 외부 DLL/리소스가 리포지토리에 완전 포함되지 않음
- 로거 클래스는 현재 `return;`으로 비활성된 코드가 존재

결론:
- MORT 전체를 그대로 포크해 유지보수하기보다,
- `캡처 + OCR + 로그`에 필요한 모듈만 추출해서 `OCR-Lite`로 재구성하는 전략이 적합

## 재사용 가치가 높은 모듈

우선 재사용 후보:
- `MORT/ScreenCapture/*`
  - OBS 미리보기 창 또는 특정 윈도우를 안정적으로 캡처 가능
- `MORT/OcrApi/OneOcr/*`
  - Windows 최신 OCR 경로(성능/정확도 유리)
- `MORT/OcrApi/WindowOcr/*`
  - OS 내장 OCR 백업 엔진
- `MORT/OcrApi/EasyOcr/*`
  - 일본어 특화 대체 경로

재사용 비추천(초기 단계):
- `Form1.cs` 중심 구조 전체
- `TransManager` 및 번역 API 모듈
- `MORT_CORE.dll` 기반 처리 경로

## 권장 개발 방향

### 방향 A (권장): OCR-Lite 신규 앱(동일 저장소 내 별도 프로젝트)

- 새 프로젝트: `OCR/OcrLite` (별도 csproj)
- 구성:
  - Capture Adapter: GraphicsCapture(윈도우 선택)
  - OCR Adapter: OneOcr -> 실패 시 WindowOcr -> 옵션으로 EasyOcr
  - Dialogue Region: 수동 ROI 1~N개
  - Transcript Logger: JSONL + TXT
- 번역은 제외(후속 단계에서 플러그인 방식으로 추가)

장점:
- 외부 DLL 의존 최소화
- 디버깅/배포 단순화
- OBS 워크플로우와 충돌 없음

### 방향 B: 기존 MORT를 강제 축소

- 기존 Form1/설정/번역 코드 대부분 비활성화
- OCR-only 모드 추가

단점:
- 레거시 결합이 강해 유지보수 난이도 높음
- 기능 제거 과정에서 회귀 위험 큼

## OCR-Lite 1차 스펙(권장)

- 입력:
  - `Attach Window`(OBS 미리보기 창 선택)
  - ROI 1개(파이어레드 대화창 기준)
- 처리:
  - 300~500ms 주기 OCR
  - 중복 필터(유사도/해시)
- 출력:
  - 오버레이 텍스트(선택)
  - 로그 파일
    - `logs/session_YYYYMMDD_HHMMSS/transcript.jsonl`
    - `logs/session_YYYYMMDD_HHMMSS/transcript.txt`
- 로그 필드:
  - `session_id`, `window_id`, `entry_id`, `timestamp`
  - `source_text`
  - `ocr_engine`, `roi_id`, `frame_hash`
  - `confidence(optional)`

## 구현 단계

1. Stage 0: 최소 부팅
- 새 csproj 생성
- ScreenCapture + ROI 표시 화면만 연결

2. Stage 1: OCR 연결
- OneOcr 우선 연결
- 실패 시 WindowOcr fallback

3. Stage 2: 로그 체계
- JSONL/TXT 동시 기록
- window(entry group) 분리 규칙 도입

4. Stage 3: 운영 기능
- 단축키 시작/정지
- ROI preset 저장/불러오기
- 성능 튜닝(주기, 스레드)

5. Stage 4: 후처리
- 로그 기반 고유명사 추출기
- 사용자 용어 사전 CSV 생성

## 리스크

- OneOcr DLL/모델 배포 경로 이슈
- Windows 버전별 GraphicsCapture 동작 차이
- EasyOCR는 Python 임베딩/모듈 설치 비용 큼

## 최종 제안

- `MORT`는 “레퍼런스 코드베이스”로 사용
- 실제 개발은 `OCR/OcrLite` 신규 경량 프로젝트로 진행
- 첫 목표는 번역 없는 OCR+로그 안정화
- 이후 번역/오버레이를 단계적으로 추가

