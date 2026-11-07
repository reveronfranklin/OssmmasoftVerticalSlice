#!/usr/bin/env bash
set -euo pipefail
project_root="$(cd "$(dirname "$0")/.." && pwd)"
test_dir="$(mktemp -d "${TMPDIR:-/tmp}/r53-receipts.XXXXXX")"
trap 'rm -rf "$test_dir"' EXIT
cat > "$test_dir/ReceiptChecks.csproj" <<PROJECT
<Project Sdk="Microsoft.NET.Sdk">
<PropertyGroup><TargetFramework>net10.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable><IsPackable>false</IsPackable><NuGetAudit>false</NuGetAudit></PropertyGroup>
<ItemGroup><PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.14.1" /><PackageReference Include="xunit" Version="2.9.3" /><PackageReference Include="xunit.runner.visualstudio" Version="3.1.4" /><Using Include="Xunit" /></ItemGroup>
<ItemGroup><ProjectReference Include="$project_root/OssmmasoftVerticalSlice.csproj" /></ItemGroup>
</Project>
PROJECT
cp "$project_root/scripts/tests/recibo-email/ReciboEmailTests.cs.txt" "$test_dir/ReciboEmailTests.cs"
dotnet test "$test_dir/ReceiptChecks.csproj" -p:NuGetAudit=false --verbosity quiet
