# LIBENS IconChanger

[English](README.md) · [사용방법](<LIBENS IconChanger/essential/사용방법.md>) · [변경 기록](CHANGELOG.md)

Windows 파일 확장자와 시스템 아이콘을 바꾸고 원래 설정으로 복원하는 무료 오픈소스 앱입니다. 한국어·영어 UI를 지원하며 현재 버전은 **1.4.1**입니다.

## 주요 기능

1. 여러 확장자의 아이콘을 한 번에 변경합니다.
2. 기본 폴더·휴지통·네트워크 드라이브의 전체 상태 또는 개별 상태를 선택합니다.
3. 내 PC·사용자 파일·문서·드라이브·프린터·전역 바로가기 오버레이를 변경합니다.
4. 이미지와 ICO·EXE·DLL에서 아이콘을 읽고 다중 해상도 ICO로 보관합니다.
5. 불투명 사각 이미지의 모서리 둥글기와 바로가기 오버레이 크기를 조절합니다.
6. 최초 변경 전 설정을 자동 백업하고 선택 복원합니다.
7. EXE 자체 아이콘 보존, 관리자 실행, 확인 후 캐시 갱신을 지원합니다.

## 실행

1. [Windows x64 실행 ZIP 다운로드](https://github.com/LIBENS-Lab/libens-iconchanger/releases/download/v1.4.1/LIBENS-IconChanger-1.4.1-win-x64.zip)를 누르거나 [Releases](https://github.com/LIBENS-Lab/libens-iconchanger/releases/latest)에서 ZIP을 내려받으세요.
2. ZIP을 마우스 오른쪽 버튼으로 눌러 **모두 압축 풀기**를 선택하세요.
3. 압축을 푼 폴더에서 `essential`을 열고 **LIBENS IconChanger.exe**를 더블 클릭하세요.

별도 설치·.NET 설치·소스 빌드가 필요하지 않습니다. 주변 DLL·리소스·라이선스 고지문이 필요하므로 `essential` 전체를 함께 보관하세요. GitHub의 `Code > Download ZIP`이나 `Source code`는 실행 파일이 없는 개발자용 소스입니다. 코드 서명되지 않은 앱이므로 Windows 보안 경고가 나타날 수 있습니다. 실행을 위해 Windows 보안 기능을 끄지 마세요.

오른쪽 위 `KO / EN` 메뉴로 언어를 바꿉니다. Windows 표시 언어가 한국어이면 한국어로, 그 외에는 영어로 시작합니다. 언어 선택은 실행 단위이며 입력 경로·옵션 값은 유지됩니다.

## 소스 빌드

Windows x64, PowerShell 7, .NET 10 SDK가 필요합니다. 저장소 루트에서 실행하세요.

```powershell
dotnet build 'LIBENS IconChanger/essential/source/LIBENS IconChanger.csproj' -c Debug
& './LIBENS IconChanger/artifacts/bin/Debug/net10.0-windows/LIBENS IconChanger.exe' --lang ko
```

자체 포함 폴더는 다음 명령으로 생성합니다.

```powershell
& './LIBENS IconChanger/build-essential.ps1'
```

빌드 산출물과 런타임은 Git에 등록하지 않습니다. 사용자 배포는 완성된 `essential` 전체를 압축해 Releases에 올리는 방식입니다.

## 적용 범위와 백업

1. 일반 확장자와 바탕 화면 시스템 아이콘은 현재 사용자 설정을 변경합니다. 전역 폴더·드라이브·프린터·EXE 대체 아이콘·바로가기 오버레이는 관리자 권한이 필요합니다.
2. 같은 ProgID를 공유하는 확장자는 함께 바뀔 수 있습니다.
3. 폴더 내용 썸네일·개별 설정 폴더·특수 폴더·제조사 아이콘은 기본 아이콘과 다르게 보일 수 있습니다.
4. 캐시 갱신과 탐색기 재시작은 확인 후 실행합니다. 진행 중인 파일 복사·이동·삭제를 먼저 끝내세요.
5. 기존 데이터 호환성을 위해 `%LOCALAPPDATA%\FileIconManager`와 `%PROGRAMDATA%\FileIconManager`를 유지합니다. 백업이나 generated-icons를 임의로 삭제하지 마세요.
6. WEBP·SVG는 Windows 이미지/썸네일 지원 환경에 따라 읽습니다. 모든 Windows 화면·버전에서 동일한 결과를 보장하는 기능은 아닙니다.

## 참여와 라이선스

1. [기여 및 검사 안내](CONTRIBUTING.md)
2. [보안 문제 신고](SECURITY.md)
3. [GitHub 공개 절차](PUBLICATION.md)
4. [MIT 라이선스](LICENSE): Copyright 2026 LIBENS. 외부 런타임과 사용자 이미지의 권리는 [별도 고지](THIRD-PARTY-NOTICES.md)를 따릅니다.
