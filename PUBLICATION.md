# GitHub 공개 안내

공개 저장소는 [LIBENS-Lab/libens-iconchanger](https://github.com/LIBENS-Lab/libens-iconchanger), 기본 브랜치는 `main`입니다. 포크를 공개할 때는 아래 원격 URL을 본인의 계정·저장소 URL로 바꿉니다.

## 공개할 파일

1. 루트의 LICENSE·README·변경 기록·기여/보안/외부 라이선스 문서.
2. `.github`의 자동 검사·이슈 양식과 Git 설정 파일.
3. `LIBENS IconChanger`의 소스·직접 제작한 앱 아이콘·사용자 안내·빌드/검사 코드.
4. `scripts/export-source.ps1`로 만든 소스 압축본에는 검토하여 Git에 등록한 파일만 들어갑니다.

개인 작업 기록·지시사항·백업·ICO 보관 데이터·서명 인증서·빌드/검수 산출물·런타임 DLL은 `.gitignore`로 제외합니다. ZIP에는 `.git`도 들어가지 않습니다.

## 첫 업로드

저장소 루트에서 실행합니다. Git 작성자 이름·메일은 공개될 수 있으므로 GitHub에서 사용할 표시명과 비공개 noreply 주소 등을 본인이 설정하세요.

```powershell
git status --short
git diff --cached --stat
git diff --cached --check
```

내용을 확인한 뒤 첫 커밋을 만듭니다.

```powershell
git commit -m 'Initial public source for LIBENS IconChanger'
```

GitHub에서 빈 저장소를 만들고 원하는 공개 범위를 선택합니다. README나 LICENSE를 서버에서 별도로 초기화하지 않으면 첫 push가 간단합니다. 아래 URL을 본인의 저장소 주소로 바꾸세요.

```powershell
git remote add origin https://github.com/LIBENS-Lab/libens-iconchanger.git
git push -u origin main
```

이미 origin이 있으면 먼저 `git remote -v`로 확인합니다. 기존 원격 저장소의 이력을 덮어쓰는 강제 push는 사용하지 않습니다.

## 소스 압축본

```powershell
git add --all
& './scripts/export-source.ps1'
```

이 스크립트는 Git 인덱스의 스냅샷을 ZIP으로 내보냅니다. 내보내기 전에 변경한 소스가 등록되어 있는지 확인하세요. 파일이 이미 있으면 덮어쓰지 않습니다. 새 파일명으로 만들려면 `-OutputFile`을 지정합니다.

## 실행 파일 배포

1. 프로젝트 폴더에서 `build-essential.ps1`로 자체 포함 구성을 빌드합니다.
2. `checks/check-portable.ps1`로 별도 폴더 복사 실행을 확인합니다.
3. 완성된 `essential` 전체를 압축해 GitHub Releases에 첨부합니다. LICENSE·THIRD-PARTY-NOTICES.md·licenses 폴더를 포함합니다.
4. EXE 한 개만 올리지 않습니다. 소스 저장소에 런타임 DLL을 커밋하지 않습니다.
5. 변경 기록과 알려진 제한을 릴리스 설명에 적습니다. 실제 배포본이 코드 서명되어 있지 않다면 서명된 앱이라고 표기하지 않습니다.

## 공개 후 설정

1. GitHub Actions에서 첫 Windows 검사 결과를 확인합니다. 로컬 검증과 GitHub 서버의 실제 실행 결과는 구분합니다.
2. 원한다면 저장소의 비공개 취약점 신고를 활성화합니다. 절차는 [GitHub 공식 안내](https://docs.github.com/en/code-security/how-tos/report-and-fix-vulnerabilities/configure-vulnerability-reporting/configure-for-a-repository)를 참고하세요.
3. Issues 양식이 보이는지 확인하고, 버그 신고에는 개인 백업 대신 재현 순서와 가린 스크린샷을 받습니다.
