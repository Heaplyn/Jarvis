Set WshShell = CreateObject("WScript.Shell")
' 0 hides the window, false returns control immediately without waiting for execution to end
WshShell.Run "cmd.exe /c run.bat", 0, false
