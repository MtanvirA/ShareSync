@echo off
title ShareSync
cd /d "%~dp0"

echo.
echo  ============================================
echo             S H A R E S Y N C
echo          MySQL 8.x Web Application
echo  ============================================
echo.

:: -- Locate dotnet ----------------------------------------------------------
set "DOTNET="

if exist "C:\Program Files\dotnet\dotnet.exe" (
    set "DOTNET=C:\Program Files\dotnet\dotnet.exe"
)

if not defined DOTNET (
    if exist "C:\Users\%USERNAME%\AppData\Local\Programs\dotnet\dotnet.exe" (
        set "DOTNET=C:\Users\%USERNAME%\AppData\Local\Programs\dotnet\dotnet.exe"
    )
)

if not defined DOTNET (
    where dotnet >nul 2>&1
    if %errorlevel% equ 0 set "DOTNET=dotnet"
)

if not defined DOTNET (
    echo  [ERROR] dotnet.exe was not found.
    echo.
    echo  Please install the .NET SDK from:
    echo  https://aka.ms/dotnet/download
    echo.
    pause
    exit /b 1
)

:: -- Check MySQL Service ----------------------------------------------------
sc query MySQL80 >nul 2>&1
if %errorlevel% equ 0 (
    echo  [OK] MySQL Service [MySQL80] detected.
) else (
    echo  [NOTE] Ensure MySQL 8.x Server is running on port 3306.
)

:: -- Ensure Local Connection Configuration Exists ---------------------------
if not exist "src\ShareSync.Web\appsettings.Local.json" (
    powershell -NoProfile -Command "try { Add-Type -TypeDefinition 'using System; using System.Runtime.InteropServices; public class C { [DllImport(\"\"Advapi32.dll\"\", EntryPoint=\"\"CredReadW\"\", CharSet=CharSet.Unicode)] public static extern bool CredRead(string t, int ty, int r, out IntPtr p); [StructLayout(LayoutKind.Sequential, CharSet=CharSet.Unicode)] public struct CR { public int F, T; public string TN, C; public long L; public int S; public IntPtr B; public int P, AC; public IntPtr A; public string TA, U; } public static string G(string t) { IntPtr p; if (CredRead(t, 1, 0, out p)) { CR c = (CR)Marshal.PtrToStructure(p, typeof(CR)); byte[] b = new byte[c.S]; Marshal.Copy(c.B, b, 0, c.S); return System.Text.Encoding.UTF8.GetString(b); } return null; } }'; `$pwd = [C]::G('Oracle|mysql-secret-store-windows-credential|password|root@localhost:3306'); if (`$pwd) { @{ ConnectionStrings = @{ MySqlConnection = \"Server=localhost;Port=3306;Database=sharesync;User=root;Password=$pwd;CharSet=utf8mb4;\" } } | ConvertTo-Json | Set-Content 'src\ShareSync.Web\appsettings.Local.json'; exit 0 } else { exit 1 } } catch { exit 1 }" >nul 2>&1
)

:: -- Check if already running ------------------------------------------------
echo  [1/3] Checking if ShareSync server is already running...
powershell -Command "try { `$r = Invoke-WebRequest -Uri 'http://localhost:5000/api/health' -UseBasicParsing -TimeoutSec 2 -ErrorAction Stop; exit 0 } catch { exit 1 }" >nul 2>&1
if %errorlevel% equ 0 (
    echo  [OK] ShareSync is already running on http://localhost:5000
    goto OPEN_BROWSER
)

:: -- Clean up any stale locked process ---------------------------------------
taskkill /F /IM ShareSync.Web.exe >nul 2>&1

:: -- Start the API server ----------------------------------------------------
echo  [1/3] Starting ShareSync backend on http://localhost:5000 ...
echo.

start "ShareSync API" cmd /k ""%DOTNET%" run --project src\ShareSync.Web\ShareSync.Web.csproj --urls http://localhost:5000"

:: -- Wait for the server to become ready (up to 30 s) -----------------------
echo  [2/3] Waiting for server to become ready...

set /a WAIT=0
:WAIT_LOOP
ping -n 2 127.0.0.1 >nul

powershell -Command "try { Invoke-WebRequest -Uri 'http://localhost:5000/api/health' -UseBasicParsing -TimeoutSec 2 -ErrorAction Stop | Out-Null; exit 0 } catch { exit 1 }" >nul 2>&1
if %errorlevel% equ 0 goto READY

set /a WAIT+=1
if %WAIT% lss 30 goto WAIT_LOOP

echo  [!] Server did not respond in time. Opening browser anyway...
goto OPEN_BROWSER

:READY
echo  [OK] ShareSync Server is ready and healthy!

:: -- Open browser -----------------------------------------------------------
:OPEN_BROWSER
echo  [3/3] Opening ShareSync in your browser...
echo.

start "" "http://localhost:5000"

echo  ============================================
echo   ShareSync is running at http://localhost:5000
echo  --------------------------------------------
echo   DEMO INVESTOR LOGIN:
echo     Email:    investor@sharesync.com
echo     Password: Password123#
echo.
echo   PERSONAL INVESTOR LOGIN:
echo     Email:    tanvir@sharesync.com
echo     Password: Password123#
echo.
echo   ADMINISTRATOR LOGIN:
echo     Email:    admin@sharesync.com
echo     Password: Admin123#
echo  --------------------------------------------
echo   Or register a new account at:
echo     http://localhost:5000/register.html
echo  ============================================
echo.
pause
