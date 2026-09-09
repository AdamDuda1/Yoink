<#
.SYNOPSIS
    Builds every distributable flavour of Yoink Downloader into .\dist.

.DESCRIPTION
    Produces three outputs:

      dist\<platform>\self-contained\   One .exe, ~130 MB. No prerequisites on the target
                                        machine. Slow first launch - the single file
                                        self-extracts to temp.

      dist\<platform>\portable\         A folder, ~77 MB across ~49 files, and it STILL
                                        needs the .NET Desktop Runtime and the Windows
                                        App SDK runtime installed on the target machine.
                                        Most of the bulk is onnxruntime.dll, DirectML.dll
                                        and the WinRT projection assembly, which ship with
                                        the app either way. Only worth it if you are
                                        shipping updates often and care about the delta.

      dist\<platform>\msix\             MSIX package. In StoreUpload mode this is the
                                        .msixupload you submit to Partner Center.

.PARAMETER MsixMode
    StoreUpload  - for Microsoft Store submission. Left UNSIGNED on purpose: the Store
                   signs it with their own certificate during certification.
    SideloadOnly - for handing the file to someone directly. MUST be signed, otherwise
                   Windows refuses to install it. Requires -CertificateThumbprint.

.PARAMETER CertificateThumbprint
    Optional. Thumbprint of a code-signing certificate in your certificate store. When
    given, the two .exe outputs are signed, and a SideloadOnly MSIX is signed too.
    Without it nothing is signed - the exes still run, but SmartScreen will warn users.

.EXAMPLE
    .\build.ps1
    Everything for x64, unsigned, MSIX ready for the Store.

.EXAMPLE
    .\build.ps1 -Platform arm64 -CertificateThumbprint A1B2C3...
    Signed ARM64 build.
#>
[CmdletBinding()]
param(
    [ValidateSet('x64', 'x86', 'arm64')]
    [string] $Platform = 'x64',

    [string] $Configuration = 'Release',

    [ValidateSet('StoreUpload', 'SideloadOnly')]
    [string] $MsixMode = 'StoreUpload',

    [string] $CertificateThumbprint,

    [string] $TimestampUrl = 'http://timestamp.digicert.com',

    [switch] $SkipMsix,

    [switch] $SkipExe
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$root = $PSScriptRoot
$project = Join-Path $root 'Yoink Downloader.csproj'
$distRoot = Join-Path $root "dist\$Platform"
$rid = "win-$Platform"

function Write-Step($text) {
    Write-Host ''
    Write-Host "==> $text" -ForegroundColor Cyan
}

function Get-MSBuild {
    $vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
    if (-not (Test-Path $vswhere)) {
        throw "vswhere.exe not found. Visual Studio is required to build the MSIX package."
    }

    $msbuild = & $vswhere -latest -products * -requires Microsoft.Component.MSBuild `
        -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1

    if (-not $msbuild) {
        throw "MSBuild not found. Install the '.NET desktop development' workload."
    }

    return $msbuild
}

function Get-SignTool {
    $signtool = Get-ChildItem (Join-Path ${env:ProgramFiles(x86)} 'Windows Kits\10\bin') `
        -Recurse -Filter 'signtool.exe' -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -match '\\x64\\' } |
        Sort-Object FullName -Descending |
        Select-Object -First 1

    if (-not $signtool) {
        throw "signtool.exe not found. Install the Windows SDK."
    }

    return $signtool.FullName
}

function Invoke-Sign([string[]] $Paths) {
    if (-not $CertificateThumbprint) { return }

    $signtool = Get-SignTool
    Write-Step "Signing $($Paths.Count) file(s)"

    foreach ($path in $Paths) {
        & $signtool sign /fd SHA256 /td SHA256 /tr $TimestampUrl /sha1 $CertificateThumbprint $path
        if ($LASTEXITCODE -ne 0) { throw "Signing failed for $path" }
    }
}

# --------------------------------------------------------------------------------------

if (Test-Path $distRoot) {
    Remove-Item $distRoot -Recurse -Force
}
New-Item -ItemType Directory -Path $distRoot -Force | Out-Null

$signTargets = @()

if (-not $SkipExe) {

    # 1. Self-contained single .exe --------------------------------------------------
    $selfContainedDir = Join-Path $distRoot 'self-contained'
    Write-Step 'Building self-contained .exe (this one is slow)'

    dotnet publish $project `
        -c $Configuration `
        -r $rid `
        -p:Platform=$Platform `
        -p:Packaging=SelfContained `
        -p:PublishDir="$selfContainedDir\" `
        --nologo
    if ($LASTEXITCODE -ne 0) { throw 'Self-contained publish failed.' }

    $signTargets += (Join-Path $selfContainedDir 'Yoink Downloader.exe')

    # 2. Framework-dependent folder ---------------------------------------------------
    $portableDir = Join-Path $distRoot 'portable'
    Write-Step 'Building framework-dependent output'

    dotnet publish $project `
        -c $Configuration `
        -r $rid `
        -p:Platform=$Platform `
        -p:Packaging=FrameworkDependent `
        -p:PublishDir="$portableDir\" `
        --nologo
    if ($LASTEXITCODE -ne 0) { throw 'Framework-dependent publish failed.' }

    $signTargets += (Join-Path $portableDir 'Yoink Downloader.exe')

    Invoke-Sign $signTargets
}

if (-not $SkipMsix) {

    # 3. MSIX -------------------------------------------------------------------------
    $msixDir = Join-Path $distRoot 'msix'
    Write-Step "Building MSIX package ($MsixMode)"

    $signMsix = ($MsixMode -eq 'SideloadOnly' -and $CertificateThumbprint)

    if ($MsixMode -eq 'SideloadOnly' -and -not $CertificateThumbprint) {
        Write-Warning 'A sideload MSIX must be signed to be installable. Pass -CertificateThumbprint, or Windows will reject the package.'
    }

    $msbuild = Get-MSBuild
    $msbuildArgs = @(
        $project
        '/restore'
        "/p:Configuration=$Configuration"
        "/p:Platform=$Platform"
        "/p:AppxPackageDir=$msixDir\"
        "/p:UapAppxPackageBuildMode=$MsixMode"
        '/p:GenerateAppxPackageOnBuild=true'
        '/p:AppxBundle=Never'
        # Symbol bundles (.appxsym) need mspdbcmf.exe from the C++ build tools. Without
        # that the StoreUpload build fails outright, so skip symbols. Partner Center only
        # uses them for crash call stacks - install the "Desktop development with C++"
        # workload and flip this to true if you want them later.
        '/p:AppxSymbolPackageEnabled=false'
        "/p:AppxPackageSigningEnabled=$($signMsix.ToString().ToLower())"
        '/verbosity:minimal'
        '/nologo'
    )

    if ($signMsix) {
        $msbuildArgs += "/p:PackageCertificateThumbprint=$CertificateThumbprint"
    }

    & $msbuild @msbuildArgs
    if ($LASTEXITCODE -ne 0) { throw 'MSIX build failed.' }
}

# --------------------------------------------------------------------------------------

Write-Step 'Done'

Get-ChildItem $distRoot -Recurse -Include '*.exe', '*.msix', '*.msixupload', '*.msixbundle' |
    ForEach-Object {
        $size = '{0,8:N1} MB' -f ($_.Length / 1MB)
        Write-Host "  $size  $($_.FullName.Substring($root.Length + 1))"
    }

if (-not $CertificateThumbprint) {
    Write-Host ''
    Write-Host '  Nothing was signed (no -CertificateThumbprint given).' -ForegroundColor Yellow
    if ($MsixMode -eq 'StoreUpload') {
        Write-Host '  That is correct for the Store package - Microsoft signs it during certification.' -ForegroundColor Yellow
    }
}
