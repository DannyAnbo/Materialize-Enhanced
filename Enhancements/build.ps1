param([switch]$Deploy,[string]$InstallDir='E:\Materialize_1.78')
$ErrorActionPreference='Stop'
$root=$PSScriptRoot
$InstallDir=[IO.Path]::GetFullPath($InstallDir)
$managed=Join-Path $InstallDir 'Materialize_Data\Managed'
New-Item -ItemType Directory -Path (Join-Path $root 'artifacts') -Force | Out-Null
$csc='C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$refs=@('mscorlib.dll','System.dll','System.Core.dll','System.Xml.dll','UnityEngine.dll','UnityEngine.CoreModule.dll','UnityEngine.IMGUIModule.dll','UnityEngine.ImageConversionModule.dll','UnityEngine.InputModule.dll','UnityEngine.PhysicsModule.dll','UnityEngine.TextRenderingModule.dll') | ForEach-Object { '/r:'+(Join-Path $managed $_) }
dotnet build (Join-Path $root 'EnhanceBuild.csproj') -c Release --nologo -v quiet
if($LASTEXITCODE -ne 0){throw 'Builder compilation failed'}
$builder=Join-Path $root 'bin\Release\net9.0\EnhanceBuild.dll'
$original=Join-Path $InstallDir 'languages\en\Assembly-CSharp.dll'
if(!(Test-Path -LiteralPath $original)){$original=Join-Path $managed 'Assembly-CSharp.dll'}
if((Get-FileHash -LiteralPath $original -Algorithm SHA256).Hash -ne 'AAB0F80D825B4C2809131F34CA8D75C0BACE623416CE8200BE73E9F36F96DA36'){throw 'Expected original Materialize 1.78 assembly; use an untouched installation or languages\en backup'}
dotnet $builder --data $original (Join-Path $root 'FactoryDefaults.cs')
if($LASTEXITCODE -ne 0){throw 'Default value extraction failed'}
$translationMap=@{}
Get-Content -Raw (Join-Path $root 'strings_csharp_zh.json') | ConvertFrom-Json | ForEach-Object {if($_.zh -and $_.s -ne $_.zh){$translationMap[$_.s]=$_.zh}}
$overrides=Get-Content -Raw (Join-Path $root 'overrides.json') | ConvertFrom-Json
$overrides.PSObject.Properties | ForEach-Object {$translationMap[$_.Name]=$_.Value}
$translationLines=$translationMap.GetEnumerator() | ForEach-Object {[Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($_.Key))+"`t"+[Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($_.Value))}
[IO.File]::WriteAllLines((Join-Path $root 'translations.tsv'),[string[]]$translationLines,[Text.UTF8Encoding]::new($false))
$sources=@('MaterializeEnhancements.cs','EditHistory.cs','RuntimeUI.cs','RecentProjects.cs','ProjectSession.cs','NewProject.cs','SurfaceWorkflow.cs','AboutUI.cs','TextureResolution.cs','ParameterControls.cs','WindowsDrop.cs','TextureFiles.cs','TextureChannels.cs','TextureNames.cs','FactoryDefaults.cs','EnhanceSelfTest.cs','ChannelSelfTest.cs','RecentSelfTest.cs','SessionSelfTest.cs','ResolutionSelfTest.cs','WorkflowSelfTest.cs','UnicodeSelfTest.cs') | ForEach-Object {Join-Path $root (''+$_)}
& $csc /nologo /target:library /nostdlib /noconfig /optimize+ /langversion:5 @refs ('/resource:'+(Join-Path $root 'translations.tsv')+',translations') ('/resource:'+(Join-Path $root 'author-avatar.png')+',author-avatar') ('/out:'+(Join-Path $root 'artifacts\MaterializeEnhancements.dll')) @sources
if($LASTEXITCODE -ne 0){throw 'Helper compilation failed'}
dotnet $builder $original (Join-Path $root 'artifacts\MaterializeEnhancements.dll') (Join-Path $root 'artifacts\Assembly-CSharp.zh.dll') (Join-Path $root 'strings_csharp_zh.json')
if($LASTEXITCODE -ne 0){throw 'Chinese build failed'}
dotnet $builder $original (Join-Path $root 'artifacts\MaterializeEnhancements.dll') (Join-Path $root 'artifacts\Assembly-CSharp.en.dll') (Join-Path $root 'strings_csharp_zh.json')
if($LASTEXITCODE -ne 0){throw 'English build failed'}
if($Deploy){
    if(Get-Process Materialize -ErrorAction SilentlyContinue){throw 'Close Materialize before deploying'}
    if(Get-Process MaterializeLangSwitch -ErrorAction SilentlyContinue){throw 'Close language switcher before deploying'}
    $baselineDir=Join-Path $InstallDir 'languages\en'
    if(!(Test-Path -LiteralPath (Join-Path $baselineDir 'Assembly-CSharp.dll'))){
        New-Item -ItemType Directory -Path $baselineDir -Force | Out-Null
        Copy-Item -LiteralPath $original -Destination (Join-Path $baselineDir 'Assembly-CSharp.dll')
        Copy-Item -LiteralPath (Join-Path $managed 'Assembly-UnityScript-firstpass.dll') -Destination $baselineDir
    }
    Copy-Item -LiteralPath (Join-Path $root 'artifacts\MaterializeEnhancements.dll') -Destination $managed -Force
    Copy-Item -LiteralPath (Join-Path $root 'artifacts\Assembly-CSharp.zh.dll') -Destination (Join-Path $managed 'Assembly-CSharp.dll') -Force
    foreach($language in @('zh','en-enhanced')){
        $languageDir=Join-Path $InstallDir ('languages\'+$language)
        New-Item -ItemType Directory -Path $languageDir -Force | Out-Null
        $artifact=if($language -eq 'zh'){'Assembly-CSharp.zh.dll'}else{'Assembly-CSharp.en.dll'}
        Copy-Item -LiteralPath (Join-Path $root ('artifacts\'+$artifact)) -Destination (Join-Path $languageDir 'Assembly-CSharp.dll') -Force
        Copy-Item -LiteralPath (Join-Path $root 'artifacts\MaterializeEnhancements.dll') -Destination $languageDir -Force
    }
    Copy-Item -LiteralPath (Join-Path $managed 'Assembly-UnityScript-firstpass.dll') -Destination (Join-Path $InstallDir 'languages\en-enhanced\Assembly-UnityScript-firstpass.dll') -Force
}
