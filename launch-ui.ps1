$exePath = Join-Path $PSScriptRoot "publish\ui-lite\ZClean.exe"
if (-not (Test-Path $exePath)) {
    Write-Error "ZClean.exe not found at $exePath"
    exit 1
}

$action = New-ScheduledTaskAction -Execute $exePath
$principal = New-ScheduledTaskPrincipal -UserId $env:USERNAME -LogonType Interactive
$task = New-ScheduledTask -Action $action -Principal $principal
Register-ScheduledTask -TaskName "ZCleanInteractiveUI" -InputObject $task -Force | Out-Null
Start-ScheduledTask -TaskName "ZCleanInteractiveUI"
Start-Sleep -Milliseconds 1500
Unregister-ScheduledTask -TaskName "ZCleanInteractiveUI" -Confirm:$false | Out-Null
Write-Host "ZClean UI launched successfully on user desktop."
