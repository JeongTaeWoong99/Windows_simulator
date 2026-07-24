# 엑셀 → 코드(.cs) → MemoryPack 바이너리(.bytes) → 미러링 파이프라인
#
# 1단계: ExcelGenerator  — Enum.cs + Row(GameData/Tables) + GameTable/TableSet + Packer 코드 생성
# 2단계: ExcelDataPacker — 생성 코드를 컴파일한 뒤 엑셀을 다시 파싱해 바이너리 패킹 (Shared/Data/*.bytes)
# 3단계: 미러링         — 정의(.cs) → Unity, 데이터(.bytes) → Unity StreamingAssets
#                         (서버는 WSGameServer.csproj의 Content로 .bytes를 bin에 자동 복사)
#
# MemoryPack은 소스 제너레이터 기반이라 Row 클래스가 컴파일된 뒤에야 직렬화할 수 있으므로
# 반드시 이 순서(코드 생성 → 패커 빌드+실행)로 실행해야 한다.

$ErrorActionPreference = "Stop"

# Unity 프로젝트 루트(미러링 대상). sync-protocol-to-unity.ps1의 기본값과 동일하게 맞춘다.
$UnityRoot = "C:\Users\wlsdn\workspace\Windows_simulator\Assets"

# 콘솔에서 직접 실행(더블클릭 등)했을 때만 멈춘다. NonInteractive(CI 등)면 프롬프트가 불가하므로 조용히 넘어간다.
# 더블클릭 실행 시 창이 바로 닫혀 결과(성공/실패)를 못 보는 걸 막는다.
function Wait-BeforeClose {
    if ([Environment]::UserInteractive) {
        try { Read-Host "엔터를 누르면 창을 닫습니다" | Out-Null } catch { }
    }
}

# 실패 시 원인을 출력하고 창을 유지한 뒤 종료한다.
function Stop-OnFailure {
    param([string]$Message, [int]$Code = 1)
    Write-Host "[실패] $Message" -ForegroundColor Red
    Wait-BeforeClose
    exit $Code
}

try {
    Write-Host "[1/3] ExcelGenerator: 코드 생성" -ForegroundColor Cyan
    dotnet run --project (Join-Path $PSScriptRoot "ExcelGenerator")
    if ($LASTEXITCODE -ne 0) { Stop-OnFailure "코드 생성 단계에서 중단합니다." $LASTEXITCODE }

    Write-Host "[2/3] ExcelDataPacker: 바이너리 패킹" -ForegroundColor Cyan
    dotnet run --project (Join-Path $PSScriptRoot "ExcelDataPacker")
    if ($LASTEXITCODE -ne 0) { Stop-OnFailure "바이너리 패킹 단계에서 중단합니다." $LASTEXITCODE }

    Write-Host "[3/3] 미러링: 정의(.cs) → Unity, 데이터(.bytes) → Unity StreamingAssets" -ForegroundColor Cyan

    # 3-1) GameData 정의(.cs) → Assets/Scripts_Server/GameData (기존 프로토콜 미러 스크립트 재사용)
    & (Join-Path $PSScriptRoot "sync-protocol-to-unity.ps1") -FolderMap @{ "GameData" = "GameData" }
    if ($LASTEXITCODE -ne 0) { Stop-OnFailure "정의(.cs) 미러링 단계에서 중단합니다." $LASTEXITCODE }

    # 3-2) .bytes → Assets/StreamingAssets/Data (없어진 파일은 함께 정리)
    $dataSrc   = Join-Path $PSScriptRoot "Shared\Data"
    $unityData = Join-Path $UnityRoot   "StreamingAssets\Data"
    New-Item -ItemType Directory -Force -Path $unityData | Out-Null

    $srcBytes = Get-ChildItem -LiteralPath $dataSrc -Filter *.bytes -File -ErrorAction SilentlyContinue
    $expected = New-Object System.Collections.Generic.HashSet[string]
    foreach ($b in $srcBytes) {
        [void]$expected.Add($b.Name)
        Copy-Item -LiteralPath $b.FullName -Destination (Join-Path $unityData $b.Name) -Force
        Write-Host "  copy : StreamingAssets\Data\$($b.Name)" -ForegroundColor Green
    }
    # 소스에서 사라진 .bytes 는 Unity 쪽에서도 .meta 와 함께 제거
    foreach ($d in (Get-ChildItem -LiteralPath $unityData -Filter *.bytes -File -ErrorAction SilentlyContinue)) {
        if (-not $expected.Contains($d.Name)) {
            Remove-Item -LiteralPath $d.FullName -Force
            $meta = "$($d.FullName).meta"
            if (Test-Path -LiteralPath $meta) { Remove-Item -LiteralPath $meta -Force }
            Write-Host "  del  : StreamingAssets\Data\$($d.Name)" -ForegroundColor DarkYellow
        }
    }

    Write-Host "[완료] GameData(.cs)/Shared·Unity(.bytes) 생성 + 미러링 완료" -ForegroundColor Green
    Wait-BeforeClose
}
catch {
    # $ErrorActionPreference=Stop 로 인한 종료성 예외도 창을 유지한 채 보여준다.
    Stop-OnFailure $_.Exception.Message
}
