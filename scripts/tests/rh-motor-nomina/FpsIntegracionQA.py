import json,subprocess,shlex
from pathlib import Path
base=Path(__file__).resolve().parents[3]
payload=json.dumps({'program':(base/'scripts/tests/rh-motor-nomina/FpsIntegracionQA.cs.txt').read_text(),'sql':''})
remote='''import tempfile,pathlib,subprocess,json,shutil,sys,os
p=pathlib.Path(tempfile.mkdtemp(prefix="r57-fuentes-"))
try:
 data=json.load(sys.stdin)
 (p/'Probe.csproj').write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable></PropertyGroup><ItemGroup><FrameworkReference Include="Microsoft.AspNetCore.App"/><Reference Include="OssmmasoftVerticalSlice"><HintPath>/srv/ossmmasoft-pruebas/staging/r57-fps-20261119T232257Z/vertical-runtime/OssmmasoftVerticalSlice.dll</HintPath></Reference><Reference Include="Microsoft.Data.SqlClient"><HintPath>/srv/ossmmasoft-pruebas/current/vertical/Microsoft.Data.SqlClient.dll</HintPath></Reference><Reference Include="Oracle.ManagedDataAccess"><HintPath>/srv/ossmmasoft-pruebas/current/vertical/Oracle.ManagedDataAccess.dll</HintPath></Reference></ItemGroup></Project>')
 (p/'Program.cs').write_text(data['program']);(p/'Context.sql').write_text(data['sql'])
 r=subprocess.run(['dotnet','run','--project',str(p/'Probe.csproj'),'--verbosity','quiet','--',str(p/'Context.sql')],capture_output=True,text=True,timeout=360,env={**os.environ,'TZ':'America/Caracas'})
 lines=[line for line in r.stdout.splitlines() if line.startswith('R57_')]
 if lines: print('\\n'.join(lines))
 else: print('R57_RUNNER_FAILED='+str(r.returncode))
 sys.exit(r.returncode)
finally: shutil.rmtree(p)
'''
r=subprocess.run(['ssh','-i',str(Path.home()/'.ssh/ubuntu_pruebas'),'-o','BatchMode=yes','-o','ConnectTimeout=10','ossmma@74.121.190.6','python3 -c '+shlex.quote(remote)],input=payload,text=True,capture_output=True,timeout=390)
if r.returncode or 'R57_FPS=' not in r.stdout: raise SystemExit('No se completo la validacion QA: '+r.stdout[:500])
data=json.loads(r.stdout.split('R57_FPS=')[-1].strip())
Path('/tmp/mm-r57-fps-integracion.json').write_text(json.dumps(data,indent=2)+'\n')
print(json.dumps({'Status':data['Status'],'Checks':len(data['Checks']),'Outcomes':data['Outcomes']}))
