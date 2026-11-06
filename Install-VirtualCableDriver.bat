@echo off
title Install Virtual Audio Cable Driver
echo ========================================================
echo  Installing Virtual Audio Cable Driver (VB-CABLE)
echo ========================================================
echo.
echo Launching 64-bit Virtual Cable Driver Installer as Administrator...
powershell -NoProfile -Command "Start-Process '%~dp0virtual_cable_driver\VBCABLE_Setup_x64.exe' -Verb RunAs"
echo.
echo Please click "Install Driver" on the installer window.
echo Once installed, "CABLE Input" will appear in your Windows Sound menu!
echo.
pause
