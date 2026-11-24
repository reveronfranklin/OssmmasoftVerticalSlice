#!/usr/bin/env python3
"""Stage only the R58 DLL/PDB. Published sources and dependency graph must match baseline."""
import datetime,hashlib,json,subprocess,shlex,tarfile
from pathlib import Path
ROOT=Path(__file__).resolve().parents[4];E=ROOT/'Requerimientos/58 - Revision Compronbantes Automaticos/Evidencias';pub=Path('/private/tmp/r58-signos-publish')
assert json.loads((E/'AlcanceApiSignosLocal.json').read_text())['ScopeValidated']
assert json.loads(Path('/private/tmp/r58-baseline-api.deps.json').read_text())==json.loads((pub/'OssmmasoftVerticalSlice.deps.json').read_text())
proof=json.loads(next(x for x in Path('/private/tmp/r58-new-api-source-proof.json').read_text().splitlines() if x.startswith('{')));assert not proof['Changed'] and not proof['LocalOnly']
name='r58-signos-api-20261124T194629Z'
files={n:hashlib.sha256((pub/n).read_bytes()).hexdigest() for n in ['OssmmasoftVerticalSlice.dll','OssmmasoftVerticalSlice.pdb']}
ssh=['ssh','-i',str(Path.home()/'.ssh/ubuntu_pruebas'),'-o','BatchMode=yes','-o','ConnectTimeout=10','ossmma@74.121.190.6']
remote=r'''import pathlib,subprocess,sys,json,hashlib,os
x=json.load(sys.stdin);base=pathlib.Path('/srv/ossmmasoft-pruebas');old=(base/'current/vertical').resolve();assert old.name=='r57-prenomina-txn-20261124T091632Z'
new=base/'releases/vertical'/x['Name']
if not new.exists():
 new.mkdir();subprocess.run(['cp','-al',str(old)+'/.',str(new)],check=True)
 for n in x['Files']:(new/n).unlink()
 (new/'.r58-incoming').mkdir(mode=0o700)
else:
 assert (new/'.r58-incoming').is_dir()
 for n in x['Files']:
  if (new/n).exists():assert hashlib.sha256((new/n).read_bytes()).hexdigest()==x['Files'][n]
print(json.dumps({'OldRelease':str(old),'NewRelease':str(new),'OldDllSha256':hashlib.sha256((old/'OssmmasoftVerticalSlice.dll').read_bytes()).hexdigest(),'OldPdbSha256':hashlib.sha256((old/'OssmmasoftVerticalSlice.pdb').read_bytes()).hexdigest(),'Files':x['Files'],'DependencyGraphPreserved':True,'ApplicationStopped':False,'SqlInstalled':False}))
'''
r=subprocess.run(ssh+['python3 -c '+shlex.quote(remote)],input=json.dumps({'Name':name,'Files':files}),capture_output=True,text=True,timeout=50)
if r.returncode:raise SystemExit(r.stderr[:1000])
stage=json.loads(r.stdout)
stage['ArtifactsVerified']=False
(E/'ApiSignosStageQA.json').write_text(json.dumps(stage,indent=2)+'\n')
archive=Path('/private/tmp/r58-signos-api.tar.gz')
with tarfile.open(archive,'w:gz') as tf:
 for n in files:tf.add(pub/n,arcname=n)
r=subprocess.run(['scp','-C','-i',str(Path.home()/'.ssh/ubuntu_pruebas'),'-o','BatchMode=yes','-o','ConnectTimeout=10',str(archive),'ossmma@74.121.190.6:'+stage['NewRelease']+'/.r58-incoming/api.tar.gz'],capture_output=True,text=True,timeout=180)
if r.returncode:raise SystemExit(r.stderr[:1000])
remote=r'''import pathlib,json,sys,hashlib,os,tarfile
x=json.load(sys.stdin);p=pathlib.Path(x['NewRelease']);old=pathlib.Path(x['OldRelease'])
with tarfile.open(p/'.r58-incoming/api.tar.gz','r:gz') as tf:
 assert set(tf.getnames())==set(x['Files']) and all(m.isfile() for m in tf.getmembers())
 tf.extractall(p/'.r58-incoming',filter='data')
for name,want in x['Files'].items():
 src=p/'.r58-incoming'/name;assert hashlib.sha256(src.read_bytes()).hexdigest()==want
 if (p/name).exists():assert hashlib.sha256((p/name).read_bytes()).hexdigest()==want
 os.replace(src,p/name)
assert hashlib.sha256((old/'OssmmasoftVerticalSlice.dll').read_bytes()).hexdigest()==x['OldDllSha256']
assert hashlib.sha256((old/'OssmmasoftVerticalSlice.pdb').read_bytes()).hexdigest()==x['OldPdbSha256']
assert (p/'OssmmasoftVerticalSlice.deps.json').read_bytes()==(old/'OssmmasoftVerticalSlice.deps.json').read_bytes()
print('staged')
'''
r=subprocess.run(ssh+['python3 -c '+shlex.quote(remote)],input=json.dumps(stage),capture_output=True,text=True,timeout=30)
if r.returncode:raise SystemExit(r.stderr[:1000])
stage['ArtifactsVerified']=True;(E/'ApiSignosStageQA.json').write_text(json.dumps(stage,indent=2)+'\n');print(json.dumps(stage))
