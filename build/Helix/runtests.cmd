robocopy %HELIX_CORRELATION_PAYLOAD% . /s /NP

dir /b /s

reg add HKLM\Software\ElevateIfNeeded /f 
if not errorlevel 1 (
    reg delete HKLM\Software\ElevateIfNeeded /f >nul
    echo elevated
) else (
    echo not elevated
)

cd scripts
TDRDump.exe > tdrdump.txt
echo Uploading tdrdump.txt to "%HELIX_RESULTS_CONTAINER_URI%/tdrdump.txt%HELIX_RESULTS_CONTAINER_RSAS%"
%HELIX_PYTHONPATH% %HELIX_SCRIPT_ROOT%\upload_result.py -result tdrdump.txt -result_name tdrdump.txt
cd ..

cd scripts
powershell -ExecutionPolicy Bypass .\runappxdiag.ps1
cd ..

REM take a screenshot:
te MUXControls.Test.dll /unicodeOutput:false /screenCaptureOnError /name:ffddfdfdf

powershell -ExecutionPolicy Bypass -Command "Wait-Process AppxDiag -Timeout 120"

FOR %%I in (scripts\*.zip) DO (
    echo Uploading %%I to "%HELIX_RESULTS_CONTAINER_URI%/%%~nI%%~xI%HELIX_RESULTS_CONTAINER_RSAS%"
    %HELIX_PYTHONPATH% %HELIX_SCRIPT_ROOT%\upload_result.py -result %%I -result_name %%~nI%%~xI 
)

REM te MUXControls.Test.dll MUXControlsTestApp.appx IXMPTestApp.appx /enablewttlogging /unicodeOutput:false /sessionTimeout:0:15 /testtimeout:0:10 /screenCaptureOnError %* 

REM %HELIX_PYTHONPATH% %HELIX_SCRIPT_ROOT%\upload_result.py -result te.wtl -result_name te.wtl

FOR %%I in (WexLogFileOutput\*.jpg) DO (
    echo Uploading %%I to "%HELIX_RESULTS_CONTAINER_URI%/%%~nI%%~xI%HELIX_RESULTS_CONTAINER_RSAS%"
    %HELIX_PYTHONPATH% %HELIX_SCRIPT_ROOT%\upload_result.py -result %%I -result_name %%~nI%%~xI 
)

cd scripts
powershell -ExecutionPolicy Bypass .\ConvertWttLogToXUnit.ps1 ..\te.wtl ..\testResults.xml %testnameprefix%
cd ..

type testResults.xml