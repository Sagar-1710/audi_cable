@echo off
title DualAudio Hub - Virtual Audio Cable Clone
echo ========================================================
echo  DUALAUDIO HUB: Starting Web & Routing Engine...
echo ========================================================
cd /d "%~dp0"

start "" http://127.0.0.1:40880
node server.js

pause
