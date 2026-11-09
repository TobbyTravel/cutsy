$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$provenance = Get-Content -LiteralPath (Join-Path $repoRoot 'assets/source/provenance.json') -Raw | ConvertFrom-Json
$files = @($provenance.files)
if ($files.Count -ne 102) { throw "Expected 102 PNG records, found $($files.Count)" }
if (@($files | Where-Object role -eq 'sprites').Count -ne 80) { throw 'Expected 80 sprite mappings' }
$seen = @{}
foreach ($record in $files) {
  if ([IO.Path]::IsPathRooted($record.path) -or $record.path -match '(^|/)\.\.(/|$)') { throw "Invalid path: $($record.path)" }
  if ($seen.ContainsKey($record.path)) { throw "Duplicate: $($record.path)" }
  $seen[$record.path] = $true
  $path = Join-Path $repoRoot $record.path
  if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Missing: $($record.path)" }
  if ((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant() -ne $record.sha256) { throw "Hash mismatch: $($record.path)" }
  foreach ($field in @('sourceRawPath','sourceProcessedPath')) {
    if ($record.$field) {
      if ([IO.Path]::IsPathRooted($record.$field) -or -not (Test-Path -LiteralPath (Join-Path $repoRoot $record.$field))) { throw "Unresolved source: $($record.$field)" }
    }
  }
  if ($record.role -eq 'sprites' -and ($record.row -notin 1,2 -or $record.column -notin 1,2,3,4)) { throw "Invalid grid mapping: $($record.path)" }
}
$pngs = @(Get-ChildItem -LiteralPath (Join-Path $repoRoot 'assets/source'), (Join-Path $repoRoot 'packs/cutsy/sprites') -Filter '*.png' -Recurse -File)
if ($pngs.Count -ne 102) { throw "Unexpected PNG inventory: $($pngs.Count)" }
$gridKeys = @($files | Where-Object role -eq sprites | ForEach-Object { "$($_.sourceSheet):$($_.row):$($_.column)" } | Sort-Object -Unique)
if ($gridKeys.Count -ne 80) { throw 'Missing/duplicate source grid mappings' }
$tracked = @(& git -C $repoRoot ls-files)
foreach ($record in $files) { if ($record.path -notin $tracked) { throw "PNG not tracked: $($record.path)" } }
foreach ($doc in Get-ChildItem -LiteralPath $repoRoot -Filter '*.md' -Recurse -File) {
  if ($doc.FullName -match '[\\/](\.git|bin|obj|artifacts|dist|work)[\\/]') { continue }
  $content = Get-Content -LiteralPath $doc.FullName -Raw
  foreach ($match in [regex]::Matches($content, '\[[^\]]*\]\(([^)]+)\)')) {
    $link = $match.Groups[1].Value.Split('#')[0]
    if (-not $link -or $link -match '^[a-z]+:' -or $link.StartsWith('#')) { continue }
    if (-not (Test-Path -LiteralPath (Join-Path $doc.DirectoryName $link))) { throw "Broken link in $($doc.Name): $link" }
  }
}
$index = @(Get-Content -LiteralPath (Join-Path $repoRoot 'docs/issue-index.json') -Raw | ConvertFrom-Json)
if ($index.Count -ne 12) { throw 'Expected 12 issue mappings' }
$roadmap = Get-Content -LiteralPath (Join-Path $repoRoot 'docs/ROADMAP.md') -Raw
foreach ($issue in $index) {
  if (-not $roadmap.Contains($issue.url)) { throw "Roadmap missing $($issue.id)" }
  if (-not (Test-Path -LiteralPath (Join-Path $repoRoot "docs/issues/$($issue.id).md"))) { throw "Missing issue specification $($issue.id)" }
}
Write-Output 'PASS: 102 PNG SHA-256 matches; 80 unique sprite mappings; all assets tracked; local Markdown links resolve; 12 issues mapped.'
Write-Output 'This check covers the immutable import. Runtime packs and desktop behavior have separate verification.'
