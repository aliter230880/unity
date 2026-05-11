@echo off
setlocal
cd /d "%~dp0"
echo.
echo Starting Vibe Unity Developer...
echo Web UI: http://localhost:17861
echo.
start "" "http://localhost:17861"
node server\server.js
if errorlevel 1 (
  echo.
  echo Node.js is required. Install it from https://nodejs.org and run this file again.
  pause
)

