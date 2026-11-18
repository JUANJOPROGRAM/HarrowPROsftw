param(
    [string]$Configuration = 'Release',
    [string]$RuntimeIdentifier = 'win-x64'
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$appProject = Join-Path $root 'src\HarrowStudio.Desktop\HarrowStudio.Desktop.csproj'
$setupProject = Join-Path $root 'src\HarrowStudio.Setup\HarrowStudio.Setup.csproj'
$outputRoot = Join-Path $root 'artifacts\installer'
$appPublish = Join-Path $outputRoot 'app-publish'
$packageDirectory = Join-Path $outputRoot 'package'
$setupPublish = Join-Path $outputRoot 'setup-publish'
$payloadZip = Join-Path $packageDirectory 'payload.zip'
$installer = Join-Path $root 'artifacts\HarrowStudioSetup.exe'
$packages = Join-Path $root '.packages'
$iconScript = Join-Path $PSScriptRoot 'generate-icon.ps1'

New-Item -ItemType Directory -Path $outputRoot -Force | Out-Null
foreach ($directory in @($appPublish, $packageDirectory, $setupPublish)) {
    if (Test-Path $directory) { Remove-Item -LiteralPath $directory -Recurse -Force }
    New-Item -ItemType Directory -Path $directory -Force | Out-Null
}

& powershell.exe -NoProfile -ExecutionPolicy Bypass -File $iconScript
if ($LASTEXITCODE -ne 0) { throw 'No se pudo generar el icono de Harrow Studio.' }

Write-Host "Publicando Harrow Studio autocontenido para $RuntimeIdentifier..."
& dotnet publish $appProject -c $Configuration -r $RuntimeIdentifier --self-contained true -o $appPublish "-p:RestorePackagesPath=$packages" -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:NuGetAudit=false -m:1
if ($LASTEXITCODE -ne 0) { throw 'Falló la publicación autocontenida de Harrow Studio.' }

Get-ChildItem -LiteralPath $appPublish -Filter '*.pdb' -File | Remove-Item -Force
Write-Host 'Comprimiendo los archivos de la aplicación...'
Compress-Archive -Path (Join-Path $appPublish '*') -DestinationPath $payloadZip -CompressionLevel Optimal

Write-Host 'Compilando el instalador de Windows...'
& dotnet publish $setupProject -c $Configuration -r $RuntimeIdentifier --self-contained true -o $setupPublish "-p:RestorePackagesPath=$packages" -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:NuGetAudit=false -m:1
if ($LASTEXITCODE -ne 0) { throw 'Falló la compilación del instalador.' }

Copy-Item -LiteralPath (Join-Path $setupPublish 'HarrowStudio.Setup.exe') -Destination $installer -Force
Write-Host "Instalador listo: $installer"
