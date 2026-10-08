$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot
dotnet publish src/CatPriceCalculator/CatPriceCalculator.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:PublishTrimmed=false -p:DebugType=None -p:DebugSymbols=false -o artifacts/win-x64
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
