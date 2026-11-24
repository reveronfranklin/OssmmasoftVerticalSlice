import json,subprocess,shlex
from pathlib import Path
base=Path(__file__).resolve().parents[3]
req=base.parent/'Requerimientos/57 - Motor Parametrizable De Calculo De Nomina'
samples=[x for x in json.loads((req/'MuestraTresAniosQA.json').read_text())['CandidatePeriods'] if x['Kind']=='ESPECIAL_E']
candidate=base.parent/'output/r57-corrida-captura-qa'
version=(candidate/'version.txt').read_text().strip()
pinned=json.loads((candidate/'api.expected.json').read_text())['OssmmasoftVerticalSlice.dll']
dll='/srv/ossmmasoft-pruebas/staging/'+version+'/vertical-runtime/OssmmasoftVerticalSlice.dll'

import sys,hashlib
mode=sys.argv[1] if len(sys.argv)>1 else 'PilotoEstructura'
assert mode in ('PilotoEstructura',)
content={'Samples':samples}
if mode=='Instalar':
 sql='\n'.join((base/'Features/RhMotorNomina/Sql'/n).read_text() for n in ['03_RH_NOM_RUN.sql','10_RH_NOM_RUN_CAP.sql','09_RH_NOM_CAP_ARC.sql'])
 content={'Sql':sql,'SqlSha256':hashlib.sha256(sql.encode()).hexdigest()}
payload=json.dumps({'program':(base/'scripts/tests/rh-motor-nomina'/('Ordinaria'+mode+'QA.cs.txt')).read_text(),'sql':json.dumps(content),'dll':dll,'hash':pinned})
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
marker='R57_CAP_MODELO='
if r.returncode or marker not in r.stdout: raise SystemExit('No se completo QA: '+r.stdout[:1800])
data=json.loads(r.stdout.split(marker)[-1].strip());(req/('Ordinaria'+mode+'QA.json')).write_text(json.dumps(data,indent=2)+'\n')
print(json.dumps({'Status':data['Status'],'Checks':len(data['Checks']),'Details':data.get('Details')}))
