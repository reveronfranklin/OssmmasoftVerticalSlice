#!/usr/bin/env bash
set -euo pipefail
project_root="$(cd "$(dirname "$0")/.." && pwd)"
test_dir="$(mktemp -d "${TMPDIR:-/tmp}/r55-checks.XXXXXX")"
trap 'rm -rf "$test_dir"' EXIT
cat > "$test_dir/Checks.csproj" <<PROJECT
<Project Sdk="Microsoft.NET.Sdk">
<PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable><NuGetAudit>false</NuGetAudit></PropertyGroup>
<ItemGroup><Compile Include="$project_root/Features/RhTitularesBeneficiarios/RhTitularesBeneficiariosShared.cs" Link="Shared.cs" /></ItemGroup>
</Project>
PROJECT
cp "$project_root/scripts/tests/titular-beneficiarios/Checks.cs.txt" "$test_dir/Program.cs"
dotnet run --project "$test_dir/Checks.csproj" --verbosity quiet
