@echo off
setlocal
cd /d "%~dp0"
title Library Manager - Setup and Run

echo ============================================
echo   Library Manager - Setup and Run
echo ============================================
echo.

REM Check .NET SDK
where dotnet >nul 2>nul
if errorlevel 1 goto NoDotnet

for /f "tokens=*" %%v in ('dotnet --version') do set "DOTNETVER=%%v"
echo [OK] .NET SDK found, version: %DOTNETVER%

echo.
echo [1/3] Restoring NuGet packages...
dotnet restore
if errorlevel 1 goto Fail

echo.
echo [2/3] Building application...
dotnet build
if errorlevel 1 goto Fail

echo.
echo [3/3] Starting application...
dotnet run --no-build
if errorlevel 1 goto Fail

echo.
echo Application closed.
pause
exit /b 0

:NoDotnet
echo [ERROR] .NET SDK not found!
echo Please install .NET 8 SDK from:
echo https://dotnet.microsoft.com/download
pause
exit /b 1

:Fail
echo.
echo [ERROR] Setup or application execution failed. Check the messages above.
pause
exit /b 1
