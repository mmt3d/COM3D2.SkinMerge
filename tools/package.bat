@echo off

set PLUGIN_NAME=COM3D2.SkinMerge
set MAIN_CS=SkinMerge.cs

for /f "tokens=*" %%i in ('powershell -Command ^
    "$content = Get-Content '%PLUGIN_NAME%/%MAIN_CS%'; " ^
    "$m = [regex]::Match($content, 'PluginVersion\s*=\s*\""(.*?)\""'); " ^
    "if ($m.Success) { $m.Groups[1].Value }"') do set VERSION=%%i
echo VERSION: %VERSION%

if exist output rmdir /s /q output
md packages\%PLUGIN_NAME%\BepinEx\plugins

copy %PLUGIN_NAME%\obj\Release\%PLUGIN_NAME%.dll packages\%PLUGIN_NAME%\BepinEx\plugins\
copy docs\README.pdf packages\%PLUGIN_NAME%\README.pdf

powershell Compress-Archive ^
    -Path "packages\%PLUGIN_NAME%" ^
    -DestinationPath "packages\%PLUGIN_NAME%-v%VERSION%.zip" ^
    -Force

rmdir /s /q packages\%PLUGIN_NAME%
