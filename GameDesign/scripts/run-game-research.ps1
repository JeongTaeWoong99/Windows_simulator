param([switch]$Check)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$node = (Get-Command node.exe -ErrorAction Stop).Source
$script = Join-Path $PSScriptRoot 'run-game-research.mjs'
$logDir = Join-Path $repoRoot '.utmp\game-research'
New-Item -ItemType Directory -Force -Path $logDir | Out-Null
$log = Join-Path $logDir 'scheduler.log'
try {
    if ($Check) {
        & $node $script --check
    } else {
        Add-Content -LiteralPath $log -Encoding UTF8 -Value "$(Get-Date -Format o) 시작"
        & $node $script *>> $log
    }
    if ($LASTEXITCODE -ne 0) { throw "게임 연구 실패 (exit $LASTEXITCODE)" }
} catch {
    Add-Content -LiteralPath $log -Encoding UTF8 -Value "$(Get-Date -Format o) 실패: $($_.Exception.Message)"
    throw
}
