#!/usr/bin/env bash
set -euo pipefail
project_root="$(cd "$(dirname "$0")/.." && pwd)"
check_dir="$(mktemp -d "${TMPDIR:-/tmp}/support-events.XXXXXX")"
trap 'rm -rf "$check_dir"' EXIT
cat > "$check_dir/Checks.csproj" <<PROJECT
<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable><NuGetAudit>false</NuGetAudit></PropertyGroup><ItemGroup><Compile Include="$project_root/Features/Support/Comments/SupportEventRules.cs" Link="SupportEventRules.cs" /></ItemGroup></Project>
PROJECT
cp "$project_root/scripts/tests/support-events/Program.cs.txt" "$check_dir/Program.cs"
dotnet run --project "$check_dir/Checks.csproj" -p:NuGetAudit=false --verbosity quiet
