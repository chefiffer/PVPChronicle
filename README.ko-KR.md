# PVP Chronicle
[English](README.md) | [简体中文](README.zh-CN.md) | [繁體中文](README.zh-TW.md) | **한국어** | [Русский](README.ru-RU.md) | [Português (Brasil)](README.pt-BR.md) | [Español](README.es-ES.md) | [ไทย](README.th-TH.md) | [Tiếng Việt](README.vi-VN.md) | [日本語](README.ja-JP.md) | [Deutsch](README.de-DE.md) | [Français](README.fr-FR.md)

PvP Chronicle은 FINAL FANTASY XIV PvP 경기를 기록하고 검토하는 Dalamud 플러그인입니다. 전장과 크리스탈라인 컨플릭트의 기록 및 리플레이, 지도 목표, 성과 통계, 그리고 관측 가능한 증거 범위 안에서만 수행되는 행동 무결성 검토를 지원합니다.

## 설치

1. Dalamud가 활성화된 XIVLauncher를 설치하고 실행합니다.
2. **Dalamud Settings → Experimental**을 엽니다.
3. 다음 사용자 지정 플러그인 저장소 URL을 추가합니다.

   ```text
   https://raw.githubusercontent.com/chefiffer/dalamud-plugins/main/repo.json
   ```

4. 플러그인 설치 관리자를 열고 **PVP Chronicle**를 검색한 뒤 **Install**을 선택합니다.

## 주요 기능

- 전장과 크리스탈라인 컨플릭트의 로컬 경기 기록 및 상세 리플레이.
- 점수, KDA, 전투 흐름, 전투불능, 이동, 징표, 관측 가능한 목표 타임라인.
- PvP 달력, 지도 순환, 지도별 기믹, 현지화된 UI.
- 서버에서 생성한 Logs와 참고 분포를 받기 위한 선택적 업로드.

PvP Chronicle은 게임 플레이를 자동화하거나 전투 기술을 실행하지 않으며, 숨겨진 게임 상태를 드러내지 않습니다. 또한 불완전한 관측을 부정행위의 확정적 증거로 취급하지 않습니다.

## 기여

저장소를 포크하고, 범위가 명확한 브랜치를 만든 다음, 변경된 동작에 대한 테스트를 추가하여 `main` 브랜치로 풀 리퀘스트를 열어 주세요.

경기 캡처, 플레이어 데이터셋, 액세스 토큰, 비공개 서버 주소 또는 다른 사용자의 개인정보를 제출하지 마세요. 보안 문제는 [`SECURITY.md`](SECURITY.md)에 따라 신고해 주세요.

## 라이선스

이 저장소의 PvP Chronicle 클라이언트 소스는 [GNU Affero General Public License v3.0](LICENSE)에 따라 배포됩니다.

이 라이선스는 PvP Chronicle의 독자적인 클라이언트 소스와 AGPL-3.0-only로 명시된 파일에만 적용됩니다. FINAL FANTASY XIV 자료, 제3자 라이브러리 또는 글꼴의 라이선스를 변경하지 않으며, 해당 구성요소에는 각 권리자와 라이선스 조건이 계속 적용됩니다.

FINAL FANTASY XIV의 명칭, 이미지 및 아이콘은 Square Enix의 자산입니다. 게임 자료의 출처와 제3자 고지는 [`plugin/THIRD-PARTY-NOTICES.md`](plugin/THIRD-PARTY-NOTICES.md)를 참조하세요.

PvP Chronicle은 독립적인 비영리 커뮤니티 프로젝트이며 Square Enix와 제휴하거나 승인을 받은 프로젝트가 아닙니다.
