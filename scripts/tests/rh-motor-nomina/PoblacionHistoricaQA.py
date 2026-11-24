"""QA R57: universo historico completo, sin importes ni DML de nomina."""
import argparse,json,subprocess,shlex,re
from pathlib import Path
base=Path(__file__).resolve().parents[3]
parser=argparse.ArgumentParser()
parser.add_argument('--runtime',default='/srv/ossmmasoft-pruebas/current/vertical')
parser.add_argument('--install',action='store_true',help='Instala solo SP_RH_NOM_POB_HIST en QA autorizado.')
parser.add_argument('--isolated-sql',action='store_true',help='Prueba SQL en cuatro objetos desechables; luego los elimina.')
parser.add_argument('--output',type=Path,default=Path('/tmp/mm-r57-poblacion.json'))
parser.add_argument('--candidates',type=Path,help='Inventario local con CandidatePeriods para validar una ventana ampliada.')
args=parser.parse_args()
candidate_path=args.candidates or (base.parent/'Requerimientos/57 - Motor Parametrizable De Calculo De Nomina/MuestraTresAniosQA.json')
candidate_data=json.loads(candidate_path.read_text())
if not candidate_data.get('CandidatePeriods'): raise SystemExit('El inventario no contiene periodos seleccionados')
candidate_data['RunIsolatedSql']=args.isolated_sql
if not re.fullmatch(r'/srv/ossmmasoft-pruebas/(current/vertical|staging/r57-poblacion-\d{8}T\d{6}Z/vertical-runtime)',args.runtime):
    raise SystemExit('Runtime QA fuera de la ruta permitida.')
candidates=base.parent/'Requerimientos/57 - Motor Parametrizable De Calculo De Nomina/FuentesHistoricasBasesQA.json'
payload=json.dumps({'program':(base/'scripts/tests/rh-motor-nomina/PoblacionHistoricaQA.cs.txt').read_text(),
    'sql':(base/'Features/RhMotorNomina/Sql/SP_RH_NOM_POB_HIST.sql').read_text() if args.install else '',
    'candidates':candidate_data,'runtime':args.runtime,
    'definition':(base/'Features/RhMotorNomina/Sql/SP_RH_NOM_POB_HIST.sql').read_text()})
remote='''import tempfile,pathlib,subprocess,json,shutil,sys,os
p=pathlib.Path(tempfile.mkdtemp(prefix="r57-poblacion-"))
try:
 data=json.load(sys.stdin)
 runtime=data['runtime']
 (p/'Probe.csproj').write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable></PropertyGroup><ItemGroup><FrameworkReference Include="Microsoft.AspNetCore.App"/><Reference Include="OssmmasoftVerticalSlice"><HintPath>'+runtime+'/OssmmasoftVerticalSlice.dll</HintPath></Reference><Reference Include="Microsoft.Data.SqlClient"><HintPath>'+runtime+'/Microsoft.Data.SqlClient.dll</HintPath></Reference><Reference Include="Oracle.ManagedDataAccess"><HintPath>'+runtime+'/Oracle.ManagedDataAccess.dll</HintPath></Reference></ItemGroup></Project>')
 (p/'Definition.sql').write_text(data['definition']);(p/'Program.cs').write_text(data['program']);(p/'Context.sql').write_text(data['sql']);(p/'Candidates.json').write_text(json.dumps(data['candidates']))
 r=subprocess.run(['dotnet','run','--project',str(p/'Probe.csproj'),'--verbosity','quiet','--',str(p/'Context.sql'),str(p/'Candidates.json'),str(p/'Definition.sql')],capture_output=True,text=True,timeout=600,env={**os.environ,'TZ':'America/Caracas'})
 lines=[line for line in r.stdout.splitlines() if line.startswith('R57_') or ': error CS' in line]
 print('\\n'.join(lines) if lines else 'R57_RUNNER_FAILED='+str(r.returncode))
 sys.exit(r.returncode)
finally: shutil.rmtree(p)
'''
r=subprocess.run(['ssh','-i',str(Path.home()/'.ssh/ubuntu_pruebas'),'-o','BatchMode=yes','-o','ConnectTimeout=10','ossmma@74.121.190.6','python3 -c '+shlex.quote(remote)],input=payload,text=True,capture_output=True,timeout=630)
if r.returncode or 'R57_POBLACION=' not in r.stdout: raise SystemExit('No se completo la validacion QA: '+r.stdout[:1500])
data=json.loads(r.stdout.split('R57_POBLACION=')[-1].strip())
args.output.write_text(json.dumps(data,indent=2)+'\n')
print(json.dumps({'Status':data['Status'],'Checks':len(data['Checks']),'Periods':len(data['Outcomes']),'Types':len({x['Type'] for x in data['Outcomes']}),'Outcomes':data['Outcomes']}))
