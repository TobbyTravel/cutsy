$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $true
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$dist = Join-Path $root 'dist'
$staging = Join-Path $dist ('.publish-' + [Guid]::NewGuid().ToString('N'))
$publish = Join-Path $staging 'publish'
$portable = Join-Path $staging 'portable'

Push-Location $root
try {
    New-Item -ItemType Directory -Path $publish, "$portable/pack", "$portable/schemas" -Force | Out-Null
    dotnet publish src/Cutsy.Windows/Cutsy.Windows.csproj -c Release -r win-x64 --self-contained true `
        -p:CutsyPublish=true -p:RestoreLockedMode=true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:EnableCompressionInSingleFile=true -p:PublishTrimmed=false -p:DebugType=none -p:DebugSymbols=false `
        -o $publish

    Copy-Item -LiteralPath "$publish/Cutsy.exe" -Destination "$portable/Cutsy.exe"
    New-Item -ItemType File -Path "$portable/portable.flag" | Out-Null
    $pack = Join-Path $root 'packs/cutsy/runtime'
    foreach ($item in @('pack.json', 'behaviors.json', 'art', 'masks', 'sounds')) {
        Copy-Item -LiteralPath (Join-Path $pack $item) -Destination "$portable/pack" -Recurse
    }
    Get-ChildItem -LiteralPath 'packs/schema' -Filter '*.json' -File |
        Copy-Item -Destination "$portable/schemas"
    Copy-Item -LiteralPath 'docs/PORTABLE.md' -Destination "$portable/README.md"
    Copy-Item -LiteralPath 'docs/PACK_EDITING.md' -Destination "$portable/PACK_EDITING.md"

    $zip = Join-Path $staging 'Cutsy-0.1.0-win-x64-portable.zip'
    Compress-Archive -Path "$portable/*" -DestinationPath $zip -CompressionLevel Optimal
    Copy-Item -LiteralPath "$publish/Cutsy.exe" -Destination "$dist/Cutsy.exe" -Force
    Copy-Item -LiteralPath $zip -Destination $dist -Force

    $files = 'Cutsy.exe', (Split-Path $zip -Leaf)
    $checksums = foreach ($file in $files) {
        $hash = (Get-FileHash -LiteralPath (Join-Path $dist $file) -Algorithm SHA256).Hash.ToLowerInvariant()
        "$hash  $file"
    }
    $checksums | Set-Content -LiteralPath "$dist/SHA256SUMS.txt" -Encoding ascii
    Get-ChildItem -LiteralPath $dist -File | Select-Object Name, Length
} finally {
    Pop-Location
    if (Test-Path -LiteralPath $staging) {
        $resolved = (Resolve-Path -LiteralPath $staging).Path
        if ([IO.Path]::GetDirectoryName($resolved) -ne (Resolve-Path -LiteralPath $dist).Path -or
            [IO.Path]::GetFileName($resolved) -notmatch '^\.publish-[a-f0-9]{32}$') {
            throw "Unexpected publish staging path: $resolved"
        }
        Remove-Item -LiteralPath $resolved -Recurse -Force
    }
}
