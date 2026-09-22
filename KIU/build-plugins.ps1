param(
 [string]$ManagedDirectory = 'D:\KSP_Clean\KSP_x64_Data\Managed',
 [string]$OutputDirectory = (Join-Path $PSScriptRoot 'Plugins'),
 [string]$Roslyn = 'C:\Program Files\dotnet\sdk\8.0.404\Roslyn\bincore\csc.dll'
)
$ErrorActionPreference = 'Stop'
$manifest = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'plugin-build-manifest.json') -Raw | ConvertFrom-Json
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$framework = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319'
$refs = @('mscorlib.dll','System.dll','System.Core.dll') | ForEach-Object { '/reference:' + (Join-Path $framework $_) }
$refs += '/reference:' + (Join-Path $ManagedDirectory 'Assembly-CSharp.dll')
$refs += Get-ChildItem -LiteralPath $ManagedDirectory -Filter 'UnityEngine*.dll' | ForEach-Object { '/reference:' + $_.FullName }
foreach ($assembly in $manifest.PSObject.Properties) {
 $sources = @($assembly.Value | ForEach-Object { Join-Path $PSScriptRoot $_ })
 $target = Join-Path $OutputDirectory ($assembly.Name + '.dll')
 & dotnet $Roslyn /nologo /codepage:65001 /target:library /langversion:8 /nostdlib+ @refs ('/out:' + $target) @sources
 if ($LASTEXITCODE -ne 0) { throw ('Compile failed: ' + $assembly.Name) }
}
