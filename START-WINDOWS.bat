@echo off
setlocal EnableExtensions
cd /d "%~dp0"
title PlaceholderGuard Website Launcher

echo.
echo ==============================================
echo   PlaceholderGuard Website + AI Asset Studio
echo ==============================================
echo.

if not exist "server.js" (
  echo [ERROR] server.js was not found.
  echo Extract the entire ZIP first, then run this BAT from the extracted folder.
  pause
  exit /b 1
)

where node >nul 2>&1
if errorlevel 1 (
  echo [ERROR] Node.js is not installed or is not on PATH.
  echo Install Node.js 20 LTS or newer from https://nodejs.org/en/download
  echo Then close and reopen this window and run START-WINDOWS.bat again.
  pause
  exit /b 1
)

for /f "delims=" %%V in ('node -p "process.versions.node"') do set "NODE_VERSION=%%V"
for /f "tokens=1 delims=." %%M in ("%NODE_VERSION%") do set "NODE_MAJOR=%%M"
if not defined NODE_MAJOR (
  echo [ERROR] Could not read the installed Node.js version.
  pause
  exit /b 1
)
if %NODE_MAJOR% LSS 20 (
  echo [ERROR] Node.js %NODE_VERSION% is installed, but version 20 or newer is required.
  echo Update Node.js from https://nodejs.org/en/download and try again.
  pause
  exit /b 1
)

if not exist "public\index.html" (
  echo [ERROR] public\index.html is missing. Re-extract the website ZIP.
  pause
  exit /b 1
)
if not exist "downloads\PlaceholderGuard-0.2.0.zip" (
  echo [ERROR] The bundled PlaceholderGuard manager ZIP is missing.
  echo Re-extract the complete website ZIP.
  pause
  exit /b 1
)

if not exist ".env" (
  copy ".env.example" ".env" >nul
  if errorlevel 1 (
    echo [ERROR] Could not create .env from .env.example.
    pause
    exit /b 1
  )
  echo Created .env. AI features will remain disabled until you add GEMINI_API_KEY.
)

echo [OK] Node.js %NODE_VERSION% found.
echo [INFO] Starting the local server in a separate terminal...
start "PlaceholderGuard Server" /D "%~dp0" cmd /k node server.js

echo [INFO] Waiting for the server to become ready...
powershell -NoProfile -ExecutionPolicy Bypass -Command "$url='http://127.0.0.1:3000/api/health'; $deadline=(Get-Date).AddSeconds(30); do { try { $r=Invoke-WebRequest -Uri $url -UseBasicParsing -TimeoutSec 2; if ($r.StatusCode -eq 200) { Start-Process 'http://127.0.0.1:3000'; Write-Host '[OK] PlaceholderGuard is open in your browser.'; exit 0 } } catch { }; Start-Sleep -Milliseconds 500 } while ((Get-Date) -lt $deadline); Write-Host '[ERROR] The server did not become ready within 30 seconds.'; Write-Host 'Check the separate PlaceholderGuard Server terminal for the exact error.'; exit 1"
if errorlevel 1 (
  echo.
  echo The website could not be started automatically.
  echo Check the separate PlaceholderGuard Server terminal for details.
  pause
  exit /b 1
)

echo.
echo Website: http://127.0.0.1:3000
echo Keep the PlaceholderGuard Server terminal open while using the website.
echo Close that server terminal to stop the website.
timeout /t 5 >nul
exit /b 0
