@echo off
setlocal
cd /d "%~dp0"
echo.
echo Paste the full path to your Unity project folder.
echo Example: C:\Users\USER\Documents\MyUnityGame
echo.
set /p UNITY_PROJECT=Unity project path: 
if "%UNITY_PROJECT%"=="" (
  echo No path entered.
  pause
  exit /b 1
)
if not exist "%UNITY_PROJECT%\Assets" (
  echo This does not look like a Unity project: Assets folder not found.
  pause
  exit /b 1
)
if not exist "%UNITY_PROJECT%\Assets\Editor" mkdir "%UNITY_PROJECT%\Assets\Editor"
copy /Y "unity-plugin\VibeUnityDeveloper.cs" "%UNITY_PROJECT%\Assets\Editor\VibeUnityDeveloper.cs"
echo.
echo Installed to:
echo %UNITY_PROJECT%\Assets\Editor\VibeUnityDeveloper.cs
echo.
echo In Unity open: Window ^> Vibe Coding ^> Fullstack Developer
pause

