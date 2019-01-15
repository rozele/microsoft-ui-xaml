$wshell = New-Object -ComObject wscript.shell;
$wshell.Run("AppxDiag.exe")
Start-Sleep 5
$wshell.SendKeys('N')
Wait-Process AppxDiag