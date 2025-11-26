@echo off

set PLUGIN_NAME=COM3D2.SkinMerge
set ASSEMBLY_INFO_CS=src/Shared/Properties/AssemblyInfo.Common.cs

for /f "tokens=*" %%i in ('powershell -Command ^
    "$content = Get-Content '%ASSEMBLY_INFO_CS%'; " ^
    "$m = [regex]::Match($content, 'PluginVersion\s*=\s*\""(.*?)\""'); " ^
    "if ($m.Success) { $m.Groups[1].Value }"') do set VERSION=%%i
echo VERSION: %VERSION%

if exist packages rmdir /s /q packages
md packages\%PLUGIN_NAME%-BepInEx\BepinEx\plugins
md packages\%PLUGIN_NAME%-Sybaris\Sybaris\UnityInjector

copy src\BepInEx\obj\Release\%PLUGIN_NAME%.dll packages\%PLUGIN_NAME%-BepInEx\BepinEx\plugins\
copy src\Sybaris\Plugin\obj\Release\%PLUGIN_NAME%.dll packages\%PLUGIN_NAME%-Sybaris\Sybaris\UnityInjector\
copy src\Sybaris\Managed\obj\Release\%PLUGIN_NAME%.Managed.dll packages\%PLUGIN_NAME%-Sybaris\Sybaris\
copy src\Sybaris\Patcher\obj\Release\%PLUGIN_NAME%.Patcher.dll packages\%PLUGIN_NAME%-Sybaris\Sybaris\
call tools\readme.bat packages\%PLUGIN_NAME%-BepInEx\README.pdf
copy packages\%PLUGIN_NAME%-BepInEx\README.pdf packages\%PLUGIN_NAME%-Sybaris\README.pdf

powershell Compress-Archive ^
    -Path "packages\%PLUGIN_NAME%-BepInEx" ^
    -DestinationPath "packages\%PLUGIN_NAME%-v%VERSION%-BepInEx.zip" ^
    -Force
powershell Compress-Archive ^
    -Path "packages\%PLUGIN_NAME%-Sybaris" ^
    -DestinationPath "packages\%PLUGIN_NAME%-v%VERSION%-Sybaris.zip" ^
    -Force

rmdir /s /q packages\%PLUGIN_NAME%-BepInEx
rmdir /s /q packages\%PLUGIN_NAME%-Sybaris
