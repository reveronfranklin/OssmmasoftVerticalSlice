#!/usr/bin/env bash
set -euo pipefail
project_root="$(cd "$(dirname "$0")/.." && pwd)"
check_dir="$(mktemp -d "${TMPDIR:-/tmp}/r54-checks.XXXXXX")"
trap 'rm -rf "$check_dir"' EXIT
cat > "$check_dir/Checks.csproj" <<PROJECT
<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable><NuGetAudit>false</NuGetAudit></PropertyGroup><ItemGroup><FrameworkReference Include="Microsoft.AspNetCore.App" /><Reference Include="Oracle.ManagedDataAccess"><HintPath>$project_root/bin/Debug/net10.0/Oracle.ManagedDataAccess.dll</HintPath></Reference><Compile Include="$project_root/Features/DatabaseDeployment/DeploymentModels.cs" Link="Models.cs" /><Compile Include="$project_root/Features/DatabaseDeployment/DeploymentPlanBuilder.cs" Link="Plan.cs" /><Compile Include="$project_root/Features/DatabaseDeployment/DeploymentStore.cs" Link="Store.cs" /><Compile Include="$project_root/Features/DatabaseDeployment/DeploymentMetadata.cs" Link="Metadata.cs" /></ItemGroup></Project>
PROJECT
cp "$project_root/scripts/tests/database-deployment/Program.cs.txt" "$check_dir/Program.cs"
dotnet run --project "$check_dir/Checks.csproj" -p:NuGetAudit=false --verbosity quiet
