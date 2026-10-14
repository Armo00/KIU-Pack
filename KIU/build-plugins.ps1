param(
 [string]$ManagedDirectory='D:/KSP/KSP_Clean/KSP_x64_Data/Managed',
 [string[]]$AssemblyNames=@(),
 [switch]$UseFrameworkCompiler,
 [string]$OutputDirectory=(Join-Path $PSScriptRoot 'Plugins'),
 [string]$Roslyn='C:/Program Files/dotnet/sdk/8.0.404/Roslyn/bincore/csc.dll'
)
$ErrorActionPreference='Stop'
$kiuManifest=Get-Content -LiteralPath (Join-Path $PSScriptRoot 'plugin-build-manifest.json') -Raw|ConvertFrom-Json
$kiuManaged=(Resolve-Path -LiteralPath $ManagedDirectory).Path
$kiuOutput=[IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $kiuOutput -Force|Out-Null
$kiuFramework='C:/Windows/Microsoft.NET/Framework64/v4.0.30319'
$kiuRefs=@('mscorlib.dll','System.dll','System.Core.dll')|ForEach-Object {'/reference:'+(Join-Path $kiuFramework $_)}
$kiuRefs+='/reference:'+(Join-Path $kiuManaged 'Assembly-CSharp.dll')
$kiuRefs+=Get-ChildItem -LiteralPath $kiuManaged -Filter 'UnityEngine*.dll'|ForEach-Object {'/reference:'+$_.FullName}
$kiuState=@{}
function Build-KiuAssembly([string]$kiuName){
 if($kiuState[$kiuName] -eq 2){return}
 if($kiuState[$kiuName] -eq 1){throw "Cyclic assembly dependency: $kiuName"}
 $kiuProperty=$kiuManifest.PSObject.Properties[$kiuName]
 if($null -eq $kiuProperty){throw "Unknown assembly dependency: $kiuName"}
 $kiuState[$kiuName]=1
 $kiuEntry=$kiuProperty.Value
 $kiuSourceProperty=$kiuEntry.PSObject.Properties['sources']
 if($null -ne $kiuSourceProperty){$kiuSources=@($kiuEntry.sources);$kiuDependencies=@($kiuEntry.references)}
 else{$kiuSources=@($kiuEntry);$kiuDependencies=@()}
 $kiuAssemblyRefs=@($kiuRefs)
 foreach($kiuDependency in $kiuDependencies){
  Build-KiuAssembly $kiuDependency
  $kiuAssemblyRefs+='/reference:'+(Join-Path $kiuOutput ($kiuDependency+'.dll'))
 }
 $kiuSourcePaths=@($kiuSources|ForEach-Object {(Resolve-Path -LiteralPath (Join-Path $PSScriptRoot $_)).Path})
 $kiuTarget=Join-Path $kiuOutput ($kiuName+'.dll')
 if($UseFrameworkCompiler){
  & (Join-Path $kiuFramework 'csc.exe') /nologo /codepage:65001 /target:library /langversion:5 /nostdlib+ /optimize+ /debug- @kiuAssemblyRefs ('/out:'+$kiuTarget) @kiuSourcePaths
 }else{
  & dotnet $Roslyn /nologo /codepage:65001 /target:library /langversion:8 /nostdlib+ /optimize+ /debug- @kiuAssemblyRefs ('/out:'+$kiuTarget) @kiuSourcePaths
 }
 if($LASTEXITCODE -ne 0){throw "Compile failed: $kiuName"}
 $kiuState[$kiuName]=2
 Write-Output ('Built '+$kiuName+'; references='+($kiuDependencies -join ','))
}
if($AssemblyNames.Count -eq 0){$AssemblyNames=@($kiuManifest.PSObject.Properties.Name)}
foreach($kiuAssembly in $AssemblyNames){Build-KiuAssembly $kiuAssembly}
