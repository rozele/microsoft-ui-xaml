Write-Host creating shell
$wshell = New-Object -ComObject wscript.shell;
Write-Host running appxdiag
$code = $wshell.Run("AppxDiag.exe", 3)
Write-Host exit code: $code
Write-Host sleeping
Start-Sleep 10
Write-Host sending keys
$wshell.SendKeys('N')
Write-Host waiting for appxdiag to exit
Wait-Process AppxDiag -Timeout 240

$proc = Get-Process AppxDiag
if($proc)
{
    Write-Host "AppxDiag did not exit!"
}
else
{
    Write-Host "AppxDiag exited"
}

Write-Host done