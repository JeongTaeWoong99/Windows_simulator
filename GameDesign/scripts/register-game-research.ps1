param()
$ErrorActionPreference = 'Stop'
if ((Get-TimeZone).Id -ne 'Korea Standard Time') {
    throw '이 등록기는 한국시간 Windows PC용이다. 시간대를 확인한 뒤 등록한다.'
}
$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$runner = Join-Path $PSScriptRoot 'run-game-research.ps1'
$name = 'WindowsSimulator-GameResearch'
$powershell = Join-Path $env:SystemRoot 'System32\WindowsPowerShell\v1.0\powershell.exe'
$arguments = '-NoProfile -NonInteractive -WindowStyle Hidden -ExecutionPolicy Bypass -File "{0}"' -f $runner
$existing = Get-ScheduledTask -TaskName $name -TaskPath '\' -ErrorAction SilentlyContinue
if ($existing -and ($existing.Actions.Arguments -notcontains $arguments)) {
    throw "같은 이름의 다른 작업이 있다: $name"
}
& $runner -Check
if ($LASTEXITCODE -ne 0) { throw 'Codex 실행 경로 확인 실패' }
$who = [System.Security.Principal.WindowsIdentity]::GetCurrent().Name
$action = New-ScheduledTaskAction -Execute $powershell -Argument $arguments -WorkingDirectory $repoRoot
$triggers = @(6, 12, 18 | ForEach-Object { New-ScheduledTaskTrigger -Daily -At ("{0:00}:00" -f $_) })
$principal = New-ScheduledTaskPrincipal -UserId $who -LogonType Interactive -RunLevel Limited
$settings = New-ScheduledTaskSettingsSet -StartWhenAvailable -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -MultipleInstances IgnoreNew -ExecutionTimeLimit (New-TimeSpan -Hours 1)
Register-ScheduledTask -TaskName $name -TaskPath '\' -Action $action -Trigger $triggers -Principal $principal -Settings $settings -Description 'wsdocs 게임별 재미 연구 · 매일 한국시간 06·12·18시 · 로그인된 Codex CLI · 문서 단독 게시' -Force | Out-Null
Get-ScheduledTask -TaskName $name | Select-Object TaskName, State, @{Name='User';Expression={$_.Principal.UserId}}
Get-ScheduledTaskInfo -TaskName $name | Select-Object NextRunTime, LastTaskResult
