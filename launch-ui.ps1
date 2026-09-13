$exePath = Join-Path $PSScriptRoot "publish\ui-lite\ZeroClean.exe"
if (-not (Test-Path $exePath)) {
    Write-Error "ZeroClean.exe not found at $exePath"
    exit 1
}

$action = New-ScheduledTaskAction -Execute $exePath
$principal = New-ScheduledTaskPrincipal -UserId $env:USERNAME -LogonType Interactive
$task = New-ScheduledTask -Action $action -Principal $principal
Register-ScheduledTask -TaskName "ZeroCleanInteractiveUI" -InputObject $task -Force | Out-Null
Start-ScheduledTask -TaskName "ZeroCleanInteractiveUI"
Start-Sleep -Milliseconds 1500
Unregister-ScheduledTask -TaskName "ZeroCleanInteractiveUI" -Confirm:$false | Out-Null
Write-Host "ZeroClean UI launched successfully on user desktop."
