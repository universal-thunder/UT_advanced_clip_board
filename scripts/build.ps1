param(
    [string]$OfflineRuntimeFeed,
    [string]$PackageCache
)
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$version = '0.1.0-beta'
$artifactRoot = Join-Path $repo 'artifacts'
$portable = Join-Path $artifactRoot 'portable'
$dist = Join-Path $artifactRoot 'dist'
$installer = Join-Path $repo 'installer'
New-Item -ItemType Directory -Force -Path $portable,$dist | Out-Null
$arguments = @('publish',(Join-Path $repo 'src\AdvancedClipboard\AdvancedClipboard.csproj'),'-c','Release','-o',$portable,'-p:DebugType=None')
if ($OfflineRuntimeFeed) { $arguments += @('--source',$OfflineRuntimeFeed,'-p:NuGetAudit=false') }
if ($PackageCache) { $arguments += @('--packages',$PackageCache) }
& dotnet @arguments
if ($LASTEXITCODE -ne 0) { throw 'Main application publish failed.' }
& (Join-Path $portable 'AdvancedClipboard.exe') --self-test
if ($LASTEXITCODE -ne 0) { throw 'Application self-test failed.' }

$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (!(Test-Path -LiteralPath $compiler)) { throw 'Windows .NET Framework C# compiler not found.' }
$shared = Join-Path $installer 'InstallEngine.cs'
$references = @('/reference:System.Windows.Forms.dll','/reference:System.Drawing.dll','/reference:System.IO.Compression.dll','/reference:System.IO.Compression.FileSystem.dll','/reference:Microsoft.CSharp.dll')
$common = @('/nologo','/platform:x64','/optimize+','/utf8output','/codepage:65001',('/win32manifest:' + (Join-Path $repo 'src\AdvancedClipboard\app.manifest'))) + $references
& $compiler @common /target:winexe ('/out:' + (Join-Path $portable 'Uninstall.exe')) $shared (Join-Path $installer 'Uninstall.cs')
if ($LASTEXITCODE -ne 0) { throw 'Uninstaller compilation failed.' }
Copy-Item -LiteralPath (Join-Path $repo 'LICENSE') -Destination (Join-Path $portable 'LICENSE.txt') -Force
Copy-Item -LiteralPath (Join-Path $repo 'docs\USAGE.md') -Destination (Join-Path $portable 'USAGE.md') -Force
Copy-Item -LiteralPath (Join-Path $repo 'third-party') -Destination $portable -Recurse -Force

# ZIP entries are taken from explicit release content, excluding logs and symbols.
$payload = Join-Path $artifactRoot 'payload.zip'
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
if (Test-Path -LiteralPath $payload) { Remove-Item -LiteralPath $payload }
$zip = [System.IO.Compression.ZipFile]::Open($payload,[System.IO.Compression.ZipArchiveMode]::Create)
try {
    foreach ($file in Get-ChildItem -LiteralPath $portable -Recurse -File) {
        if ($file.Extension -in @('.log','.pdb') -or $file.Name -eq 'install-manifest.txt') { continue }
        $relative = $file.FullName.Substring($portable.Length + 1).Replace('\','/')
        [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip,$file.FullName,$relative,[System.IO.Compression.CompressionLevel]::Optimal) | Out-Null
    }
} finally { $zip.Dispose() }
$setup = Join-Path $dist "AdvancedClipboard-Setup-v$version-win-x64.exe"
& $compiler @common /target:winexe ('/out:' + $setup) ('/resource:' + $payload + ',payload.zip') ('/resource:' + (Join-Path $repo 'LICENSE') + ',LICENSE') $shared (Join-Path $installer 'Setup.cs')
if ($LASTEXITCODE -ne 0) { throw 'Installer compilation failed.' }
$testExe = Join-Path $artifactRoot 'InstallerTests.exe'
& $compiler @common /target:exe ('/out:' + $testExe) ('/resource:' + $payload + ',payload.zip') $shared (Join-Path $installer 'InstallerTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Installer tests compilation failed.' }
$testRoot = Join-Path $artifactRoot ('install-test-' + [guid]::NewGuid().ToString('N'))
& $testExe $testRoot
if ($LASTEXITCODE -ne 0) { throw 'Installer integration tests failed.' }

# Portable archive does not need an installed-app uninstaller or manifest.
$portableZip = Join-Path $dist "AdvancedClipboard-v$version-win-x64-portable.zip"
if (Test-Path -LiteralPath $portableZip) { Remove-Item -LiteralPath $portableZip }
$zip = [System.IO.Compression.ZipFile]::Open($portableZip,[System.IO.Compression.ZipArchiveMode]::Create)
try {
    foreach ($file in Get-ChildItem -LiteralPath $portable -Recurse -File) {
        if ($file.Extension -in @('.log','.pdb') -or $file.Name -in @('install-manifest.txt','Uninstall.exe')) { continue }
        $relative = $file.FullName.Substring($portable.Length + 1).Replace('\','/')
        [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip,$file.FullName,$relative,[System.IO.Compression.CompressionLevel]::Optimal) | Out-Null
    }
} finally { $zip.Dispose() }
$hashes = foreach ($file in @($setup,$portableZip)) {
    $hash = Get-FileHash -LiteralPath $file -Algorithm SHA256
    $hash.Hash.ToLowerInvariant() + '  ' + (Split-Path -Leaf $file)
}
$hashes | Set-Content -LiteralPath (Join-Path $dist 'SHA256SUMS.txt') -Encoding ASCII
Write-Host "Build and tests passed. Release files: $dist"
