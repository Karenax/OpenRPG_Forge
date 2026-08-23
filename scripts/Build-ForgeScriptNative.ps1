param(
    [ValidateSet('windows-x64', 'linux-x64')]
    [string]$Platform = 'windows-x64',
    [switch]$SkipDownload
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$dependencyRoot = Join-Path $repositoryRoot 'tmp\native-deps'
$angelArchive = Join-Path $dependencyRoot 'angelscript_2.38.0.zip'
$angelRoot = Join-Path $dependencyRoot 'angelscript-2.38.0\sdk'
$zigArchive = Join-Path $dependencyRoot 'zig-x86_64-windows-0.16.0.zip'
$zigRoot = Join-Path $dependencyRoot 'zig-0.16.0\zig-x86_64-windows-0.16.0'
$zig = Join-Path $zigRoot 'zig.exe'
$outputDirectoryName = if ($Platform -eq 'windows-x64') { 'win-x64' } else { 'linux-x64' }
$outputRoot = Join-Path $repositoryRoot "native\ForgeScript\lib\$outputDirectoryName"
$outputName = if ($Platform -eq 'windows-x64') {
    'forge_script_native.dll'
}
else {
    'libforge_script_native.so'
}
$target = if ($Platform -eq 'windows-x64') {
    'x86_64-windows-gnu'
}
else {
    'x86_64-linux-gnu'
}
$output = Join-Path $outputRoot $outputName

$dependencies = @(
    @{
        Path = $angelArchive
        Uri = 'https://www.angelcode.com/angelscript/sdk/files/angelscript_2.38.0.zip'
        Hash = 'B33B5DBCDA10317EF67D628353D83246984CE6FCAC102D4DC2AED121EBA52E6F'
    },
    @{
        Path = $zigArchive
        Uri = 'https://ziglang.org/download/0.16.0/zig-x86_64-windows-0.16.0.zip'
        Hash = '68659EB5F1E4EB1437A722F1DD889C5A322C9954607F5EDCF337BC3684A75A7E'
    }
)

New-Item -ItemType Directory -Force -Path $dependencyRoot,$outputRoot | Out-Null
foreach ($dependency in $dependencies) {
    if (-not (Test-Path -LiteralPath $dependency.Path)) {
        if ($SkipDownload) {
            throw "Dépendance native absente : $($dependency.Path)"
        }

        Invoke-WebRequest -Uri $dependency.Uri -OutFile $dependency.Path
    }

    $actualHash = (Get-FileHash -LiteralPath $dependency.Path -Algorithm SHA256).Hash
    if ($actualHash -ne $dependency.Hash) {
        throw "Hash invalide pour $($dependency.Path)."
    }
}

if (-not (Test-Path -LiteralPath $angelRoot)) {
    Expand-Archive -LiteralPath $angelArchive -DestinationPath (Split-Path -Parent $angelRoot)
}

if (-not (Test-Path -LiteralPath $zig)) {
    Expand-Archive -LiteralPath $zigArchive -DestinationPath (Split-Path -Parent $zigRoot)
}

$angelSource = Join-Path $angelRoot 'angelscript\source'
$angelInclude = Join-Path $angelRoot 'angelscript\include'
$stringAddon = Join-Path $angelRoot 'add_on\scriptstdstring'
$bridge = Join-Path $repositoryRoot 'native\ForgeScript\src\forge_script_native.cpp'
$sources = @(
    'as_atomic.cpp',
    'as_builder.cpp',
    'as_bytecode.cpp',
    'as_callfunc.cpp',
    'as_callfunc_mips.cpp',
    'as_callfunc_x86.cpp',
    'as_callfunc_x64_gcc.cpp',
    'as_callfunc_x64_msvc.cpp',
    'as_callfunc_x64_mingw.cpp',
    'as_compiler.cpp',
    'as_configgroup.cpp',
    'as_context.cpp',
    'as_datatype.cpp',
    'as_gc.cpp',
    'as_generic.cpp',
    'as_globalproperty.cpp',
    'as_memory.cpp',
    'as_module.cpp',
    'as_objecttype.cpp',
    'as_outputbuffer.cpp',
    'as_parser.cpp',
    'as_restore.cpp',
    'as_scriptcode.cpp',
    'as_scriptengine.cpp',
    'as_scriptfunction.cpp',
    'as_scriptnode.cpp',
    'as_scriptobject.cpp',
    'as_string.cpp',
    'as_string_util.cpp',
    'as_thread.cpp',
    'as_tokenizer.cpp',
    'as_typeinfo.cpp',
    'as_variablescope.cpp'
) | ForEach-Object { Join-Path $angelSource $_ }
$sources += Join-Path $stringAddon 'scriptstdstring.cpp'
$sources += $bridge

$arguments = @(
    'c++',
    '-target', $target,
    '-std=c++17',
    '-O2',
    '-shared',
    '-Wno-nullability-completeness',
    '-DANGELSCRIPT_EXPORT',
    '-D_LIB',
    '-D_CRT_SECURE_NO_WARNINGS',
    "-I$angelInclude",
    "-I$angelSource",
    "-I$stringAddon"
)
if ($Platform -eq 'linux-x64') {
    $arguments += '-fPIC'
}
$arguments += @('-o', $output)
$arguments += $sources

& $zig @arguments
if ($LASTEXITCODE -ne 0) {
    throw "La compilation native ForgeScript a échoué avec le code $LASTEXITCODE."
}

$runtimeNativeRoot = Join-Path $repositoryRoot 'apps\Forge.Runtime\native'
New-Item -ItemType Directory -Force -Path $runtimeNativeRoot | Out-Null
Copy-Item -LiteralPath $output -Destination (Join-Path $runtimeNativeRoot $outputName) -Force

Write-Output "OPENRPG_FORGE_FORGESCRIPT_NATIVE_BUILD_PASS platform=$Platform vm=2.38.0 output=$output"
