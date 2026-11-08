# 레퍼런스 분석 자동 커밋
# Claude 예약 작업(MisterMind)이 GameDesign/design/research/references/ 에 쓴 문서를 커밋한다.
# 예약 작업은 PC의 git 잠금 파일을 지울 권한이 없어 직접 커밋하지 못하므로, Windows 작업 스케줄러가 이 스크립트를 돌린다.
# 이 폴더 외의 변경은 스테이징하지도, 건드리지도 않는다.
#
# 사용: powershell -NoProfile -ExecutionPolicy Bypass -File <이 파일> [-Push]

param(
    [switch]$Push
)

$ErrorActionPreference = 'Stop'
$RepoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$Folder = 'GameDesign/design/research/references'
$LogFile = Join-Path $env:TEMP 'ws-commit-references.log'

function Write-Log([string]$Message) {
    $line = '{0} {1}' -f (Get-Date -Format 'yyyy-MM-dd HH:mm:ss'), $Message
    Add-Content -Path $LogFile -Value $line -Encoding UTF8
}

Set-Location $RepoRoot

# 사전 확인 — 하나라도 걸리면 아무것도 하지 않고 끝낸다
$branch = (git rev-parse --abbrev-ref HEAD).Trim()
if ($branch -ne 'main') { Write-Log "건너뜀: 브랜치가 main이 아님 ($branch)"; exit 0 }
if (Test-Path '.git/index.lock') { Write-Log '건너뜀: 다른 git 작업 중 (index.lock)'; exit 0 }
if ((Test-Path '.git/MERGE_HEAD') -or (Test-Path '.git/rebase-merge') -or (Test-Path '.git/rebase-apply')) {
    Write-Log '건너뜀: merge/rebase 진행 중'; exit 0
}

$changes = @(git status --porcelain -- $Folder | Where-Object { $_ -ne '' })
if ($changes.Count -eq 0) { Write-Log '변경 없음'; exit 0 }

# 새로 생긴 회차 문서 이름(README 제외)을 본문에 적는다
$docs = @($changes | ForEach-Object { ($_.Substring(3)).Trim('"') } |
    Where-Object { $_ -like '*.md' -and $_ -notlike '*README.md' } |
    ForEach-Object { [IO.Path]::GetFileNameWithoutExtension($_) })

$title = if ($docs.Count -gt 0) { "docs: 레퍼런스 분석 $($docs.Count)편 추가" } else { 'docs: 레퍼런스 분석 목록 갱신' }
$body = ($docs | ForEach-Object { "- $_" }) -join "`n"
$message = "$title`n`n$body`n`n- 예약 작업 MisterMind 작성분 자동 커밋`n"

$msgFile = Join-Path $env:TEMP 'ws-commit-references-msg.txt'
[IO.File]::WriteAllText($msgFile, $message, (New-Object System.Text.UTF8Encoding($false)))

git add -- $Folder
git commit --only -q -F $msgFile -- $Folder
if ($LASTEXITCODE -ne 0) { Write-Log "커밋 실패 (exit $LASTEXITCODE)"; exit 1 }

$head = (git log -1 --format='%h %s').Trim()
Write-Log "커밋: $head"

if ($Push) {
    git push -q origin main
    if ($LASTEXITCODE -ne 0) { Write-Log "push 실패 (exit $LASTEXITCODE) — 원격이 앞서 있으면 직접 pull 후 push"; exit 1 }
    Write-Log 'push 완료'
}
