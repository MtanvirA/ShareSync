@echo off
title ShareSync
cd /d "%~dp0"

echo.
echo  ============================================
echo             S H A R E S Y N C
echo  ============================================
echo.

:: -- Locate dotnet ----------------------------------------------------------
set "DOTNET="

if exist "C:\Users\%USERNAME%\AppData\Local\Programs\dotnet\dotnet.exe" (
    set "DOTNET=C:\Users\%USERNAME%\AppData\Local\Programs\dotnet\dotnet.exe"
)

if not defined DOTNET (
    where dotnet >nul 2>&1
    if %errorlevel% equ 0 set "DOTNET=dotnet"
)

if not defined DOTNET (
    echo  [ERROR] dotnet.exe was not found.
    echo.
    echo  Please install the .NET 8 SDK from:
    echo  https://aka.ms/dotnet/download
    echo.
    pause
    exit /b 1
)

:: -- Start the API server ----------------------------------------------------
echo  [1/3] Starting backend API on http://localhost:5000 ...
echo.

start "ShareSync API" cmd /k ""%DOTNET%" run --project src\ShareSync.Web\ShareSync.Web.csproj --urls http://localhost:5000"

:: -- Wait for the server to become ready (up to 30 s) -----------------------
echo  [2/3] Waiting for server to be ready...

set /a WAIT=0
:WAIT_LOOP
timeout /t 1 /nobreak >nul

powershell -Command "try { Invoke-WebRequest -Uri 'http://localhost:5000/api/health' -UseBasicParsing -TimeoutSec 2 -ErrorAction Stop | Out-Null; exit 0 } catch { exit 1 }" >nul 2>&1
if %errorlevel% equ 0 goto READY

set /a WAIT+=1
if %WAIT% lss 30 goto WAIT_LOOP

echo  [!] Server did not respond in time. Opening browser anyway...
goto OPEN_BROWSER

:READY
echo  [OK] Server is ready.

:: -- Open browser -----------------------------------------------------------
:OPEN_BROWSER
echo  [3/3] Opening ShareSync in your browser...
echo.

start "" "http://localhost:5000"

echo  ============================================
echo   ShareSync is running at http://localhost:5000
echo   Close the API window or press Ctrl+C to stop.
echo  ============================================
echo.
pause
