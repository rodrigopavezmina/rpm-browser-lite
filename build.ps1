param(
    [switch]$SkipInstaller,
    [string]$ArtifactLabel = '1.0.0',
    [string]$BuildFolder = 'build'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 2.0

if ($ArtifactLabel -notmatch '^[0-9A-Za-z.-]+$') {
    throw 'ArtifactLabel solo puede contener letras, números, puntos y guiones.'
}

$projectRoot = $PSScriptRoot
$packageVersion = '1.0.4129.50'
$packageRoot = Join-Path $projectRoot ".packages\Microsoft.Web.WebView2.$packageVersion"
$packageArchive = Join-Path $projectRoot ".packages\Microsoft.Web.WebView2.$packageVersion.zip"
$packageUrl = "https://api.nuget.org/v3-flatcontainer/microsoft.web.webview2/$packageVersion/microsoft.web.webview2.$packageVersion.nupkg"
$buildRoot = Join-Path $projectRoot $BuildFolder
$appBuild = Join-Path $buildRoot 'app'
$portableBuild = Join-Path $buildRoot 'RPM IA Job Apply Browser Lite Turbo Portable'
$payloadBuild = Join-Path $buildRoot 'installer-payload'
$artifacts = Join-Path $projectRoot 'artifacts'
$csc = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$staleArtifacts = New-Object System.Collections.Generic.List[string]

if (-not (Test-Path -LiteralPath $csc)) {
    throw '.NET Framework 4.x no está disponible en este equipo.'
}

if (-not (Test-Path -LiteralPath $packageRoot)) {
    New-Item -ItemType Directory -Force -Path (Split-Path $packageArchive) | Out-Null
    Invoke-WebRequest -Uri $packageUrl -OutFile $packageArchive
    Expand-Archive -Path $packageArchive -DestinationPath $packageRoot -Force
}

foreach ($target in @($appBuild, $portableBuild, $payloadBuild)) {
    if (Test-Path -LiteralPath $target) {
        $resolved = [IO.Path]::GetFullPath($target)
        if (-not $resolved.StartsWith([IO.Path]::GetFullPath($projectRoot), [StringComparison]::OrdinalIgnoreCase)) {
            throw "Ruta de compilación no válida: $resolved"
        }
        Remove-Item -LiteralPath $resolved -Recurse -Force
    }
    New-Item -ItemType Directory -Force -Path $target | Out-Null
}
New-Item -ItemType Directory -Force -Path $artifacts | Out-Null

function Move-StaleArtifact([string]$path) {
    if (Test-Path -LiteralPath $path) {
        $stalePath = $path + '.' + [Guid]::NewGuid().ToString('N') + '.old'
        Move-Item -LiteralPath $path -Destination $stalePath -Force
        $staleArtifacts.Add($stalePath)
    }
}

$coreDll = Join-Path $packageRoot 'lib\net462\Microsoft.Web.WebView2.Core.dll'
$formsDll = Join-Path $packageRoot 'lib\net462\Microsoft.Web.WebView2.WinForms.dll'
$loaderDll = Join-Path $packageRoot 'runtimes\win-x64\native\WebView2Loader.dll'
$iconGenerator = Join-Path $appBuild 'IconGenerator.exe'
$iconPath = Join-Path $buildRoot 'RPMIAJobApplyBrowserLiteTurbo.ico'
& $csc @('/nologo','/target:exe','/platform:anycpu','/optimize+',"/out:$iconGenerator",'/reference:System.dll','/reference:System.Drawing.dll',(Join-Path $projectRoot 'tools\IconGenerator.cs'))
if ($LASTEXITCODE -ne 0) { throw 'No se pudo crear el generador de icono.' }
& $iconGenerator $iconPath
if ($LASTEXITCODE -ne 0) { throw 'No se pudo crear el icono.' }
Remove-Item -LiteralPath $iconGenerator -Force
$sourceRoot = Join-Path $projectRoot 'src\ChatGPTLite'
$sources = @(
    (Join-Path $sourceRoot 'Program.cs'),
    (Join-Path $sourceRoot 'JsonStore.cs'),
    (Join-Path $sourceRoot 'AppController.cs'),
    (Join-Path $sourceRoot 'AppPaths.cs'),
    (Join-Path $sourceRoot 'BrowserWindow.cs'),
    (Join-Path $sourceRoot 'IconVisuals.cs'),
    (Join-Path $sourceRoot 'LibraryForm.cs'),
    (Join-Path $sourceRoot 'Models.cs'),
    (Join-Path $sourceRoot 'SettingsForm.cs'),
    (Join-Path $sourceRoot 'ExtensionsForm.cs'),
    (Join-Path $sourceRoot 'JobApplyModels.cs'),
    (Join-Path $sourceRoot 'JobApplyStore.cs'),
    (Join-Path $sourceRoot 'JobApplyEngine.cs'),
    (Join-Path $sourceRoot 'JobProfileForm.cs'),
    (Join-Path $sourceRoot 'UiTheme.cs'),
    (Join-Path $sourceRoot 'WindowCustomizationForm.cs'),
    (Join-Path $sourceRoot 'AssemblyInfo.cs')
)
$appExe = Join-Path $appBuild 'RPMIAJobApplyBrowserLiteTurbo.exe'

$compilerArgs = @(
    '/nologo', '/target:winexe', '/platform:x64', '/optimize+', '/debug-',
    "/out:$appExe",
    "/win32icon:$iconPath",
    "/win32manifest:$(Join-Path $sourceRoot 'app.manifest')",
    "/resource:$(Join-Path $sourceRoot 'JobApplyScript.js'),ChatGPTLite.JobApplyScript.js",
    '/reference:System.dll', '/reference:System.Core.dll', '/reference:System.Drawing.dll',
    '/reference:System.Windows.Forms.dll', '/reference:System.Runtime.Serialization.dll',
    '/reference:System.Security.dll', '/reference:System.Web.Extensions.dll',
    "/reference:$coreDll", "/reference:$formsDll"
) + $sources

& $csc $compilerArgs
if ($LASTEXITCODE -ne 0) { throw 'La compilación de RPM IA Job Apply Browser Lite Turbo falló.' }

Copy-Item -LiteralPath $coreDll, $formsDll, $loaderDll -Destination $appBuild
Copy-Item -LiteralPath (Join-Path $sourceRoot 'ChatGPTLite.exe.config') -Destination (Join-Path $appBuild 'RPMIAJobApplyBrowserLiteTurbo.exe.config')

Copy-Item -Path (Join-Path $appBuild '*') -Destination $portableBuild -Recurse
New-Item -ItemType File -Path (Join-Path $portableBuild 'portable.flag') -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $projectRoot 'LEEME-JOB-APPLY.md') -Destination (Join-Path $portableBuild 'LEEME.md')

Copy-Item -Path (Join-Path $appBuild '*') -Destination $payloadBuild -Recurse
Copy-Item -LiteralPath (Join-Path $projectRoot 'LEEME-JOB-APPLY.md') -Destination (Join-Path $payloadBuild 'LEEME.md')

$portableFileName = "RPM-IA-Job-Apply-Browser-Lite-Turbo-Portable-$ArtifactLabel-x64.zip"
$installerFileName = "RPM-IA-Job-Apply-Browser-Lite-Turbo-Setup-$ArtifactLabel-x64.exe"
$hashFileName = "SHA256SUMS-$ArtifactLabel.txt"
$portableZip = Join-Path $artifacts $portableFileName
Move-StaleArtifact $portableZip
Compress-Archive -Path (Join-Path $portableBuild '*') -DestinationPath $portableZip -CompressionLevel Optimal

if (-not $SkipInstaller) {
    $installerSource = Join-Path $projectRoot 'src\Installer\Program.cs'
    $installerExe = Join-Path $artifacts $installerFileName
    Move-StaleArtifact $installerExe
$payloadFiles = @('RPMIAJobApplyBrowserLiteTurbo.exe','RPMIAJobApplyBrowserLiteTurbo.exe.config','Microsoft.Web.WebView2.Core.dll','Microsoft.Web.WebView2.WinForms.dll','WebView2Loader.dll','LEEME.md')
    $installerArgs = @(
        '/nologo', '/target:winexe', '/platform:x64', '/optimize+', '/debug-',
        "/out:$installerExe",
        "/win32icon:$iconPath",
        "/win32manifest:$(Join-Path $projectRoot 'src\Installer\app.manifest')",
        '/reference:System.dll', '/reference:System.Core.dll', '/reference:System.Drawing.dll',
        '/reference:System.Windows.Forms.dll', '/reference:Microsoft.CSharp.dll',
        $installerSource
    )
    foreach ($file in $payloadFiles) {
        $installerArgs += "/resource:$(Join-Path $payloadBuild $file),payload.$file"
    }
    & $csc $installerArgs
    if ($LASTEXITCODE -ne 0) { throw 'La compilación del instalador falló.' }
}

$hashPath = Join-Path $artifacts $hashFileName
Move-StaleArtifact $hashPath
$hashLines = Get-ChildItem -LiteralPath $artifacts -File | Where-Object { $_.Name -in @($portableFileName,$installerFileName) } | ForEach-Object {
    $hash = Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256
    "$($hash.Hash.ToLowerInvariant())  $($_.Name)"
}
Set-Content -LiteralPath $hashPath -Value $hashLines -Encoding ASCII

foreach ($staleArtifact in $staleArtifacts) {
    Remove-Item -LiteralPath $staleArtifact -Force -ErrorAction SilentlyContinue
}

Get-ChildItem -LiteralPath $artifacts | Select-Object Name, Length, LastWriteTime
