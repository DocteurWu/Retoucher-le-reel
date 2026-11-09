@echo off
chcp 65001 >nul
title Configuration ALVR Cable USB
echo =======================================================
echo   CONFIGURATION ALVR PAR CABLE USB (RETOUCHER LE REEL)
echo =======================================================
echo.

set "ADB=C:\Users\VR\Desktop\alvr_launcher_windows\installations\v20.14.1\adb.exe"

:check_device
echo Verification du casque Meta Quest...
"%ADB%" devices | findstr "device\>" >nul
if %errorlevel% equ 0 goto authorized

"%ADB%" devices | findstr "unauthorized" >nul
if %errorlevel% equ 0 (
    echo.
    echo -------------------------------------------------------------
    echo [ACTION REQUISE DANS LE CASQUE]
    echo Le casque est detecte mais NON AUTORISE !
    echo 1. Mettez le casque sur votre tete.
    echo 2. Cochez "Toujours autoriser depuis cet ordinateur".
    echo 3. Cliquez sur "Autoriser" (Debogage USB).
    echo -------------------------------------------------------------
    echo.
    pause
    goto check_device
)

echo.
echo [ATTENTION] Aucun casque detecte sur le port USB !
echo Verifiez que le cable USB-C est branche et que le casque est allume.
echo.
pause
goto check_device

:authorized
echo.
echo [1/3] Casque Quest detecte et autorise en USB !
echo [2/3] Nettoyage des ports pour eviter "os error 98"...
"%ADB%" forward --remove-all >nul 2>&1
"%ADB%" reverse --remove-all >nul 2>&1
"%ADB%" shell am force-stop alvr.client >nul 2>&1

echo [3/3] Configuration des redirections ADB (Forward)...
"%ADB%" forward tcp:8082 tcp:8082
"%ADB%" forward tcp:9943 tcp:9943
"%ADB%" forward tcp:9944 tcp:9944

echo.
echo =======================================================
echo   CONNEXION CABLE PRETE (PORTS LIBERES)
echo =======================================================
echo.
echo 1. Dans l'interface ALVR sur le PC :
echo    - Dans l'onglet "Devices", cliquez sur "Launch Wired"
echo.
echo 2. Dans le casque Meta Quest :
echo    - Lancez ALVR (ou il se lancera tout seul).
echo    - Si demande, connectez-vous a : 127.0.0.1
echo.
pause
