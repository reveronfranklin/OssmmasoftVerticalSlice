from pathlib import Path
import json,subprocess,shlex
root=Path(__file__).resolve().parents[4];req=root/'Requerimientos/57 - Motor Parametrizable De Calculo De Nomina';seed=json.loads((req/'VariacionesSinFormulaSemillaQA.json').read_text());policy=[{'CodigoTipoNomina':x['Type'],'CodigoConcepto':x['Concept'],'HashClasificacionEsperado':x['ClassificationHash'],'ReferenciaDecision':x['DecisionReference']} for x in seed['Entries']];assert len(policy)==50
remote='''import json,sys,pathlib,hashlib,os,stat,shutil,datetime
policy=json.load(sys.stdin);assert len(policy)==50
base=pathlib.Path('/srv/ossmmasoft-pruebas/shared/config');f=(base/'vertical.json').resolve();assert base in f.parents
assert (pathlib.Path('/srv/ossmmasoft-pruebas/current/vertical').resolve().name).startswith('r57-cap-model-')
raw=f.read_text();cfg=json.loads(raw);assert int(cfg['settings']['EmpresaConfig'])==13
section=cfg.setdefault('RhMotorNomina',{});assert not section.get('AplicacionEnabled',False)
previous=section.get('BonosManualesE');assert previous is None or previous==policy or (isinstance(previous,list) and len(previous)==39 and all(x in policy for x in previous)),'Existing policy differs'
changed=previous!=policy or not section.get('CorridasCapturaEnabled',False)
if changed:
 backup=f.with_name(f.name+'.r57-cap-srv-'+datetime.datetime.now(datetime.timezone.utc).strftime('%Y%m%dT%H%M%SZ')+'.bak');shutil.copy2(f,backup)
 section['BonosManualesE']=policy;section['CorridasCapturaEnabled']=True;tmp=f.with_name('.vertical.r57-captura-servidor.tmp')
 fd=os.open(tmp,os.O_WRONLY|os.O_CREAT|os.O_EXCL,stat.S_IMODE(f.stat().st_mode))
 with os.fdopen(fd,'w') as stream:json.dump(cfg,stream,indent=2);stream.write('\\n');stream.flush();os.fsync(stream.fileno())
 os.replace(tmp,f)
check=json.loads(f.read_text());assert check['RhMotorNomina']['BonosManualesE']==policy and not check['RhMotorNomina'].get('AplicacionEnabled',False)
print(json.dumps({'PolicyEntries':len(policy),'Changed':changed,'Company':13,'ApplicationEnabled':False,'CorridasCapturaEnabled':True,'OtherSettingsChanged':False,'PolicySha256':hashlib.sha256(json.dumps(policy,separators=(',',':')).encode()).hexdigest()}))
'''
r=subprocess.run(['ssh','-i',str(Path.home()/'.ssh/ubuntu_pruebas'),'-o','BatchMode=yes','-o','ConnectTimeout=10','ossmma@74.121.190.6','python3 -c '+shlex.quote(remote)],input=json.dumps(policy),text=True,capture_output=True,timeout=40)
if r.returncode:raise SystemExit('No se actualizo politica QA: '+r.stderr[:500])
d=json.loads(r.stdout);(req/'CapturaModeloPoliticaQA.json').write_text(json.dumps(d,indent=2)+'\n');print(d)
