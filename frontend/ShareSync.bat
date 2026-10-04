@echo off
title ShareSync
cd /d "%~dp0"

echo ========================================
echo           ShareSync Launcher
echo ========================================
echo.

where python >nul 2>&1

if %errorlevel% equ 0 (
    echo Starting ShareSync...
    echo.
    echo Opening http://localhost:5501
    echo.
    start "" "http://localhost:5501"
    python -m http.server 5501
) else (
    echo Python was not found.
    echo Opening ShareSync directly...
    echo.
    start "" "%~dp0index.html"
)

pause