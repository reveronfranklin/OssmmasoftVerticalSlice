import json,subprocess,shlex
from pathlib import Path
base=Path(__file__).resolve().parents[3]
req=base.parent/'Requerimientos/57 - Motor Parametrizable De Calculo De Nomina'
samples=[x for x in json.loads((req/'MuestraTresAniosQA.json').read_text())['CandidatePeriods'] if x['Kind']=='ESPECIAL_E']
candidate=base.parent/'output/r57-captura-servidor-qa'
version=(candidate/'version.txt').read_text().strip()
pinned=json.loads((candidate/'api.expected.json').read_text())['OssmmasoftVerticalSlice.dll']
dll='/srv/ossmmasoft-pruebas/staging/'+version+'/vertical-runtime/OssmmasoftVerticalSlice.dll'

payload=json.dumps({'program':(base/'scripts/tests/rh-motor-nomina/CapturaServidorQA.cs.txt').read_text(),'sql':json.dumps({'Samples':samples}),'dll':dll,'hash':pinned})
remote='''import tempfile,pathlib,subprocess,json,shutil,sys,os,hashlib
p=pathlib.Path(tempfile.mkdtemp(prefix="r57-fuentes-"))
try:
 data=json.load(sys.stdin)
 assert hashlib.sha256(pathlib.Path(data['dll']).read_bytes()).hexdigest()==data['hash']
 (p/'Probe.csproj').write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable></PropertyGroup><ItemGroup><FrameworkReference Include="Microsoft.AspNetCore.App"/><Reference Include="OssmmasoftVerticalSlice"><HintPath>'+data['dll']+'</HintPath></Reference><Reference Include="Microsoft.Data.SqlClient"><HintPath>/srv/ossmmasoft-pruebas/current/vertical/Microsoft.Data.SqlClient.dll</HintPath></Reference><Reference Include="Oracle.ManagedDataAccess"><HintPath>/srv/ossmmasoft-pruebas/current/vertical/Oracle.ManagedDataAccess.dll</HintPath></Reference></ItemGroup></Project>')
 (p/'Program.cs').write_text(data['program']);(p/'Context.sql').write_text(data['sql'])
 proc=subprocess.Popen(['dotnet','run','--project',str(p/'Probe.csproj'),'--verbosity','quiet','--',str(p/'Context.sql')],stdout=subprocess.PIPE,stderr=subprocess.PIPE,text=True,start_new_session=True,env={**os.environ,'TZ':'America/Caracas'})
 try:stdout,stderr=proc.communicate(timeout=240)
 except subprocess.TimeoutExpired:
  os.killpg(proc.pid,9);stdout,stderr=proc.communicate(timeout=5)
  trace=p/'bin/Debug/net10.0/R57CapStage.log'
  print('R57_PROBE_TIMEOUT='+ (trace.read_text()[-1500:] if trace.exists() else 'before-main'))
  sys.exit(124)
 import types
 r=types.SimpleNamespace(stdout=stdout,returncode=proc.returncode)
 lines=[line for line in r.stdout.splitlines() if line.startswith('R57_') or ': error CS' in line]
 if lines: print('\\n'.join(lines))
 else: print('R57_RUNNER_FAILED='+str(r.returncode))
 sys.exit(r.returncode)
finally: shutil.rmtree(p)
'''
r=subprocess.run(['ssh','-i',str(Path.home()/'.ssh/ubuntu_pruebas'),'-o','BatchMode=yes','-o','ConnectTimeout=10','ossmma@74.121.190.6','python3 -c '+shlex.quote(remote)],input=payload,text=True,capture_output=True,timeout=270)
if r.returncode or 'R57_CAP_SERVIDOR=' not in r.stdout: raise SystemExit('No se completo la validacion QA: '+r.stdout[:500])
data=json.loads(r.stdout.split('R57_CAP_SERVIDOR=')[-1].strip())
(req/'CapturaServidorQA.json').write_text(json.dumps(data,indent=2)+'\n')
print(json.dumps({'Status':data['Status'],'Checks':len(data['Checks']),'Cases':data['Cases'],'Archives':data['Archives'],'Ordinary':data['OrdinaryContexts'],'Special':data['SpecialContexts'],'Outcomes':data['Outcomes']}))
