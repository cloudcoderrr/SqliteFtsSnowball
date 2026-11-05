#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Generates ground-truth stemming test data under Tests/Data/stemming from the official
    Snowball test vectors (github.com/snowballstem/snowball-data), pinned to a commit.

.DESCRIPTION
    For every supported language the script downloads the authoritative `voc.txt` (input
    words) and `output.txt` (expected stems) vectors, subsamples the first -MaxPerLanguage
    aligned pairs, and writes a UTF-8 `<language>.tsv` file (word <TAB> expected-stem).
    A `manifest.json` records the pinned commit and per-language counts.

    The generated files are committed to the repository so the tests are reproducible and
    reviewable. Re-run this script when bumping the Snowball version (update -Sha to the
    matching snowball-data commit).

.NOTES
    Validated: for snowball-data commit 019921c3... every file matches the stemmers bundled
    in libstemmer_csharp 3.2.0 exactly.
#>
[CmdletBinding()]
param(
    [string]$Sha = '019921c3a41c2b70063ee3feb215a00c8e594597',
    [int]$MaxPerLanguage = 1000,
    [string]$OutputDir
)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

$repoRoot = Split-Path -Parent $PSScriptRoot
if (-not $OutputDir) { $OutputDir = Join-Path $repoRoot 'Tests/Data/stemming' }
$baseUrl = "https://raw.githubusercontent.com/snowballstem/snowball-data/$Sha"

$languages = @(
    'arabic', 'armenian', 'basque', 'catalan', 'czech', 'danish', 'dutch', 'dutch_porter',
    'earlymodernenglish', 'english', 'esperanto', 'estonian', 'finnish', 'french', 'german',
    'greek', 'hindi', 'hungarian', 'indonesian', 'irish', 'italian', 'lithuanian', 'nepali',
    'norwegian', 'persian', 'polish', 'porter', 'portuguese', 'romanian', 'russian', 'serbian',
    'sesotho', 'spanish', 'swedish', 'tamil', 'turkish', 'ukrainian', 'yiddish'
)

function Get-SnowballFile {
    param([string]$Language, [string]$File)
    foreach ($candidate in @("$baseUrl/$Language/$File", "$baseUrl/$Language/$File.gz")) {
        try {
            $resp = Invoke-WebRequest -Uri $candidate -ErrorAction Stop
        }
        catch {
            if ($_.Exception.Response -and [int]$_.Exception.Response.StatusCode -eq 404) { continue }
            throw
        }
        $bytes = $resp.Content
        if ($bytes -is [string]) { $bytes = [System.Text.Encoding]::UTF8.GetBytes($bytes) }
        if ($candidate.EndsWith('.gz')) {
            $inMs = [System.IO.MemoryStream]::new($bytes)
            $gz = [System.IO.Compression.GZipStream]::new($inMs, [System.IO.Compression.CompressionMode]::Decompress)
            $outMs = [System.IO.MemoryStream]::new()
            $gz.CopyTo($outMs)
            $gz.Dispose(); $inMs.Dispose()
            $bytes = $outMs.ToArray(); $outMs.Dispose()
        }
        return [System.Text.Encoding]::UTF8.GetString($bytes)
    }
    return $null
}

New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null
$manifest = [ordered]@{ snowballDataSha = $Sha; snowballVersion = '3.2.0'; maxPerLanguage = $MaxPerLanguage; languages = @() }

foreach ($lang in $languages) {
    $voc = Get-SnowballFile -Language $lang -File 'voc.txt'
    $out = Get-SnowballFile -Language $lang -File 'output.txt'
    if (-not $voc -or -not $out) { Write-Warning "No vectors for '$lang' - skipped."; continue }

    $vLines = $voc -replace "`r", '' -split "`n"
    $oLines = $out -replace "`r", '' -split "`n"
    $limit = [Math]::Min([Math]::Min($vLines.Length, $oLines.Length), $MaxPerLanguage)

    $sb = [System.Text.StringBuilder]::new()
    $written = 0
    for ($i = 0; $i -lt $limit; $i++) {
        $w = $vLines[$i]; $s = $oLines[$i]
        if ([string]::IsNullOrWhiteSpace($w) -or [string]::IsNullOrWhiteSpace($s)) { continue }
        [void]$sb.Append($w).Append("`t").Append($s).Append("`n")
        $written++
    }

    $dst = Join-Path $OutputDir "$lang.tsv"
    [System.IO.File]::WriteAllText($dst, $sb.ToString(), [System.Text.UTF8Encoding]::new($false))
    $manifest.languages += [ordered]@{ name = $lang; file = "stemming/$lang.tsv"; count = $written }
    Write-Host ("{0,-20} {1} pairs" -f $lang, $written)
}

$manifestPath = Join-Path (Split-Path -Parent $OutputDir) 'manifest.json'
$manifest | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $manifestPath -Encoding utf8
Write-Host "Wrote manifest: $manifestPath"
