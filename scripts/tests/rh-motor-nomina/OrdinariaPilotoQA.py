import json,subprocess,shlex
from pathlib import Path
base=Path(__file__).resolve().parents[3]
req=base.parent/'Requerimientos/57 - Motor Parametrizable De Calculo De Nomina'
samples=[x for x in json.loads((req/'MuestraTresAniosQA.json').read_text())['CandidatePeriods'] if x['Kind']=='ESPECIAL_E']
candidate=base.parent/'output/r57-presupuesto-contrato-qa'
version=(candidate/'version.txt').read_text().strip()
pinned=json.loads((candidate/'api.expected.json').read_text())['OssmmasoftVerticalSlice.dll']
dll='/srv/ossmmasoft-pruebas/staging/'+version+'/vertical-runtime/OssmmasoftVerticalSlice.dll'

import sys,hashlib
mode=sys.argv[1] if len(sys.argv)>1 else 'Preparar'
assert mode in ('Preparar','Worker','Referencias','Poblacion','Lote','Progreso','Lectura','LotePequeno','AuditarLote','Reencolar','Exclusiones','Incidencias','Cobertura','Rehabilitar','Triggers','RepararTrigger','Reprocesar974','VerificarFinal','SueldoEtapa','EnlaceEtapa','CadenaWorker','CadenaLote','CoberturaRamas','CadenaProgreso','CadenaLectura','CadenaEncolar','DocInstalar','DocVerificar','CadenaCierre','PrenominaEstructura','PrenominaInstalar','PrenominaLoteInstalar','PrenominaLotePrueba','PrenominaLoteProgreso','PrenominaLoteTemporal','PrenominaLoteCierre','PrenominaLoteCancelar','PrenominaModeloLector','PrenominaLoteMedir','PrenominaLoteModelos','PrenominaLoteDiferencia','PrenominaLoteTick','PrenominaEspacio','PrenominaLoteError','PrenominaLoteFrescura','PrenominaLoteRecuperar','PrenominaLoteActualCrear','PrenominaLoteActualProgreso','PrenominaLoteActualTemporal','PrenominaLoteActualAplicar','PrenominaUiProgreso','PrenominaUiVerificar','PrenominaSecuencia','PrenominaSecuenciaAlinear','PrenominaTemporalDiagnostico','PrenominaInitrans','PrenominaDependencias')
if mode in ('CadenaWorker','CadenaLote','CadenaLectura','CadenaEncolar','CadenaCierre'):
 candidate=base.parent/('output/r57-chain-clob-qa' if (base.parent/'output/r57-chain-clob-qa/api.expected.json').exists() else 'output/r57-chain-timing-qa' if (base.parent/'output/r57-chain-timing-qa/api.expected.json').exists() else 'output/r57-chain-worker-qa');version=(candidate/'version.txt').read_text().strip();pinned=json.loads((candidate/'api.expected.json').read_text())['OssmmasoftVerticalSlice.dll'];dll='/srv/ossmmasoft-pruebas/staging/'+version+'/vertical-runtime/OssmmasoftVerticalSlice.dll'
if mode=='DocVerificar':
 candidate=base.parent/'output/r57-chain-clob-qa';version=(candidate/'version.txt').read_text().strip();pinned=json.loads((candidate/'api.expected.json').read_text())['OssmmasoftVerticalSlice.dll'];dll='/srv/ossmmasoft-pruebas/staging/'+version+'/vertical-runtime/OssmmasoftVerticalSlice.dll'
content={'Samples':samples}
if mode=='PrenominaInitrans':content['InitransSql']=(base/'Features/RhMotorNomina/Sql/QA_RH_NOM_PRE_INITRANS.sql').read_text()
if mode=='PrenominaSecuenciaAlinear':content['AlignmentSql']=(base/'Features/RhMotorNomina/Sql/QA_RH_NOM_SEQ_ALINEAR.sql').read_text()
if mode!='Preparar':content['Pilot']=json.loads((req/'OrdinariaPilotoPrepararQA.json').read_text())['Details']
if mode=='Preparar':
 sql=(base/'Features/RhMotorNomina/Sql/QA_RH_NOM_PER_PIL.sql').read_text()
 content={'Sql':sql,'SqlSha256':hashlib.sha256(sql.encode()).hexdigest()}
batch=int(sys.argv[2]) if mode in ('Lote','LotePequeno') else None
if mode in ('Lote','LotePequeno'):
 assert batch is not None
 if mode=='Lote':assert 0<=batch<14
 else:assert 10<=batch<132 and batch%2==0
 pop=json.loads((req/'OrdinariaPilotoPoblacionQA.json').read_text())['Details'];content.update({'BatchStart':batch*10 if mode=='Lote' else batch,'BatchSize':10 if mode=='Lote' else 2,'ExpectedPopulationHash':pop['PopulationHash'],'ExpectedEligible':pop['EligibleSingleCargo']})
if mode=='AuditarLote':
 batch=int(sys.argv[2]);assert 0<=batch<132 and batch%10==0
 paths=[req/'OrdinariaPilotoLote00QA.json']+[req/('OrdinariaPilotoLoteS'+str(n).zfill(4)+'QA.json') for n in range(10,132,2)]
 all_cases=[c for f in paths if f.exists() for c in json.loads(f.read_text())['Details']['Cases']];assert len({c['CaseHash'] for c in all_cases})==len(all_cases) and batch<len(all_cases)
 recovery_files=sorted(req.glob('OrdinariaPilotoReencolar*QA.json'))
 recovered={c['CaseHash']:c for f in recovery_files for c in json.loads(f.read_text())['Details']['Cases'] if c.get('Run') is not None}
 for case in all_cases:
  if case['CaseHash'] in recovered and case['Run'] is None:
   case['PreviousErrors']=case['Errors'];case['Run']=recovered[case['CaseHash']]['Run'];case['Errors']=[]
 content['Cases']=all_cases[batch:batch+10];content['BatchStart']=batch
if mode=='Reencolar':
 batch=int(sys.argv[2]);assert batch>=0 and batch%2==0
 paths=[req/'OrdinariaPilotoLote00QA.json']+[req/('OrdinariaPilotoLoteS'+str(n).zfill(4)+'QA.json') for n in range(10,132,2)]
 all_cases=[c for f in paths for c in json.loads(f.read_text())['Details']['Cases']];assert len(all_cases)==132
 missing=[c for c in all_cases if c['Run'] is None and c['Archive'] is not None];content['Cases']=missing[batch:batch+2];content['BatchStart']=batch
 assert content['Cases']
if mode in ('Incidencias','VerificarFinal'):content['Cases']=[c for c in json.loads((req/'OrdinariaPilotoResultadoFinalQA.json').read_text())['Cases'] if c['State']=='CALCULADO_CONTEXTO_BLOQUEADO']
if mode=='RepararTrigger':
 content['OriginalTriggerSha256']='A8862A0DF59AC0EB95C9A570B48627095403C428553CF1FD2D2538D43007B60D'
 content['ReplacementTrigger']=(req/'RH_TRG_BEF_UPD_CONCEPTOS.qa.sql').read_text().removesuffix('\n/\n')
if mode=='Reprocesar974':
 batch=int(sys.argv[2]);assert batch in (0,2,4,6)
 content['Cases']=[c for c in json.loads((req/'OrdinariaPilotoResultadoFinalQA.json').read_text())['Cases'] if c['State']=='CALCULADO_CONTEXTO_BLOQUEADO'][batch:batch+2]
 content['BatchStart']=batch
if mode in ('SueldoEtapa','EnlaceEtapa'):
 batch=int(sys.argv[2]);assert 0<=batch<132 and batch%10==0
 content['Cases']=json.loads((req/'OrdinariaPilotoResultadoRehabilitadoQA.json').read_text())['Cases'][batch:batch+10]
 content['BatchStart']=batch
if mode=='CadenaWorker':
 batch=int(sys.argv[2]);assert batch in (0,2,4)
 all_cases=json.loads((req/'OrdinariaPilotoResultadoRehabilitadoQA.json').read_text())['Cases']
 selected=[all_cases[n] for n in (0,65,130,131)];rehabilitated=[c for c in all_cases if 'PreviousArchive' in c];assert len(rehabilitated)==7;selected += [rehabilitated[0],rehabilitated[-1]];content['Cases']=selected[batch:batch+2]
if mode=='CadenaLote':
 batch=int(sys.argv[2]);assert 0<=batch<132 and batch%2==0
 content['Cases']=json.loads((req/'OrdinariaPilotoResultadoRehabilitadoQA.json').read_text())['Cases'][batch:batch+2]
if mode=='CadenaEncolar':
 batch=int(sys.argv[2]);assert 0<=batch<132 and batch%4==0
 content['Cases']=json.loads((req/'OrdinariaPilotoResultadoRehabilitadoQA.json').read_text())['Cases'][batch:batch+4]
if mode=='PrenominaModeloLector':
 content['Scripts']=[(base/'Features/RhMotorNomina/Sql/SP_RH_NOM_VER_DOC.sql').read_text()]
if mode=='PrenominaLoteInstalar':
 content['Scripts']=[(base/'Features/RhMotorNomina/Sql'/n).read_text() for n in ['SP_RH_NOM_CAP_DOC.sql','12_RH_NOM_PRE_LOT.sql','13_RH_NOM_PRE_TMP.sql']]
if mode in ('PrenominaLoteProgreso','PrenominaLoteTemporal','PrenominaLoteCierre','PrenominaLoteCancelar','PrenominaModeloLector','PrenominaLoteMedir','PrenominaLoteModelos','PrenominaLoteDiferencia','PrenominaLoteTick','PrenominaEspacio','PrenominaLoteError','PrenominaLoteFrescura','PrenominaLoteRecuperar','PrenominaLoteActualCrear','PrenominaLoteActualProgreso','PrenominaLoteActualTemporal','PrenominaLoteActualAplicar','PrenominaUiProgreso','PrenominaUiVerificar','PrenominaSecuencia','PrenominaSecuenciaAlinear','PrenominaTemporalDiagnostico','PrenominaInitrans','PrenominaDependencias'):
 candidate=base.parent/('output/r57-prenomina-txn-qa' if (base.parent/'output/r57-prenomina-txn-qa/api.expected.json').exists() else 'output/r57-prenomina-retry-qa' if (base.parent/'output/r57-prenomina-retry-qa/api.expected.json').exists() else 'output/r57-prenomina-infra-qa' if (base.parent/'output/r57-prenomina-infra-qa/api.expected.json').exists() else 'output/r57-prenomina-clob-qa' if (base.parent/'output/r57-prenomina-clob-qa/api.expected.json').exists() else 'output/r57-prenomina-qa');version=(candidate/'version.txt').read_text().strip();pinned=json.loads((candidate/'api.expected.json').read_text())['OssmmasoftVerticalSlice.dll'];dll='/srv/ossmmasoft-pruebas/staging/'+version+'/vertical-runtime/OssmmasoftVerticalSlice.dll'
if mode=='PrenominaLotePrueba':
 content['Cases']=json.loads((req/'CadenaSueldoPoblacionWorkerQA.json').read_text())['Cases']
 candidate=base.parent/('output/r57-prenomina-txn-qa' if (base.parent/'output/r57-prenomina-txn-qa/api.expected.json').exists() else 'output/r57-prenomina-retry-qa' if (base.parent/'output/r57-prenomina-retry-qa/api.expected.json').exists() else 'output/r57-prenomina-infra-qa' if (base.parent/'output/r57-prenomina-infra-qa/api.expected.json').exists() else 'output/r57-prenomina-clob-qa' if (base.parent/'output/r57-prenomina-clob-qa/api.expected.json').exists() else 'output/r57-prenomina-qa');version=(candidate/'version.txt').read_text().strip();pinned=json.loads((candidate/'api.expected.json').read_text())['OssmmasoftVerticalSlice.dll'];dll='/srv/ossmmasoft-pruebas/staging/'+version+'/vertical-runtime/OssmmasoftVerticalSlice.dll'
if mode=='PrenominaInstalar':
 content['Sql']=(base/'Features/RhMotorNomina/Sql/SP_RH_NOM_PRE_FUE.sql').read_text()
if mode=='DocInstalar':
 content['Sql']=(base/'Features/RhMotorNomina/Sql/11_RH_NOM_RUN_DOC.sql').read_text()
if mode=='PrenominaLoteTemporal' and (req/'PrenominaUiLISTAQA.json').exists():
 content['StampBefore']=json.loads((req/'PrenominaUiLISTAQA.json').read_text())['Details']['PeriodStamp']
if mode in ('CadenaCierre','PrenominaLoteTemporal'):
 content['Cases']=json.loads((req/'CadenaSueldoPoblacionWorkerQA.json').read_text())['Cases']
programMode='CadenaWorker'    if mode=='CadenaLote' else 'Lote'     if mode=='LotePequeno' else mode
payload=json.dumps({'stopRuntime':mode in ('PrenominaLoteCancelar','PrenominaLoteActualAplicar','PrenominaTemporalDiagnostico','PrenominaInitrans','PrenominaDependencias'),'longProbe':mode in ('PrenominaLoteTemporal','PrenominaLoteActualTemporal','PrenominaLoteActualAplicar','PrenominaUiProgreso','PrenominaUiVerificar','PrenominaSecuencia','PrenominaSecuenciaAlinear','PrenominaTemporalDiagnostico','PrenominaInitrans','PrenominaDependencias'),'program':(base/'scripts/tests/rh-motor-nomina'/('OrdinariaPiloto'+programMode+'QA.cs.txt')).read_text(),'sql':json.dumps(content),'dll':dll,'hash':pinned})
if mode in ('SueldoEtapa','EnlaceEtapa'):
 data=json.loads(payload);data['helper']=(base/'Features/RhMotorNomina/RhNomEtapaSueldo.cs').read_text();data['enlace']=(base/'Features/RhMotorNomina/RhNomEnlaceSueldo.cs').read_text() if mode=='EnlaceEtapa' else None;payload=json.dumps(data)
if mode=='CadenaLote':
 data=json.loads(payload);data['cache']='/srv/ossmmasoft-pruebas/staging/'+version+'/qa-probes/'+hashlib.sha256((data['program']+pinned).encode()).hexdigest()[:24];payload=json.dumps(data)
remote='''import tempfile,pathlib,subprocess,json,shutil,sys,os,hashlib
p=pathlib.Path(tempfile.mkdtemp(prefix="r57-fuentes-"))
try:
 data=json.load(sys.stdin)
 if data.get('stopRuntime'):subprocess.run(['systemctl','--user','stop','ossmmasoft-vertical.service'],check=True)
 assert hashlib.sha256(pathlib.Path(data['dll']).read_bytes()).hexdigest()==data['hash']
 (p/'Probe.csproj').write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable></PropertyGroup><ItemGroup><FrameworkReference Include="Microsoft.AspNetCore.App"/><Reference Include="OssmmasoftVerticalSlice"><HintPath>'+data['dll']+'</HintPath></Reference><Reference Include="Microsoft.Data.SqlClient"><HintPath>/srv/ossmmasoft-pruebas/current/vertical/Microsoft.Data.SqlClient.dll</HintPath></Reference><Reference Include="Oracle.ManagedDataAccess"><HintPath>/srv/ossmmasoft-pruebas/current/vertical/Oracle.ManagedDataAccess.dll</HintPath></Reference></ItemGroup></Project>')
 if data.get('enlace'): (p/'RhNomEnlaceSueldo.cs').write_text(data['enlace'])
 if 'helper' in data: (p/'RhNomEtapaSueldo.cs').write_text(data['helper'])
 (p/'Program.cs').write_text(data['program']);(p/'Context.sql').write_text(data['sql'])
 command=['dotnet','run','--project',str(p/'Probe.csproj'),'--verbosity','quiet','--',str(p/'Context.sql')]
 if data.get('cache'):
  import fcntl
  cache=pathlib.Path(data['cache']);cache.mkdir(parents=True,exist_ok=True)
  with (cache/'.build.lock').open('w') as lock:
   fcntl.flock(lock,fcntl.LOCK_EX)
   binary=cache/'bin/Probe.dll';receipt=cache/'binary.sha256'
   if not receipt.exists():
    shutil.copyfile(p/'Probe.csproj',cache/'Probe.csproj');shutil.copyfile(p/'Program.cs',cache/'Program.cs')
    built=subprocess.run(['dotnet','build',str(cache/'Probe.csproj'),'-o',str(cache/'bin'),'--verbosity','quiet'],capture_output=True,text=True,timeout=120,env={**os.environ,'TZ':'America/Caracas'})
    if built.returncode: print('R57_BUILD_FAILED='+str(built.returncode));sys.exit(built.returncode)
    receipt.write_text(hashlib.sha256(binary.read_bytes()).hexdigest())
   assert receipt.read_text()==hashlib.sha256(binary.read_bytes()).hexdigest()
  command=['dotnet',str(binary),str(p/'Context.sql')]
 proc=subprocess.Popen(command,stdout=subprocess.PIPE,stderr=subprocess.PIPE,text=True,start_new_session=True,env={**os.environ,'TZ':'America/Caracas'})
 try:stdout,stderr=proc.communicate(timeout=900 if data.get('longProbe') else 240)
 except subprocess.TimeoutExpired:
  os.killpg(proc.pid,9);stdout,stderr=proc.communicate(timeout=5)
  trace=p/'bin/Debug/net10.0/R57CapStage.log'
  print('R57_PROBE_TIMEOUT='+ (trace.read_text()[-1500:] if trace.exists() else 'trace-no-disponible'))
  sys.exit(124)
 import types
 r=types.SimpleNamespace(stdout=stdout,returncode=proc.returncode)
 lines=[line for line in r.stdout.splitlines() if line.startswith('R57_') or ': error CS' in line]
 if lines: print('\\n'.join(lines))
 else: print('R57_RUNNER_FAILED='+str(r.returncode))
 sys.exit(r.returncode)
finally:
 if 'data' in locals() and data.get('stopRuntime'):subprocess.run(['systemctl','--user','start','ossmmasoft-vertical.service'],check=True)
 shutil.rmtree(p)
'''
r=subprocess.run(['ssh','-i',str(Path.home()/'.ssh/ubuntu_pruebas'),'-o','BatchMode=yes','-o','ConnectTimeout=10','ossmma@74.121.190.6','python3 -c '+shlex.quote(remote)],input=payload,text=True,capture_output=True,timeout=930 if mode in ('PrenominaLoteTemporal','PrenominaLoteActualTemporal','PrenominaLoteActualAplicar','PrenominaUiProgreso','PrenominaUiVerificar','PrenominaSecuencia','PrenominaSecuenciaAlinear','PrenominaTemporalDiagnostico','PrenominaInitrans','PrenominaDependencias') else 270)
marker='R57_CAP_MODELO='
if r.returncode or marker not in r.stdout: raise SystemExit('No se completo QA: '+r.stdout[:1800])
data=json.loads(r.stdout.split(marker)[-1].strip());
if mode=='PrenominaLoteTemporal' and 'Stage' in data.get('Details',{}):(req/('PrenominaUi'+data['Details']['Stage']+'QA.json')).write_text(json.dumps(data,indent=2)+'\n')
name=('OrdinariaPilotoCadenaEncolar'+str(batch).zfill(4)+'QA.json') if mode=='CadenaEncolar' else ('OrdinariaPilotoCadenaLote'+str(batch).zfill(4)+'QA.json') if mode=='CadenaLote' else ('OrdinariaPilotoCadenaWorker'+str(batch).zfill(4)+'QA.json') if mode=='CadenaWorker' else ('OrdinariaPilotoEnlaceEtapa'+str(batch).zfill(4)+'QA.json') if mode=='EnlaceEtapa' else ('OrdinariaPilotoSueldoEtapa'+str(batch).zfill(4)+'QA.json') if mode=='SueldoEtapa' else ('OrdinariaPilotoReprocesar974'+str(batch).zfill(4)+'QA.json') if mode=='Reprocesar974' else ('OrdinariaPilotoReencolar'+str(batch).zfill(4)+'QA.json') if mode=='Reencolar' else ('OrdinariaPilotoAudit'+str(batch).zfill(4)+'QA.json') if mode=='AuditarLote' else ('OrdinariaPilotoLoteS'+str(batch).zfill(4)+'QA.json') if mode=='LotePequeno' else ('OrdinariaPilotoLote'+str(batch).zfill(2)+'QA.json') if mode=='Lote' else ('OrdinariaPiloto'+mode+'QA.json');(req/name).write_text(json.dumps(data,indent=2)+'\n')
if isinstance(data.get('Details'),dict) and 'Counts' in data['Details'] and 'Cases' in data['Details']:print(json.dumps({'Status':data['Status'],'Batch':batch,'Cases':len(data['Details']['Cases']),'Counts':data['Details']['Counts']}))
else:print(json.dumps({'Status':data['Status'],'Checks':len(data['Checks']),'Details':data.get('Details')}))
