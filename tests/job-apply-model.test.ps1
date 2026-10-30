$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$csc = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $csc)) { throw 'csc.exe no está disponible.' }

$testBuild = Join-Path $PSScriptRoot '.job-apply-model-test-build'
if (Test-Path -LiteralPath $testBuild) { Remove-Item -LiteralPath $testBuild -Recurse -Force }
New-Item -ItemType Directory -Path $testBuild -Force | Out-Null
try {
    $output = Join-Path $testBuild 'JobApplyModelTests.exe'
    $sources = @(
        (Join-Path $root 'src\ChatGPTLite\JobApplyModels.cs'),
        (Join-Path $root 'src\ChatGPTLite\JobApplyStore.cs'),
        (Join-Path $PSScriptRoot 'JobApplyModelTests.cs')
    )
    $args = @(
        '/nologo', '/target:exe', '/platform:x64', '/optimize+', "/out:$output",
        '/reference:System.dll', '/reference:System.Runtime.Serialization.dll',
        '/reference:System.Security.dll', '/reference:System.Web.Extensions.dll'
    ) + $sources
    & $csc $args
    if ($LASTEXITCODE -ne 0) { throw 'La compilación de pruebas del modelo falló.' }
    & $output
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}
finally {
    if (Test-Path -LiteralPath $testBuild) { Remove-Item -LiteralPath $testBuild -Recurse -Force }
}
