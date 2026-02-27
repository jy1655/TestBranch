# OcrLite (MORT-based OCR + Translation)

`OcrLite`는 MORT 코드를 참고해 만든 **OCR + 번역 경량 앱**입니다.

- 캡쳐: OBS 같은 상용 프로그램 창을 Attach 후 화면 읽기
- OCR: Windows OCR (`Windows.Media.Ocr`)
- 번역: DeepL / Google / Papago
- 로그: 원문/번역문을 `dialogue_window` 단위로 구분 저장 (`txt + jsonl`)

## 기능

- 윈도우 목록에서 OBS 창 선택 후 Attach
- 미리보기 위에서 ROI(대사창 영역) 드래그 선택
- 주기 OCR (기본 350ms)
- 중복 텍스트 필터
- 번역 엔진 선택
  - `None`, `DeepL`, `Google`, `Papago`
- 인증 방식
  - DeepL: API Key
  - Google:
    - API Key
    - OAuth(Access Token + Project ID)
    - OAuth(Refresh Token + Client ID + Client Secret + Project ID, 자동 Access Token 갱신)
  - Papago: Client ID + Client Secret
- 로그 저장
  - `logs/session_YYYYMMDD_HHMMSS/transcript.txt`
  - `logs/session_YYYYMMDD_HHMMSS/transcript.jsonl`

## 요구사항

- Windows 10/11
- .NET SDK 9.x
- OCR 대상 언어 팩(예: 일본어 OCR이면 Windows 언어 기능 설치 필요)

## 실행

```powershell
cd OCR/OcrLite
dotnet restore
dotnet run -c Release
```

## 사용 순서

1. `Refresh` 클릭
2. 창 목록에서 OBS(미리보기) 창 선택
3. `Attach`
4. 미리보기 화면에서 대사창 영역을 마우스로 드래그
5. 번역 엔진 및 인증값 입력
6. `Start OCR`
7. 인식/번역 결과와 로그 파일 확인

## 로그 포맷

- `dialogue_window_id`: 같은 대화 흐름 묶음
- `entry_id`: OCR 이벤트 순번
- `source_text`: 원문
- `translated_text`: 번역문
- `engine`: OCR 엔진명
- `attached_window`, `roi`: 재현 가능한 문맥 정보

`jsonl` 예시:

```json
{"entry_id":1,"dialogue_window_id":1,"timestamp":"2026-02-27T12:10:21+09:00","source_lang":"ja","target_lang":"ko","source_text":"オーキドはかせ","translated_text":"오박사","engine":"WindowsOCR+DeepL","attached_window":{"handle":"0x2A04F0","title":"OBS 31.0.0"},"roi":{"x":0,"y":510,"width":1280,"height":210}}
```

## 주의사항
- OBS 미리보기 창이 가려져 있거나 최소화되면 캡처가 실패할 수 있습니다.
- OCR 정확도는 ROI를 타이트하게 잡을수록 좋아집니다.

## Google OAuth 메모
- 앱 내부 브라우저 로그인 플로우는 미포함입니다.
- Google 입력값이 동시에 있으면 `Refresh Token` 모드 > `Access Token` 모드 > `API Key` 순서로 사용됩니다.
- OAuth Access Token 직접 입력 모드는 토큰 만료 시 재입력이 필요합니다.
- OAuth Refresh Token 모드를 쓰면 앱이 `oauth2.googleapis.com/token`으로 Access Token을 자동 갱신합니다.
- `google antigravity` 같은 비공식 번역 경로는 지원하지 않습니다. Google Cloud Translation 공식 API 경로(v2/v3)만 사용합니다.

## 라이선스 메모

- 이 프로젝트는 MORT(MIT License)의 구조/아이디어를 참고해 OCR-only 워크플로우로 재구성했습니다.
