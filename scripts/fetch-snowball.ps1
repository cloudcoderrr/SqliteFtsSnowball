#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Downloads the official Snowball "libstemmer" C# source distribution, applies the
    repository's unified-diff patches, and copies the stemmer sources into the
    SqliteFtsSnowball library project so they can be compiled into the NuGet.

.DESCRIPTION
    This is the single source of truth for the vendored Snowball stemmers. It is run
    by CI (build + test workflows) and by developers locally. The downloaded sources
    are intentionally NOT committed to the repository (they are reproducible from the
    pinned version plus the patches in ./patches/libstemmer_csharp).

    To bump the Snowball version later, change -Version (or the <SnowballVersion>
    property in Directory.Build.props) and, if the upstream layout/content changes,
    add/update unified-diff patches under ./patches/libstemmer_csharp.

.PARAMETER Version
    The Snowball release to download (default: value of SNOWBALL_VERSION env var, else 3.2.0).

.PARAMETER OutputDir
    Destination directory for the generated sources
    (default: <repo>/src/SqliteFtsSnowball/Generated).

.PARAMETER Force
    Re-download and regenerate even if the marker file already matches the version.

.EXAMPLE
    pwsh ./scripts/fetch-snowball.ps1
    pwsh ./scripts/fetch-snowball.ps1 -Version 3.2.0 -Force
#>
[CmdletBinding()]
param(
    [string]$Version = $(if ($env:SNOWBALL_VERSION) { $env:SNOWBALL_VERSION } else { '3.2.0' }),
    [string]$OutputDir,
    [switch]$Force
)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

$repoRoot = Split-Path -Parent $PSScriptRoot
if (-not $OutputDir) { $OutputDir = Join-Path $repoRoot 'src/SqliteFtsSnowball/Generated' }
$patchesDir = Join-Path $repoRoot 'patches/libstemmer_csharp'
$marker = Join-Path $OutputDir '.snowball-version'

if ((Test-Path $marker) -and -not $Force) {
    $existing = (Get-Content -Raw -LiteralPath $marker).Trim()
    if ($existing -eq $Version) {
        Write-Host "Snowball C# sources v$Version already present in '$OutputDir'. Use -Force to regenerate."
        exit 0
    }
}

$tmp = Join-Path ([System.IO.Path]::GetTempPath()) ('snowball_' + [System.Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force -Path $tmp | Out-Null
try {
    $tarball = Join-Path $tmp "libstemmer_csharp-$Version.tar.gz"
    $url = "https://snowballstem.org/dist/libstemmer_csharp-$Version.tar.gz"
    Write-Host "Downloading $url"
    Invoke-WebRequest -Uri $url -OutFile $tarball

    Write-Host "Extracting $tarball"
    tar -xzf $tarball -C $tmp
    if ($LASTEXITCODE -ne 0) { throw "tar failed to extract '$tarball'." }

    $srcRoot = Join-Path $tmp "libstemmer_csharp-$Version"
    if (-not (Test-Path $srcRoot)) { throw "Extracted source root not found: '$srcRoot'." }

    # Apply unified-diff patches (in filename order), relative to the extracted tarball root.
    if (Test-Path $patchesDir) {
        $patches = Get-ChildItem -Path $patchesDir -Filter '*.patch' -File -ErrorAction SilentlyContinue | Sort-Object Name
        foreach ($p in $patches) {
            Write-Host "Applying patch: $($p.Name)"
            git -C $srcRoot apply --verbose --whitespace=nowarn -- "$($p.FullName)"
            if ($LASTEXITCODE -ne 0) { throw "Failed to apply patch '$($p.Name)'." }
        }
    }

    $snowballSrc = Join-Path $srcRoot 'csharp/Snowball'
    if (-not (Test-Path $snowballSrc)) { throw "Expected 'csharp/Snowball' in the distribution." }

    if (Test-Path $OutputDir) { Remove-Item -Recurse -Force $OutputDir }
    $dstSnowball = Join-Path $OutputDir 'Snowball'
    New-Item -ItemType Directory -Force -Path $dstSnowball | Out-Null

    # Copy every *.cs EXCEPT AssemblyInfo.cs (its [assembly:*] attributes would collide
    # with the SDK-generated assembly info). Each file gets a '#nullable disable' /
    # '#pragma warning disable' prefix so the upstream generated code does not pollute
    # our build with warnings.
    $header = "#nullable disable" + "`n" + "#pragma warning disable" + "`n"
    $count = 0
    Get-ChildItem -Path $snowballSrc -Recurse -Filter '*.cs' | ForEach-Object {
        if ($_.Name -eq 'AssemblyInfo.cs') { return }
        $rel = $_.FullName.Substring($snowballSrc.Length).TrimStart('\', '/')
        $dst = Join-Path $dstSnowball $rel
        New-Item -ItemType Directory -Force -Path (Split-Path -Parent $dst) | Out-Null
        $content = Get-Content -Raw -LiteralPath $_.FullName
        Set-Content -LiteralPath $dst -Value ($header + $content) -Encoding utf8
        $count++
    }

    Set-Content -LiteralPath $marker -Value $Version -Encoding utf8
    Write-Host "Done: wrote $count Snowball C# source files (v$Version) to '$OutputDir'."
}
finally {
    Remove-Item -Recurse -Force $tmp -ErrorAction SilentlyContinue
}
