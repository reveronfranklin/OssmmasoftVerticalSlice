from pathlib import Path
import subprocess,shlex,json
root=Path(__file__).resolve().parents[4]
remote='''from pathlib import Path
import json,os,stat,shutil,datetime
base=Path('/srv/ossmmasoft-pruebas');active=(base/'current/vertical').resolve().name
assert active=='r57-cap-model-20261122T235432Z' or active.startswith('r57-budget-')
f=(base/'shared/config/vertical.json').resolve();assert (base/'shared/config') in f.parents
cfg=json.loads(f.read_text());assert int(cfg['settings']['EmpresaConfig'])==13
section=cfg.setdefault('RhMotorNomina',{});assert not section.get('AplicacionEnabled',False) and len(section.get('BonosManualesE',[]))==53
changed=section.get('ContratoPresupuestoTipoActualEnabled') is not True
if changed:
 backup=f.with_name(f.name+'.r57-budget-'+datetime.datetime.now(datetime.timezone.utc).strftime('%Y%m%dT%H%M%SZ')+'.bak');shutil.copy2(f,backup)
 section['ContratoPresupuestoTipoActualEnabled']=True;tmp=f.with_name('.vertical.r57-budget.tmp')
 fd=os.open(tmp,os.O_WRONLY|os.O_CREAT|os.O_EXCL,stat.S_IMODE(f.stat().st_mode))
 with os.fdopen(fd,'w') as stream:json.dump(cfg,stream,indent=2);stream.write('\\n');stream.flush();os.fsync(stream.fileno())
 os.replace(tmp,f)
check=json.loads(f.read_text());assert check==cfg and not check['RhMotorNomina'].get('AplicacionEnabled',False)
print(json.dumps({'Company':13,'Changed':changed,'ContractEnabled':True,'FinancialApplicationEnabled':False,'OtherSettingsChanged':False}))
'''
r=subprocess.run(['ssh','-i',str(Path.home()/'.ssh/ubuntu_pruebas'),'-o','BatchMode=yes','-o','ConnectTimeout=10','ossmma@74.121.190.6','python3 -c '+shlex.quote(remote)],capture_output=True,text=True,timeout=35)
if r.returncode:raise SystemExit('No se configuro QA: '+r.stderr[:400])
data=json.loads(r.stdout);(root/'Requerimientos/57 - Motor Parametrizable De Calculo De Nomina/PresupuestoContratoConfiguracionQA.json').write_text(json.dumps(data,indent=2)+'\n');print(data)
