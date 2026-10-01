#!/usr/bin/env bash
# Rex Magnum - fast C# compile check WITHOUT Unity (no license, no editor download, ~1 min).
# Compiles Unity/Assets/RexMagnum/Scripts with the .NET SDK against Unity 2021.3 reference DLLs from NuGet,
# plus small stubs for the Input System, URP and uGUI packages (compile_check/Stubs.cs, members checked against
# the real package source). Unity 6 names that the 2021 DLLs do not have are renamed in a temporary copy
# (linearVelocity, PhysicsMaterial, NamedBuildTarget, RenderPipeline.SubmitRenderRequest, DetailPrototype.alignToGround).
# Two passes, like Unity: everything with UNITY_EDITOR (Assembly-CSharp-Editor), then Runtime only (the player build).
# It finds C# errors only. Builder exceptions, shaders and looks still need real Unity (unity_cloud_test.sh / PC test).
# Needs network access to api.nuget.org and the Ubuntu apt mirror (for dotnet-sdk-8.0 if missing).
set -euo pipefail
HERE="$(cd "$(dirname "$0")" && pwd)"
SCRIPTS="$HERE/../Unity/Assets/RexMagnum/Scripts"
W=/tmp/rexmagnum_compile
mkdir -p "$W/ref"
if ! command -v dotnet >/dev/null; then sudo_=""; [ "$(id -u)" = 0 ] || sudo_=sudo
  $sudo_ apt-get update -q >/dev/null && DEBIAN_FRONTEND=noninteractive $sudo_ apt-get install -y -q dotnet-sdk-8.0 >/dev/null; fi
get() { [ -d "$W/ref/$1" ] || { curl -sSL -o "$W/ref/$1.nupkg" "https://api.nuget.org/v3-flatcontainer/$1/$2/$1.$2.nupkg" && (cd "$W/ref" && unzip -qo "$1.nupkg" -d "$1"); }; }
get unityengine.modules 2021.3.33
get unity3d.sdk 2021.1.14.1
rm -rf "$W/src" && cp -r "$SCRIPTS" "$W/src"
find "$W/src" -name '*.cs' -print0 | xargs -0 sed -i -E \
  's/\blinearVelocity\b/velocity/g; s/\blinearDamping\b/drag/g; s/\bangularDamping\b/angularDrag/g;
   s/\bPhysicsMaterialCombine\b/PhysicMaterialCombine/g; s/\bPhysicsMaterial\b/PhysicMaterial/g;
   s/UnityEditor\.Build\.NamedBuildTarget\.(Android|Standalone|iOS)/BuildTargetGroup.\1/g;
   s/RenderPipeline\.(SupportsRenderRequest|SubmitRenderRequest)/RPX.\1/g;
   s/,?\s*alignToGround = [0-9.]+f?//g; s/,?\s*\bdensity = [0-9.]+f//g'
mkproj() { # name, compile glob, defines, editor dll
mkdir -p "$W/$1"
cat > "$W/$1/$1.csproj" <<XML
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><TargetFramework>netstandard2.1</TargetFramework><LangVersion>9.0</LangVersion><Nullable>disable</Nullable>
    <EnableDefaultCompileItems>false</EnableDefaultCompileItems><AllowUnsafeBlocks>true</AllowUnsafeBlocks>
    <GenerateAssemblyInfo>false</GenerateAssemblyInfo>
    <NoWarn>CS0618;CS0649</NoWarn><DefineConstants>$3;UNITY_6000_0_OR_NEWER;UNITY_2022_3_OR_NEWER;UNITY_2021_3_OR_NEWER;UNITY_STANDALONE;ENABLE_INPUT_SYSTEM</DefineConstants></PropertyGroup>
  <ItemGroup><Compile Include="$HERE/compile_check/*.cs" /><Compile Include="$W/src/$2" />
    <Reference Include="$W/ref/unityengine.modules/lib/netstandard2.0/*.dll" />$4</ItemGroup>
</Project>
XML
}
mkproj Editor '**/*.cs' UNITY_EDITOR "<Reference Include=\"$W/ref/unity3d.sdk/lib/UnityEditor.dll\" />"
mkproj Player 'Runtime/**/*.cs' NO_EDITOR ""
fail=0
for p in Editor Player; do
  out=$(cd "$W/$p" && dotnet build "$p.csproj" -nologo -v q 2>&1 | grep -E ' (error|warning) ' | sed "s| \[$W.*||; s|$W/src/||" | sort -u || true)
  n=$(printf "%s" "$out" | grep -c ' error ' || true)
  echo "== $p pass: $n errors"; [ -n "$out" ] && printf "%s\n" "$out"
  [ "$n" = 0 ] || fail=1
done
exit $fail
