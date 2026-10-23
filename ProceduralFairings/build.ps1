param(
 [Parameter(Mandatory=$true)][string]$ManagedDirectory,
 [Parameter(Mandatory=$true)][string]$ROUtilsDll,
 [Parameter(Mandatory=$true)][string]$Compiler,
 [string]$OutputDirectory=(Join-Path $PSScriptRoot 'Plugins')
)
$ErrorActionPreference='Stop'
$pfManaged=(Resolve-Path -LiteralPath $ManagedDirectory).Path
$pfRo=(Resolve-Path -LiteralPath $ROUtilsDll).Path
$pfCompiler=(Resolve-Path -LiteralPath $Compiler).Path
$pfSource=(Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '../KIU/CSScripts/ProceduralFairings')).Path
$pfOutput=[IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Force -Path $pfOutput|Out-Null
# Use KSP's Mono BCL: upstream ColliderPool needs Queue.TryDequeue.
$pfRefs=@('mscorlib.dll','System.dll','System.Core.dll','Assembly-CSharp.dll')|ForEach-Object{'/reference:'+(Join-Path $pfManaged $_)}
$pfRefs+='/reference:'+$pfRo
$pfRefs+=Get-ChildItem -LiteralPath $pfManaged -Filter 'UnityEngine*.dll'|ForEach-Object{'/reference:'+$_.FullName}
$pfFiles=@(Get-ChildItem -LiteralPath $pfSource -Filter '*.cs' -Recurse|ForEach-Object{$_.FullName})
& $pfCompiler /nologo /codepage:65001 /target:library /langversion:8 /nostdlib+ /optimize+ /debug- /platform:anycpu @pfRefs ('/out:'+(Join-Path $pfOutput 'ProceduralFairings.dll')) @pfFiles
if($LASTEXITCODE -ne 0){throw 'ProceduralFairings compile failed'}
Write-Output 'PF built. No KSP deployment was performed.'
