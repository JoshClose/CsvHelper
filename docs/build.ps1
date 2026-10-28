#!/usr/bin/env pwsh

<#
.SYNOPSIS
	Builds the documentation site into docs/_site.

.DESCRIPTION
	1. Installs the front-end tools (npm ci) when package-lock.json has changed.
	2. Runs the generator (tools/CsvHelper.Docs): it builds the library for every target framework, reads its API and
	   XML docs, the README and docs/pages, and the git tags, and writes the pages and the search index.
	3. Builds the CSS with Tailwind, which scans the generated pages.

	Open docs/_site/index.html directly, or serve the folder (for example: npx serve docs/_site).

.PARAMETER Preview
	Show the working tree as release <version> (1.2.0 or v1.2.0), released today, with its notes from
	docs/releases/<version>/: the changelog and migration guide as they will be once it is tagged. Nothing is tagged.

.PARAMETER NoTagBuilds
	Don't build release tags whose API snapshot is not cached yet (faster; their API diffs are then missing).

.PARAMETER Strict
	Fail on any warning: broken links, unresolved crefs, undocumented public members, unmapped README headings.

.PARAMETER BaseUrl
	The absolute URL the site is published at (default: baseUrl in docs/site.json).

.PARAMETER CheckSnippets
	Compile the C# code of the migration guides (docs/releases/*/migration.md) against the CsvHelper versions they show,
	instead of building the site. Restores the old packages from NuGet the first time.

.PARAMETER Only
	With -CheckSnippets: only these guides (v34.0.0, v20.0.0).

.PARAMETER Help
	Show this list of options.

.EXAMPLE
	docs/build.ps1 -Preview 34.0.0

	Shows what the site will look like once v34.0.0 is tagged.

.EXAMPLE
	docs/build.ps1 -NoTagBuilds

	A quick build while working on the pages.

.EXAMPLE
	docs/build.ps1 -CheckSnippets -Only v34.0.0

	Compiles the before and after code of the v34.0.0 migration guide.
#>
[CmdletBinding(PositionalBinding = $false)]
param(
	[string] $Preview,
	[switch] $NoTagBuilds,
	[switch] $Strict,
	[string] $BaseUrl,
	[switch] $CheckSnippets,
	[string[]] $Only,
	[Alias('h')] [switch] $Help,
	# --help, which PowerShell does not take as a parameter name, and anything unknown.
	[Parameter(ValueFromRemainingArguments, DontShow)] [string[]] $Remaining
)

$ErrorActionPreference = 'Stop'
$docs = $PSScriptRoot
$repo = Split-Path $docs -Parent

if ($Help -or $Remaining -contains '--help') {
	# The options and examples of the help comment above, so that they are written once.
	$info = Get-Help $PSCommandPath
	$values = @{ Preview = 'version'; BaseUrl = 'url'; Only = 'versions' }
	Write-Host $info.Synopsis
	Write-Host ''
	Write-Host 'Usage: docs/build.ps1 [options]'
	Write-Host ''
	Write-Host 'Options:'
	foreach ($parameter in $info.parameters.parameter | Where-Object name -ne 'Remaining') {
		$name = $parameter.name -eq 'Help' ? '-h, --help' : "-$($parameter.name)"
		$value = $parameter.type.name -eq 'SwitchParameter' ? '' : " <$($values[$parameter.name] ?? 'value')>"
		Write-Host "  $name$value"
		foreach ($line in ($parameter.description.Text -join "`n") -split "`n") { Write-Host "      $line" }
	}

	Write-Host ''
	Write-Host 'Examples:'
	foreach ($example in $info.examples.example) {
		Write-Host "  $($example.code.Trim())"
		foreach ($line in ($example.remarks.Text -join "`n").Trim() -split "`n" | Where-Object { $_ }) { Write-Host "      $line" }
	}

	return
}

if ($Remaining) {
	throw "Unknown argument: $($Remaining -join ' '). docs/build.ps1 -h lists the options."
}

if ($CheckSnippets) {
	$arguments = @('run', '--project', (Join-Path $repo 'tools/CsvHelper.Docs'), '-c', 'Release', '--', '--repo', $repo, '--check-snippets')
	foreach ($guide in $Only) { $arguments += @('--only', $guide) }
	Invoke-Step 'Compiling the code of the migration guides' { dotnet @arguments }

	return
}

function Invoke-Step([string] $name, [scriptblock] $command) {
	Write-Host "==> $name" -ForegroundColor Green
	& $command
	if ($LASTEXITCODE -ne 0) { throw "$name failed (exit code $LASTEXITCODE)." }
}

# npm ci only when the lockfile changed since the last install.
$lock = Join-Path $docs 'package-lock.json'
$stamp = Join-Path $docs 'node_modules/.package-lock.hash'
$hash = (Get-FileHash $lock -Algorithm SHA256).Hash
if (-not (Test-Path $stamp) -or (Get-Content $stamp -Raw).Trim() -ne $hash) {
	Invoke-Step 'Installing front-end tools' { npm ci --prefix $docs --no-audit --no-fund }
	Set-Content -Path $stamp -Value $hash
}

$arguments = @('run', '--project', (Join-Path $repo 'tools/CsvHelper.Docs'), '-c', 'Release', '--', '--repo', $repo)
if ($NoTagBuilds) { $arguments += '--no-tag-builds' }
if ($Strict) { $arguments += '--strict' }
if ($BaseUrl) { $arguments += @('--base-url', $BaseUrl) }
if ($Preview) { $arguments += @('--preview', $Preview) }
Invoke-Step 'Generating the pages' { dotnet @arguments }

$css = Join-Path $docs 'assets/site.css'
$out = Join-Path $docs '_site/assets/site.css'
Invoke-Step 'Building the CSS' { npm exec --prefix $docs --no -- tailwindcss -i $css -o $out --minify }

Write-Host "Done: $(Join-Path $docs '_site/index.html')" -ForegroundColor Green
