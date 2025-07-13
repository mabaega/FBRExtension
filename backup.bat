@echo off
echo ========================================
echo    FBR Extension Project Backup
echo ========================================
echo.

:: Check if PowerShell is available
powershell -Command "Get-Host" >nul 2>&1
if errorlevel 1 (
    echo ERROR: PowerShell is not available!
    echo Please install PowerShell to use this backup script.
    pause
    exit /b 1
)

:: Run the PowerShell backup script
echo Starting backup process...
echo.
powershell -ExecutionPolicy Bypass -File "%~dp0backup-project.ps1"

echo.
echo ========================================
echo Backup process finished!
echo ========================================
pause