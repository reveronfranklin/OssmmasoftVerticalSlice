#!/usr/bin/env bash
set -euo pipefail
project_root="$(cd "$(dirname "$0")/.." && pwd)"
test_dir="$(mktemp -d "${TMPDIR:-/tmp}/r52-email.XXXXXX")"
trap 'rm -rf "$test_dir"' EXIT
cat > "$test_dir/EmailChecks.csproj" <<PROJECT
<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable><NuGetAudit>false</NuGetAudit></PropertyGroup><ItemGroup><ProjectReference Include="$project_root/OssmmasoftVerticalSlice.csproj" /></ItemGroup></Project>
PROJECT
cp "$project_root/scripts/tests/email-manual/Program.cs.txt" "$test_dir/Program.cs"
dotnet run --project "$test_dir/EmailChecks.csproj" -p:NuGetAudit=false --verbosity quiet
