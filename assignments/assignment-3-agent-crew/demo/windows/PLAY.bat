@echo off
rem Yokai Fighters demo launcher: checks the build is complete, then starts the game.
setlocal
cd /d "%~dp0"

if not exist "YokaiFighters.exe" goto missing
for %%F in ("YokaiFighters.exe") do set EXE_SIZE=%%~zF
if %EXE_SIZE% LSS 1000000 goto lfs
if not exist "data_YokaiFighters_windows_x86_64\" goto missing
if not exist "data\moves\" goto missing

start "" "YokaiFighters.exe"
exit /b 0

:lfs
echo YokaiFighters.exe is only a Git LFS pointer (%EXE_SIZE% bytes), not the real game.
echo Install Git LFS (https://git-lfs.com), then run in the repo:  git lfs pull
echo Or download the release zip: https://github.com/Andreas203/yokai-fighters/releases/tag/demo-v0.1
pause
exit /b 1

:missing
echo The game folder is incomplete. YokaiFighters.exe needs these next to it:
echo   data_YokaiFighters_windows_x86_64\   data\   fixtures\
echo Run PLAY.bat from inside this folder (do not copy the exe out, or run it from inside a zip).
pause
exit /b 1
