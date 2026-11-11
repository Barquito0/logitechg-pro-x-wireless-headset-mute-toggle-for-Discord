param([switch]$Installer, [switch]$Test)
$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) { throw 'The Windows .NET Framework C# compiler was not found.' }
$outputFolder = Join-Path $PSScriptRoot 'dist'
New-Item -ItemType Directory -Path $outputFolder -Force | Out-Null
$response = Join-Path $outputFolder 'build.rsp'
$sources = @(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'src') -Filter '*.cs' | Sort-Object Name)
$arguments = @('/nologo','/target:winexe','/platform:x64','/optimize+', '/warn:4', '/reference:System.Windows.Forms.dll', '/reference:System.Drawing.dll', '/reference:System.Xml.dll', '/reference:System.Core.dll',
    ('/out:"{0}"' -f (Join-Path $outputFolder 'HeadsetButtonBridge.exe')),
    ('/win32manifest:"{0}"' -f (Join-Path $PSScriptRoot 'packaging/app.manifest')))
$arguments += @($sources | ForEach-Object { '"{0}"' -f $_.FullName })
$arguments | Set-Content -LiteralPath $response -Encoding UTF8
& $compiler ('@' + $response)
if ($LASTEXITCODE -ne 0) { throw 'Application build failed.' }
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'packaging/HeadsetButtonBridge.exe.config') -Destination $outputFolder -Force
if ($Test) {
    $testResponse = Join-Path $outputFolder 'tests.rsp'
    @('/nologo','/target:exe','/platform:x64','/optimize+', '/reference:System.Core.dll','/reference:System.Xml.dll',
        ('/out:"{0}"' -f (Join-Path $outputFolder 'BridgeTests.exe')),
        ('"{0}"' -f (Join-Path $PSScriptRoot 'src/Settings.cs')),
        ('"{0}"' -f (Join-Path $PSScriptRoot 'src/ReportDetector.cs')),
        ('"{0}"' -f (Join-Path $PSScriptRoot 'tests/BridgeTests.cs'))
    ) | Set-Content -LiteralPath $testResponse -Encoding UTF8
    & $compiler ('@' + $testResponse)
    if ($LASTEXITCODE -ne 0) { throw 'Test build failed.' }
    & (Join-Path $outputFolder 'BridgeTests.exe')
    if ($LASTEXITCODE -ne 0) { throw 'Tests failed.' }
}
if ($Installer) {
    $innoCompiler = Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6/ISCC.exe'
    if (-not (Test-Path -LiteralPath $innoCompiler)) { $innoCompiler = (Get-Command ISCC.exe -ErrorAction Stop).Source }
    & $innoCompiler (Join-Path $PSScriptRoot 'packaging/installer.iss')
    if ($LASTEXITCODE -ne 0) { throw 'Installer build failed.' }
}
Get-Item -LiteralPath (Join-Path $outputFolder 'HeadsetButtonBridge.exe') | Select-Object FullName,Length
