# LIBENS IconChanger

Windows 파일 확장자와 시스템 아이콘을 바꾸고 최초 설정으로 복원하는 무료 데스크톱 앱입니다. 현재 버전은 1.4.1이며 한국어·영어 UI를 지원합니다.

## 기능

1. 쉼표·세미콜론으로 지정한 여러 확장자 아이콘을 일괄 변경합니다.
2. PNG, JPG/JPEG, BMP, GIF, TIFF, ICO와 EXE/DLL 아이콘을 읽습니다. WEBP·SVG는 Windows 썸네일 공급자 지원에 따라 변환합니다.
3. 불투명 사각 이미지의 모서리 둥글기를 조절합니다. 기존 투명 모서리는 보존합니다.
4. 기본 폴더·휴지통·네트워크 드라이브의 모든 상태 또는 개별 상태를 선택합니다.
5. 내 PC·사용자 파일·문서·드라이브·프린터와 전역 바로가기 화살표를 변경합니다.
6. 바로가기 오버레이 크기를 10~100%로 조절하고 나머지 캔버스를 투명하게 저장합니다.
7. EXE 자체 아이콘 보존과 최초 설정 자동 백업·복원을 지원합니다.
8. `KO / EN` 메뉴로 언어를 전환합니다. 입력 경로·숫자 값·백업 식별값은 언어와 무관하게 유지됩니다.

## 개발 실행

Windows x64, PowerShell 7, .NET 10 SDK가 필요합니다. 이 README가 있는 폴더에서 실행합니다.

```powershell
dotnet build 'essential/source/LIBENS IconChanger.csproj' -c Debug
& './artifacts/bin/Debug/net10.0-windows/LIBENS IconChanger.exe' --lang ko
& './artifacts/bin/Debug/net10.0-windows/LIBENS IconChanger.exe' --lang en
```

프로젝트 폴더는 `LIBENS IconChanger`, 프로젝트 파일은 `LIBENS IconChanger.csproj`, 실행 파일은 `LIBENS IconChanger.exe`입니다. 코드 네임스페이스는 공백을 사용할 수 없으므로 `LibensIconChanger`를 사용합니다.

## 자체 포함 실행 구성

```powershell
./build-essential.ps1
```

명시적으로 실행 구성을 빌드하면 `essential/LIBENS IconChanger.exe`와 .NET 런타임·고지문을 생성합니다. 다른 PC로 옮길 때는 `essential` 전체를 복사합니다.

기존 실행 구성을 덮어쓰지 않는 별도 검수 구성은 다음처럼 만들 수 있습니다.

```powershell
./build-essential.ps1 -OutputDirectory ./artifacts/libens-check-app
./checks/check-portable.ps1 -AppDirectory ./artifacts/libens-check-app
```

완성된 실행 폴더에는 프로젝트 LICENSE·외부 고지문·런타임 licenses가 포함됩니다. 소스만 내려받았다면 먼저 빌드해 실행 파일과 런타임을 생성하세요.

## 검증

```powershell
& './artifacts/bin/Debug/net10.0-windows/LIBENS IconChanger.exe' --lang ko --ui-check ./artifacts/ui-libens-ko
& './artifacts/bin/Debug/net10.0-windows/LIBENS IconChanger.exe' --lang en --ui-check ./artifacts/ui-libens-en
& './artifacts/bin/Debug/net10.0-windows/LIBENS IconChanger.exe' --lang en --ui-check-native ./artifacts/ui-libens-native-en
dotnet run --project checks/CoreChecks/CoreChecks.csproj -- ./artifacts/core-checks-new essential/source/assets/app.ico
```

UI 검사는 예시 데이터를 사용하며 실제 아이콘 설정을 쓰지 않습니다. CoreChecks는 격리된 레지스트리에서 적용·백업·복원을 검사합니다. 셸 픽셀 검사는 Visual Studio C++ Build Tools와 Windows SDK가 있는 환경에서 `checks/check-shell.ps1`로 실행합니다.

## 데이터와 공개 준비

1. 기존 사용자 백업·ICO는 `%LOCALAPPDATA%\FileIconManager`, 전체 사용자 백업·ICO는 `%PROGRAMDATA%\FileIconManager`에서 계속 읽습니다. 이 경로를 임의로 이동하면 기존 레지스트리 참조가 끊길 수 있습니다.
2. 시작·미리보기는 설정을 바꾸지 않습니다. 전역 설정은 관리자 권한과 적용 확인이 필요합니다. 탐색기 재시작은 별도 확인 후 실행합니다.
3. `.gitignore`는 런타임·빌드 결과·검수 자료·로컬 작업 기록·백업·서명 인증서 등을 제외합니다. 앱 소스와 자체 앱 아이콘, 빌드·검사 코드, 사용자 안내를 등록합니다.
4. 프로젝트는 루트의 MIT LICENSE를 따릅니다. 외부 런타임과 사용자 이미지에는 별도 라이선스가 적용됩니다. 공개 절차는 루트 PUBLICATION.md를 참고하세요.

사용 안내는 [한국어](essential/사용방법.md)와 [English](essential/USER-GUIDE.en.md)를 참고하세요.
