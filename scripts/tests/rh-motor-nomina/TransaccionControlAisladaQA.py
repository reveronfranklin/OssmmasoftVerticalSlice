"""Exercise production transaction primitives in an isolated, disposable RH namespace."""
import hashlib,json,re,shlex,subprocess,uuid
from pathlib import Path
base=Path(__file__).resolve().parents[3]
root=base/'Features/RhMotorNomina'
prefix='Q57T'+uuid.uuid4().hex[:6].upper()+'_'
files=['Sql/06_RH_NOM_FIN_PLAN.sql','Sql/07_RH_NOM_FIN_EVT.sql','Sql/08_RH_NOM_FIN_TX.sql','Sql/SP_RH_NOM_MOV_PLAN.sql']
original={name:(root/name).read_text() for name in files}
pattern=r'\b(?:RH_[A-Z0-9_]+|TR_RH_[A-Z0-9_]+|(?:PK|UK|FK|CK|IX)_RH_[A-Z0-9_]+|SP_RH_NOM_MOV_PLAN)\b'
names=set(re.findall(pattern,'\n'.join(original.values())))
names.update(['RH_MOV_NOMINA_U1','RH_MOV_NOMINA_U2'])
mapping={n:prefix+hashlib.sha256(n.encode()).hexdigest()[:8].upper() for n in sorted(names)}
assert len(set(mapping.values()))==len(mapping) and all(len(n)<=30 for n in mapping.values())
token=re.compile(r'\b(?:'+ '|'.join(map(re.escape,sorted(names,key=len,reverse=True)))+r')\b')
reverse=re.compile(r'\b(?:'+'|'.join(map(re.escape,mapping.values()))+r')\b')
inverse={v:k for k,v in mapping.items()}
sql={n:token.sub(lambda m:mapping[m[0]],text) for n,text in original.items()}
assert all(reverse.sub(lambda m:inverse[m[0]],sql[n])==text for n,text in original.items())
# No production table/package identifier may survive in executable cloned SQL.
assert not any(re.search(pattern,text) for text in sql.values())
clones={}
class_map={'RhNomTransaccionDb':'ShadowTxDb','RhNomLibroFinancieroDb':'ShadowBookDb','RhNomMovimientosPlanDb':'ShadowMovDb','RhNomPlanesGuardadosDb':'ShadowPlanDb','RhNomAplicacionHandler':'ShadowApplicationHandler'}
classes=re.compile(r'\b(?:'+'|'.join(class_map)+r')\b')
back_classes={v:k for k,v in class_map.items()}
back_pattern=re.compile(r'\b(?:'+'|'.join(back_classes)+r')\b')
helper_files=['RhNomTransaccionDb.cs','RhNomLibroFinancieroDb.cs','RhNomPlanFinancieroDb.cs','RhNomPlanesGuardados.cs','RhNomAplicacion.cs']
for name,old,stop in [
 ('RhNomTransaccionDb.cs','RhNomTransaccionDb',None),
 ('RhNomLibroFinancieroDb.cs','RhNomLibroFinancieroDb','public sealed partial class RhNominaController'),
 ('RhNomPlanFinancieroDb.cs','RhNomMovimientosPlanDb','public sealed class RhNomDiagnosticoFinancieroHandler'),
 ('RhNomPlanesGuardados.cs','RhNomPlanesGuardadosDb','public sealed class RhNomPlanesGuardadosHandler'),
 ('RhNomAplicacion.cs','RhNomAplicacionHandler','public sealed partial class RhNominaController')]:
 text=(root/name).read_text()
 if stop:text=text[:text.index(stop)]
 if name in ['RhNomPlanFinancieroDb.cs','RhNomPlanesGuardados.cs']:
  text=text[:text.index('public record')]+text[text.index('public static class '+old):]
 before=text
 text=classes.sub(lambda m:class_map[m[0]],text)
 text=token.sub(lambda m:mapping[m[0]],text)
 recovered=back_pattern.sub(lambda m:back_classes[m[0]],reverse.sub(lambda m:inverse[m[0]],text))
 assert recovered==before
 clones[class_map[old]+'.cs']=text
candidate=base.parent/'output/r57-tx-control-qa'
version=(candidate/'version.txt').read_text().strip()
dll_hash=hashlib.sha256(Path('/tmp/r57-tx-control-api/OssmmasoftVerticalSlice.dll').read_bytes()).hexdigest()
manifest={'apiDllPath':'/srv/ossmmasoft-pruebas/staging/'+version+'/vertical-runtime/OssmmasoftVerticalSlice.dll','prefix':prefix,'map':mapping,'originalSqlHashes':{n:hashlib.sha256(t.encode()).hexdigest() for n,t in original.items()},'originalHelperHashes':{n:hashlib.sha256((root/n).read_bytes()).hexdigest() for n in helper_files},'renamingOnlyVerified':True,'apiDllHash':dll_hash}
output_root=base.parent/'output/r57-transaccion-control-aislada-qa'
out=output_root/prefix;out.mkdir(parents=True,exist_ok=True)
manifest.update({'testProgramSha256':hashlib.sha256((base/'scripts/tests/rh-motor-nomina/TransaccionControlAisladaQA.cs.txt').read_bytes()).hexdigest(),'runnerSha256':hashlib.sha256(Path(__file__).read_bytes()).hexdigest(),'clonedSqlHashes':{n:hashlib.sha256(t.encode()).hexdigest() for n,t in sql.items()},'clonedHelperHashes':{n:hashlib.sha256(t.encode()).hexdigest() for n,t in clones.items()}})
(out/'manifest.json').write_text(json.dumps(manifest,indent=2)+'\n')
(out/'schema.sql').write_text('\n'.join(sql.values()))
subprocess.run(['bash',str(base/'scripts/check-oracle-identifiers.sh'),str(out/'schema.sql')],check=True,capture_output=True,text=True)
payload={'manifest':manifest,'program':(base/'scripts/tests/rh-motor-nomina/TransaccionControlAisladaQA.cs.txt').read_text(),'sql':sql,'clones':clones}
remote='''import tempfile,pathlib,subprocess,json,shutil,sys,os,hashlib
p=pathlib.Path(tempfile.mkdtemp(prefix="r57-shadow-"))
try:
 d=json.load(sys.stdin)
 dll=pathlib.Path(d['manifest']['apiDllPath'])
 assert hashlib.sha256(dll.read_bytes()).hexdigest()==d['manifest']['apiDllHash']
 (p/'Probe.csproj').write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable></PropertyGroup><ItemGroup><FrameworkReference Include="Microsoft.AspNetCore.App"/><Reference Include="OssmmasoftVerticalSlice"><HintPath>'+str(dll)+'</HintPath></Reference><Reference Include="Microsoft.Data.SqlClient"><HintPath>/srv/ossmmasoft-pruebas/current/vertical/Microsoft.Data.SqlClient.dll</HintPath></Reference><Reference Include="Oracle.ManagedDataAccess"><HintPath>/srv/ossmmasoft-pruebas/current/vertical/Oracle.ManagedDataAccess.dll</HintPath></Reference></ItemGroup></Project>')
 (p/'Program.cs').write_text(d['program']);(p/'bundle.json').write_text(json.dumps(d))
 (p/'GlobalUsings.cs').write_text('global using Microsoft.Extensions.Configuration;')
 for n,t in d['clones'].items():(p/n).write_text(t)
 r=subprocess.run(['dotnet','run','--project',str(p/'Probe.csproj'),'--verbosity','quiet','--',str(p/'bundle.json')],capture_output=True,text=True,timeout=600,env={**os.environ,'TZ':'America/Caracas'})
 lines=[x for x in r.stdout.splitlines() if x.startswith('R57_')]
 print('\\n'.join(lines) if lines else 'R57_RUNNER_FAILED='+str(r.returncode))
 if not lines:print('\\n'.join(x for x in r.stdout.splitlines() if 'error CS' in x)[:5000])
 sys.exit(r.returncode)
finally:shutil.rmtree(p)
'''
r=subprocess.run(['ssh','-i',str(Path.home()/'.ssh/ubuntu_pruebas'),'-o','BatchMode=yes','-o','ConnectTimeout=10','ossmma@74.121.190.6','python3 -c '+shlex.quote(remote)],input=json.dumps(payload),capture_output=True,text=True,timeout=660)
(out/'runner-summary.txt').write_text(r.stdout)
if r.returncode or 'R57_SHADOW_CONTROL=' not in r.stdout:raise SystemExit('No se completo QA aislado: '+r.stdout[:6000])
result=json.loads(r.stdout.split('R57_SHADOW_CONTROL=')[-1].strip());(out/'result.json').write_text(json.dumps(result,indent=2)+'\n')
assert result['Prefix']==manifest['prefix']
for name in ['manifest.json','result.json','schema.sql']:(output_root/name).write_text((out/name).read_text())
(output_root/'latest.json').write_text(json.dumps({'RunDirectory':prefix,'Status':result['Status']},indent=2)+'\n')
print(json.dumps({'Status':result['Status'],'Checks':len(result['Checks']),'ShadowEvents':result['ShadowEvents'],'Cleaned':result['Cleaned'],'RealPayrollWrites':False}))
