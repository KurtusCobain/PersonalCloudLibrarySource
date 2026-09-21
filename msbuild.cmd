@echo off
setlocal
set "REAL_MSBUILD="
for /f "delims=" %%I in ('where MSBuild.exe 2^>nul') do if not defined REAL_MSBUILD set "REAL_MSBUILD=%%I"
if not defined REAL_MSBUILD (
  echo MSBuild.exe was not found on PATH.
  exit /b 9009
)
"%REAL_MSBUILD%" %* /nologo /clp:ErrorsOnly;Summary
exit /b %ERRORLEVEL%
