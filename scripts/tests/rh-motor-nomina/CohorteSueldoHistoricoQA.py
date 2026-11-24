import json,subprocess,shlex
from pathlib import Path
base=Path(__file__).resolve().parents[3]
import argparse,re
parser=argparse.ArgumentParser()
parser.add_argument('--fecha',help='Limita a una fecha ya existente en el inventario, yyyy-MM-dd.')
parser.add_argument('--candidates',type=Path)
parser.add_argument('--tipo',type=int)
parser.add_argument('--periodo',type=int)
parser.add_argument('--runtime',default='/srv/ossmmasoft-pruebas/current/vertical')
parser.add_argument('--output',default='/tmp/mm-r57-cohorte.json')
args=parser.parse_args()
if not re.fullmatch(r'/srv/ossmmasoft-pruebas/(current/vertical|staging/r57-poblacion-\d{8}T\d{6}Z/vertical-runtime)',args.runtime): raise SystemExit('Runtime QA no permitido')
candidate_path=args.candidates or (base.parent/'Requerimientos/57 - Motor Parametrizable De Calculo De Nomina/MuestraTresAniosQA.json')
candidates=json.loads(candidate_path.read_text())['CandidatePeriods']
if args.tipo is not None: candidates=[x for x in candidates if x['Type']==args.tipo]
if args.periodo is not None:
 if args.tipo is None: raise SystemExit('--periodo requiere --tipo')
 candidates=[x for x in candidates if x['Period']==args.periodo]
if args.fecha:
 if not re.fullmatch(r'\d{4}-\d{2}-\d{2}',args.fecha): raise SystemExit('Fecha invalida')
 candidates=[x for x in candidates if x['Date'].startswith(args.fecha+'T')]
 if not candidates: raise SystemExit('La fecha no pertenece al inventario')
if not candidates: raise SystemExit('Ningun contexto corresponde a los filtros')
if len(candidates)>8: raise SystemExit('Limite de 8 contextos por ejecucion; fragmente con --tipo, --fecha y/o --periodo y preserve cada --output')
hashes={str(x['Type']):x['ModelHash'] for x in json.loads((base.parent/'Requerimientos/57 - Motor Parametrizable De Calculo De Nomina/ResultadoDiagnosticoSueldoQA.json').read_text())['Outcomes']}
payload=json.dumps({'program':(base/'scripts/tests/rh-motor-nomina/CohorteSueldoHistoricoQA.cs.txt').read_text(),'sql':json.dumps({'CandidatePeriods':candidates,'ModelHashes':hashes}),'runtime':args.runtime})
remote='''import tempfile,pathlib,subprocess,json,shutil,sys,os
p=pathlib.Path(tempfile.mkdtemp(prefix="r57-fuentes-"))
try:
 data=json.load(sys.stdin)
 runtime=data['runtime']
 (p/'Probe.csproj').write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable></PropertyGroup><ItemGroup><FrameworkReference Include="Microsoft.AspNetCore.App"/><Reference Include="OssmmasoftVerticalSlice"><HintPath>'+runtime+'/OssmmasoftVerticalSlice.dll</HintPath></Reference><Reference Include="Microsoft.Data.SqlClient"><HintPath>/srv/ossmmasoft-pruebas/current/vertical/Microsoft.Data.SqlClient.dll</HintPath></Reference><Reference Include="Oracle.ManagedDataAccess"><HintPath>/srv/ossmmasoft-pruebas/current/vertical/Oracle.ManagedDataAccess.dll</HintPath></Reference></ItemGroup></Project>')
 (p/'Program.cs').write_text(data['program']);(p/'Context.sql').write_text(data['sql'])
 r=subprocess.run(['dotnet','run','--project',str(p/'Probe.csproj'),'--verbosity','quiet','--',str(p/'Context.sql')],capture_output=True,text=True,timeout=600,env={**os.environ,'TZ':'America/Caracas'})
 lines=[line for line in r.stdout.splitlines() if line.startswith('R57_') or ': error CS' in line]
 if lines: print('\\n'.join(lines))
 else: print('R57_RUNNER_FAILED='+str(r.returncode))
 sys.exit(r.returncode)
finally: shutil.rmtree(p)
'''
r=subprocess.run(['ssh','-i',str(Path.home()/'.ssh/ubuntu_pruebas'),'-o','BatchMode=yes','-o','ConnectTimeout=10','ossmma@74.121.190.6','python3 -c '+shlex.quote(remote)],input=payload,text=True,capture_output=True,timeout=630)
if r.returncode or 'R57_COHORTE=' not in r.stdout: raise SystemExit('No se completo la validacion QA: '+r.stdout[:500])
data=json.loads(r.stdout.split('R57_COHORTE=')[-1].strip())
Path(args.output).write_text(json.dumps(data,indent=2)+'\n')
print(json.dumps({'Status':data['Status'],'Checks':len(data['Checks']),'Outcomes':data['Outcomes']}))
