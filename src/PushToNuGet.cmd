for /F %%a in ('dir ..\*.nupkg /s/a/b ^| find /V /i "symbols" ^| find /V /i "\packages\" ^| find /V /i "Test" ^| find /i "Release"  ') do @.nuget\nuget push %%a -s %NUGET_BASE_URL% -ApiKey password 
