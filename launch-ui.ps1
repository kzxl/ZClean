$exePath = Join-Path $PSScriptRoot "publish\ui-lite\CleanTool.exe"
if (-not (Test-Path $exePath)) {
    Write-Error "CleanTool.exe not found at $exePath"
    exit 1
}

$action = New-ScheduledTaskAction -Execute $exePath
$principal = New-ScheduledTaskPrincipal -UserId $env:USERNAME -LogonType Interactive
$task = New-ScheduledTask -Action $action -Principal $principal
Register-ScheduledTask -TaskName "CleanToolInteractiveUI" -InputObject $task -Force | Out-Null
Start-ScheduledTask -TaskName "CleanToolInteractiveUI"
Start-Sleep -Milliseconds 1500
Unregister-ScheduledTask -TaskName "CleanToolInteractiveUI" -Confirm:$false | Out-Null
Write-Host "CleanTool UI launched successfully on user desktop."
