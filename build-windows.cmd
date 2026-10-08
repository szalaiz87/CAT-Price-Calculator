@echo off
cd /d "%~dp0"
dotnet publish src\CatPriceCalculator\CatPriceCalculator.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o artifacts\win-x64
if errorlevel 1 exit /b 1
echo EXE: artifacts\win-x64\CAT-Price-Calculator.exe
