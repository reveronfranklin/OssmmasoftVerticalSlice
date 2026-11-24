#!/usr/bin/env python3
"""Coordinate the approved Oracle/API sign adapter in QA; temporary validation always rolls back."""
import argparse,ast,hashlib,json,re,shlex,subprocess
from pathlib import Path
ROOT=Path(__file__).resolve().parents[4];E=ROOT/'Requerimientos/58 - Revision Compronbantes Automaticos/Evidencias';SQL=ROOT/'OssmmasoftVerticalSlice/Features/Contabilidad/Sql'
parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--conversion-aprobada',action='store_true',required=True);parser.parse_args()
stage=json.loads((E/'ApiSignosStageQA.json').read_text());assert stage['ArtifactsVerified']
patches=[]
restored_sources=json.loads((E/'FuentesRestauradasSignosQA.json').read_text())
for name in ['SP_CNT_AUT_CONF','SP_CNT_CMP_GET_ALL','SP_CNT_CMP_GET_ID']:
 text=(SQL/(name+'.sql')).read_text().strip();assert text.endswith('/');text=text[:-1].strip()
 raw=(E/'EscritoresCNT'/(name+'.PROCEDURE.txt')).read_text()
 restored=restored_sources[name]
 # Oracle re-emits extra spaces before the procedure name after CREATE OR REPLACE.
 canonical=lambda value:re.sub(r'\APROCEDURE[ \t]+', 'PROCEDURE ',value,count=1)
 assert canonical(restored)==canonical(raw), 'Restoration changed more than header whitespace: '+name
 raw=restored
 patches.append({'Name':name,'Sql':text,'Sha256':hashlib.sha256(text.encode()).hexdigest(),'BeforeSha256':hashlib.sha256(raw.encode()).hexdigest()})
support=[]
for name,kind in [('SP_CNT_CMP_INS','PROCEDURE'),('SP_CNT_DET_INS','PROCEDURE'),('SP_CNT_DET_GET_CMP','PROCEDURE'),('SP_CNT_RPT_MAY_ANA','PROCEDURE'),('CNT_CMP_GUARD','PACKAGE BODY'),('CNT_CMP_WRITE_TRG','TRIGGER'),('CNT_DET_WRITE_TRG','TRIGGER')]:
 p=E/'EscritoresCNT'/(name+'.'+kind.replace(' ','_')+'.txt');text=p.read_text();assert not re.search(r'\b(COMMIT|AUTONOMOUS_TRANSACTION)\b',text,re.I);support.append({'Name':name,'Type':kind,'Sha256':hashlib.sha256(text.encode()).hexdigest()})
legacy=json.loads((E/'ContrasteFuentesMayorQA.json').read_text())['CommitmentComparison'];assert len({x['Header'] for x in legacy})==1
payload={'Stage':stage,'Patches':patches,'Support':support,'LegacyHeader':int(legacy[0]['Header']),'BackupDirectory':'/srv/ossmmasoft-pruebas/shared/req58/signos-'+Path(stage['NewRelease']).name}
# Reuse audited read-only helpers as text, without importing the executing installer.
tree=ast.parse((Path(__file__).parent/'instalar_y_validar_qa.py').read_text());old=next(x.value.value for x in tree.body if isinstance(x,ast.Assign) and isinstance(x.targets[0],ast.Name) and x.targets[0].id=='program')
helpers=old[old.index('string sha('):old.index('async Task restore()')]
program=r'''
using System.Data;using System.Globalization;using System.Reflection;using System.Security.Cryptography;using System.Text;using System.Text.Json;using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;using Oracle.ManagedDataAccess.Client;using OssmmasoftVerticalSlice.ContextDB;using OssmmasoftVerticalSlice.Features.Contabilidad;
var input=JsonDocument.Parse(await File.ReadAllTextAsync(args[0])).RootElement;OracleConnection? cn=null;string phase="open";bool changed=false;var before=new Dictionary<string,string>();var backup=input.GetProperty("BackupDirectory").GetString()!;
'''+helpers+r'''
async Task<string> sourceOf(string name,string kind){using var c=cmd("SELECT TEXT FROM ALL_SOURCE WHERE OWNER='CNT' AND NAME=:name AND TYPE=:kind ORDER BY LINE");c.Parameters.Add("name",OracleDbType.Varchar2).Value=name;c.Parameters.Add("kind",OracleDbType.Varchar2).Value=kind;using var r=await c.ExecuteReaderAsync();var b=new StringBuilder();while(await r.ReadAsync())if(!r.IsDBNull(0))b.Append(r.GetString(0));return b.ToString();}
OracleCommand sp(string name,OracleTransaction tx){var c=cmd(name);c.CommandType=CommandType.StoredProcedure;c.Transaction=tx;return c;}
void add(OracleCommand c,string name,OracleDbType type,object? value){c.Parameters.Add(name,type).Value=value??DBNull.Value;}
OracleParameter message(OracleCommand c)=>c.Parameters.Add("p_Message",OracleDbType.Varchar2,4000,null,ParameterDirection.Output);
bool ok(OracleParameter p)=>new[]{"success","suscces"}.Contains((Convert.ToString(p.Value)??"").ToLowerInvariant());
async Task restore(){foreach(var item in input.GetProperty("Patches").EnumerateArray()){var name=item.GetProperty("Name").GetString()!;var raw=await File.ReadAllTextAsync(Path.Combine(backup,name+".txt"));if(sha(raw)!=item.GetProperty("BeforeSha256").GetString())throw new Exception("Restore hash mismatch");var ddlText=Regex.Replace(raw,@"^(\s*PROCEDURE\s+)("+name+@")(\s*\()",m=>"CREATE OR REPLACE "+m.Groups[1].Value+"CNT."+m.Groups[2].Value+m.Groups[3].Value);await ddl(ddlText);if(await status(name)!="VALID")throw new Exception("Restore invalid");}}
var assembly=typeof(CntMoney).Assembly;var detailMapper=assembly.GetType("OssmmasoftVerticalSlice.Features.Contabilidad.CntDb")!.GetMethod("MapDetalle",BindingFlags.Public|BindingFlags.Static)!;
var parameterMapper=assembly.GetType("OssmmasoftVerticalSlice.Features.Contabilidad.CntDetalleWriteSupport")!.GetMethod("AddDetailParams",BindingFlags.Public|BindingFlags.Static)!;
async Task<List<CntDetalleResponse>> details(int id,OracleTransaction tx){using var c=sp("CNT.SP_CNT_DET_GET_CMP",tx);add(c,"p_CODIGO_COMPROBANTE",OracleDbType.Int32,id);add(c,"p_CODIGO_EMPRESA",OracleDbType.Int32,13);c.Parameters.Add("p_ResultSet",OracleDbType.RefCursor).Direction=ParameterDirection.Output;var m=message(c);var list=new List<CntDetalleResponse>();using(var r=await c.ExecuteReaderAsync()){while(await r.ReadAsync())list.Add((CntDetalleResponse)detailMapper.Invoke(null,[r])!);}if(!ok(m))throw new Exception("Detail read failed");return list;}
try{
 var cfg=new ConfigurationBuilder().AddJsonFile("/srv/ossmmasoft-pruebas/shared/config/vertical.json").Build();if(cfg.GetValue<int>("settings:EmpresaConfig")!=13)throw new Exception("Company mismatch");cn=new ConnectionDB(cfg).GetCntConnection();await cn.OpenAsync();
 using(var c=cmd("SELECT SYS_CONTEXT('USERENV','DB_NAME'),SYS_CONTEXT('USERENV','SERVER_HOST'),SYS_CONTEXT('USERENV','SESSION_USER') FROM DUAL")){using var r=await c.ExecuteReaderAsync();await r.ReadAsync();if(r.GetString(0)!="XE"||r.GetString(1)!="6d88ba053190"||r.GetString(2)!="CNT")throw new Exception("Identity mismatch");}
 if(input.GetProperty("Mode").GetString()=="restore"){await restore();Console.WriteLine("R58_RESULT="+JsonSerializer.Serialize(new{Restored=true}));return;}
 phase="source-preflight";
 foreach(var item in input.GetProperty("Patches").EnumerateArray()){var name=item.GetProperty("Name").GetString()!;var raw=await source(name);if(sha(raw)!=item.GetProperty("BeforeSha256").GetString())throw new Exception("Baseline changed:"+name);before[name]=raw;}
 foreach(var item in input.GetProperty("Support").EnumerateArray())if(sha(await sourceOf(item.GetProperty("Name").GetString()!,item.GetProperty("Type").GetString()!))!=item.GetProperty("Sha256").GetString())throw new Exception("Supporting source changed");
 var fingerprintBefore=await fingerprint();Directory.CreateDirectory(backup);File.SetUnixFileMode(backup,UnixFileMode.UserRead|UnixFileMode.UserWrite|UnixFileMode.UserExecute);foreach(var pair in before){var file=Path.Combine(backup,pair.Key+".txt");await File.WriteAllTextAsync(file,pair.Value);File.SetUnixFileMode(file,UnixFileMode.UserRead|UnixFileMode.UserWrite);}
 using(var c=cmd("ROLLBACK")){await c.ExecuteNonQueryAsync();}
 foreach(var item in input.GetProperty("Patches").EnumerateArray()){var name=item.GetProperty("Name").GetString()!;phase="ddl-"+name;var sql=item.GetProperty("Sql").GetString()!;if(sha(sql)!=item.GetProperty("Sha256").GetString())throw new Exception("Payload hash mismatch");changed=true;await ddl(sql);if(await status(name)!="VALID")throw new Exception("Compilation invalid");}
 phase="controlled-transaction";int temporaryId=0;var tag="R58-"+Guid.NewGuid().ToString("N")[..12];int legacyRows=0;decimal headerDebe=0,headerHaber=0,preDebe=0,preCreditSigned=0;var ledgerRows=0;var storedRows=new List<object>();
 using(var tx=cn.BeginTransaction()){
 try{
  int debitMajor=0,creditMajor=0,debitAux=0,creditAux=0;
  using(var c=cmd(@"SELECT DM,CM,DA,CA FROM (SELECT D.CODIGO_MAYOR DM,C.CODIGO_MAYOR CM,A.CODIGO_AUXILIAR DA,B.CODIGO_AUXILIAR CA FROM CNT.CNT_MAYORES D JOIN CNT.CNT_MAYORES C ON C.CODIGO_EMPRESA=D.CODIGO_EMPRESA JOIN CNT.CNT_AUXILIARES A ON A.CODIGO_MAYOR=D.CODIGO_MAYOR AND A.CODIGO_EMPRESA=D.CODIGO_EMPRESA JOIN CNT.CNT_AUXILIARES B ON B.CODIGO_MAYOR=C.CODIGO_MAYOR AND B.CODIGO_EMPRESA=C.CODIGO_EMPRESA AND B.CODIGO_PROVEEDOR=A.CODIGO_PROVEEDOR WHERE D.CODIGO_EMPRESA=13 AND TRIM(D.NUMERO_MAYOR)='300' AND TRIM(C.NUMERO_MAYOR)='103' AND (A.FECHA_FIN_VIGENCIA IS NULL OR A.FECHA_FIN_VIGENCIA>=TRUNC(SYSDATE)) AND (B.FECHA_FIN_VIGENCIA IS NULL OR B.FECHA_FIN_VIGENCIA>=TRUNC(SYSDATE)) ORDER BY A.CODIGO_AUXILIAR,B.CODIGO_AUXILIAR) WHERE ROWNUM=1")){c.Transaction=tx;using var r=await c.ExecuteReaderAsync();if(!await r.ReadAsync())throw new Exception("No active auxiliary pair");debitMajor=r.GetInt32(0);creditMajor=r.GetInt32(1);debitAux=r.GetInt32(2);creditAux=r.GetInt32(3);}
  using(var c=sp("CNT.SP_CNT_CMP_INS",tx)){add(c,"p_CODIGO_PERIODO",OracleDbType.Int32,198);add(c,"p_TIPO_COMPROBANTE_ID",OracleDbType.Int32,18);add(c,"p_FECHA_COMPROBANTE",OracleDbType.Date,new DateTime(2022,7,4));add(c,"p_ORIGEN_ID",OracleDbType.Int32,1);add(c,"p_OBSERVACION",OracleDbType.Varchar2,tag);add(c,"p_USUARIO_ID",OracleDbType.Int32,0);add(c,"p_CODIGO_EMPRESA",OracleDbType.Int32,13);var id=c.Parameters.Add("p_CODIGO_OUT",OracleDbType.Int32,ParameterDirection.Output);var m=message(c);add(c,"p_PERMITIR_AUTOMATICO",OracleDbType.Int32,0);await c.ExecuteNonQueryAsync();if(!ok(m))throw new Exception("Temporary header failed:"+Convert.ToString(m.Value));temporaryId=((Oracle.ManagedDataAccess.Types.OracleDecimal)id.Value).ToInt32();}
  foreach(var x in new[]{new CntDetalleCreateCommand(debitMajor,debitAux,tag,"D",null,tag,-125.35m),new CntDetalleCreateCommand(creditMajor,creditAux,tag,"H",null,tag,125.35m)}){
   using var c=sp("CNT.SP_CNT_DET_INS",tx);add(c,"p_CODIGO_COMPROBANTE",OracleDbType.Int32,temporaryId);parameterMapper.Invoke(null,[c,x]);add(c,"p_USUARIO_ID",OracleDbType.Int32,0);add(c,"p_CODIGO_EMPRESA",OracleDbType.Int32,13);c.Parameters.Add("p_CODIGO_OUT",OracleDbType.Int32).Direction=ParameterDirection.Output;var m=message(c);add(c,"p_PERMITIR_AUTOMATICO",OracleDbType.Int32,0);await c.ExecuteNonQueryAsync();if(!ok(m))throw new Exception("Temporary detail failed:"+Convert.ToString(m.Value));
  }
  var translated=await details(temporaryId,tx);if(translated.Count!=2||translated.Single(x=>x.CodigoMayor==debitMajor).Debe!=125.35m||translated.Single(x=>x.CodigoMayor==creditMajor).Haber!=125.35m)throw new Exception("Detail sides differ");
  using(var c=cmd("SELECT CODIGO_MAYOR,MONTO FROM CNT.CNT_DETALLE_COMPROBANTE WHERE CODIGO_COMPROBANTE=:id AND CODIGO_EMPRESA=13 ORDER BY CODIGO_MAYOR")){c.Transaction=tx;add(c,"id",OracleDbType.Int32,temporaryId);using var r=await c.ExecuteReaderAsync();while(await r.ReadAsync()){var major=r.GetInt32(0);var amount=r.GetDecimal(1);if(amount!=(major==debitMajor?125.35m:-125.35m))throw new Exception("Stored sign wrong");storedRows.Add(new{Major=major,Monto=amount});}}
  using(var c=sp("CNT.SP_CNT_CMP_GET_ID",tx)){add(c,"p_CODIGO_COMPROBANTE",OracleDbType.Int32,temporaryId);add(c,"p_CODIGO_EMPRESA",OracleDbType.Int32,13);c.Parameters.Add("p_ResultSet",OracleDbType.RefCursor).Direction=ParameterDirection.Output;var m=message(c);using(var r=await c.ExecuteReaderAsync()){if(!await r.ReadAsync())throw new Exception("Header not read");headerDebe=r.GetDecimal(r.GetOrdinal("TOTAL_DEBE"));headerHaber=r.GetDecimal(r.GetOrdinal("TOTAL_HABER"));}if(!ok(m)||headerDebe!=125.35m||headerHaber!=125.35m)throw new Exception("Header total mismatch");}
  using(var c=sp("CNT.SP_CNT_RPT_MAY_ANA",tx)){add(c,"p_PAGE_SIZE",OracleDbType.Int32,100);add(c,"p_PAGE_NUMBER",OracleDbType.Int32,1);add(c,"p_SEARCH_TEXT",OracleDbType.Varchar2,tag);add(c,"p_CODIGO_PERIODO",OracleDbType.Int32,198);add(c,"p_CODIGO_MAYOR",OracleDbType.Int32,null);add(c,"p_CODIGO_AUXILIAR",OracleDbType.Int32,null);add(c,"p_FECHA_DESDE",OracleDbType.Date,new DateTime(2022,7,4));add(c,"p_FECHA_HASTA",OracleDbType.Date,new DateTime(2022,7,4));add(c,"p_CODIGO_EMPRESA",OracleDbType.Int32,13);c.Parameters.Add("p_ResultSet",OracleDbType.RefCursor).Direction=ParameterDirection.Output;var m=message(c);c.Parameters.Add("p_TotalRecords",OracleDbType.Int32).Direction=ParameterDirection.Output;c.Parameters.Add("p_TotalPages",OracleDbType.Int32).Direction=ParameterDirection.Output;
   var mapper=typeof(GetCntMayorAnaliticoHandler).GetMethod("MapMayorAnalitico",BindingFlags.NonPublic|BindingFlags.Static)!;using(var r=await c.ExecuteReaderAsync()){while(await r.ReadAsync()){var dto=(CntMayorAnaliticoResponse)mapper.Invoke(null,[r])!;if(dto.CodigoComprobante!=temporaryId||(dto.CodigoMayor==debitMajor?dto.Debe!=125.35m:dto.Haber!=125.35m))throw new Exception("Ledger side mismatch");ledgerRows++;}}if(!ok(m)||ledgerRows!=2)throw new Exception("Ledger rows differ");}
  using(var c=cmd("SELECT SUM(CASE WHEN MONTO>0 THEN MONTO ELSE 0 END),SUM(CASE WHEN MONTO<0 THEN MONTO ELSE 0 END) FROM CNT.CNT_DETALLE_COMPROBANTE WHERE CODIGO_COMPROBANTE=:id AND CODIGO_EMPRESA=13")){c.Transaction=tx;add(c,"id",OracleDbType.Int32,temporaryId);using var r=await c.ExecuteReaderAsync();await r.ReadAsync();preDebe=r.GetDecimal(0);preCreditSigned=r.GetDecimal(1);if(preDebe!=125.35m||preCreditSigned!=-125.35m)throw new Exception("Preclose criterion mismatch");}
  var existing=await details(input.GetProperty("LegacyHeader").GetInt32(),tx);legacyRows=existing.Count;if(legacyRows!=26||existing.Any(x=>x.CodigoMayor==9?x.Debe<=0||x.Haber!=0:x.CodigoMayor==3?x.Haber<=0||x.Debe!=0:true))throw new Exception("Legacy direction mismatch");
 }finally{tx.Rollback();}}
 phase="postflight";using(var c=cmd("SELECT COUNT(*) FROM CNT.CNT_COMPROBANTES WHERE CODIGO_COMPROBANTE=:id AND CODIGO_EMPRESA=13")){add(c,"id",OracleDbType.Int32,temporaryId);if(Convert.ToInt32(await c.ExecuteScalarAsync())!=0)throw new Exception("Temporary header survived rollback");}
 var fingerprintAfter=await fingerprint();if(fingerprintBefore!=fingerprintAfter)throw new Exception("Financial snapshot changed");
 Console.WriteLine("R58_RESULT="+JsonSerializer.Serialize(new{Applied=true,Company=13,SqlObjectsValid=3,BackupDirectory=backup,TemporaryHeaderId=temporaryId,TemporaryRows=storedRows,TemporaryTransactionRolledBack=true,TemporaryHeaderAbsent=true,HeaderDebe=headerDebe,HeaderHaber=headerHaber,LedgerRows=ledgerRows,PrecloseCriteriaDebit=preDebe,PrecloseCriteriaSignedCredit=preCreditSigned,PrecloseExecuted=false,AutomaticConfirmationExecuted=false,LegacyLinesMappedCorrectly=legacyRows,FinancialFingerprintBefore=fingerprintBefore,FinancialFingerprintAfter=fingerprintAfter,HistoricalDataUnchanged=true,SequencesMayAdvance=true,ApiFormatPreserved=true}));
}catch(Exception ex){var failPhase=phase;var failure=ex is OracleException oe?"ORA-"+oe.Number:ex.Message;var restored=false;if(changed){try{await restore();restored=true;}catch(Exception re){failure+="; restore "+re.GetType().Name;}}
 Console.WriteLine("R58_RESULT="+JsonSerializer.Serialize(new{Applied=false,FailedPhase=failPhase,Failure=failure,SqlRestored=restored,SqlChangeStarted=changed,BackupDirectory=backup}));Environment.ExitCode=1;}finally{cn?.Dispose();}
'''
payload['Mode']='apply'
remote=r'''import pathlib,subprocess,sys,json,tempfile,shutil,os,hashlib,time,urllib.request,signal
x=json.load(sys.stdin);stage=x['payload']['Stage'];base=pathlib.Path('/srv/ossmmasoft-pruebas');current=base/'current/vertical';old=pathlib.Path(stage['OldRelease']);new=pathlib.Path(stage['NewRelease']);assert current.resolve()==old
for name,want in stage['Files'].items():assert hashlib.sha256((new/name).read_bytes()).hexdigest()==want
assert hashlib.sha256((old/'OssmmasoftVerticalSlice.dll').read_bytes()).hexdigest()==stage['OldDllSha256']
config=base/'shared/config/vertical.json';config_before=hashlib.sha256(config.read_bytes()).hexdigest()
p=pathlib.Path(tempfile.mkdtemp(prefix='r58-signos-coordinated-'));stopped=False;db=None;switched=False
try:
 refs=''.join('<Reference Include="'+name+'"><HintPath>'+str(new/(name+'.dll'))+'</HintPath></Reference>' for name in ['OssmmasoftVerticalSlice','Oracle.ManagedDataAccess','Microsoft.Data.SqlClient'])
 (p/'Probe.csproj').write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable></PropertyGroup><ItemGroup><FrameworkReference Include="Microsoft.AspNetCore.App"/>'+refs+'</ItemGroup></Project>')
 (p/'Program.cs').write_text(x['program']);(p/'payload.json').write_text(json.dumps(x['payload']))
 env={**os.environ,'TZ':'America/Caracas'};build=subprocess.run(['dotnet','build',str(p/'Probe.csproj'),'--verbosity','quiet'],capture_output=True,text=True,timeout=120,env=env)
 if build.returncode:raise RuntimeError('\n'.join(line for line in build.stdout.splitlines() if ': error CS' in line))
 assert current.resolve()==old
 subprocess.run(['systemctl','--user','stop','ossmmasoft-vertical.service'],check=True);stopped=True
 def execute():
  process=subprocess.Popen(['dotnet',str(p/'bin/Debug/net10.0/Probe.dll'),str(p/'payload.json')],stdout=subprocess.PIPE,stderr=subprocess.PIPE,text=True,env=env,start_new_session=True)
  try:out,err=process.communicate(timeout=120)
  except subprocess.TimeoutExpired:
   os.killpg(process.pid,signal.SIGKILL);process.communicate();raise RuntimeError('DB worker timeout; restore required from backup')
  lines=[line for line in out.splitlines() if line.startswith('R58_RESULT=')]
  if not lines:raise RuntimeError('DB worker returned no receipt')
  return json.loads(lines[-1].split('=',1)[1])
 db=execute()
 if not db.get('Applied'):raise RuntimeError('Database validation failed')
 assert current.resolve()==old
 temp=base/'current'/('.vertical-r58-'+str(os.getpid()));os.symlink(new,temp);os.replace(temp,current);switched=True
 subprocess.run(['systemctl','--user','start','ossmmasoft-vertical.service'],check=True)
 for _ in range(30):
  try:
   if urllib.request.urlopen('http://127.0.0.1:5101/health',timeout=2).status==200:break
  except Exception:time.sleep(1)
 else:raise RuntimeError('New API health timeout')
 assert hashlib.sha256(config.read_bytes()).hexdigest()==config_before
 print('R58_RESULT='+json.dumps({'Published':True,'NewRelease':str(new),'OldRelease':str(old),'ApiHealth':200,'ConfigUnchanged':True,'Database':db,'DllSha256':stage['Files']['OssmmasoftVerticalSlice.dll'],'FrontendReleaseChanged':False}))
except Exception as ex:
 restore=None
 if stopped:
  backup=pathlib.Path(x['payload']['BackupDirectory'])
  needs_restore=(db and (db.get('Applied') or db.get('SqlChangeStarted') and not db.get('SqlRestored'))) or (db is None and all((backup/(z['Name']+'.txt')).exists() for z in x['payload']['Patches']))
  if needs_restore:
   try:
    x['payload']['Mode']='restore';(p/'payload.json').write_text(json.dumps(x['payload']));restore=execute()
   except Exception as err:restore={'Restored':False,'Failure':str(err)}
  if switched and current.resolve()==new:
   temp=base/'current'/('.vertical-r58-restore-'+str(os.getpid()));os.symlink(old,temp);os.replace(temp,current)
  subprocess.run(['systemctl','--user','start','ossmmasoft-vertical.service'],check=False)
 print('R58_RESULT='+json.dumps({'Published':False,'Failure':str(ex),'Database':db,'Restoration':restore,'ApiRestartAttempted':stopped}));sys.exit(1)
finally:shutil.rmtree(p)
'''
ssh=['ssh','-i',str(Path.home()/'.ssh/ubuntu_pruebas'),'-o','BatchMode=yes','-o','ConnectTimeout=10','ossmma@74.121.190.6']
r=subprocess.run(ssh+['python3 -c '+shlex.quote(remote)],input=json.dumps({'program':program,'payload':payload}),capture_output=True,text=True,timeout=300)
if 'R58_RESULT=' not in r.stdout:raise SystemExit(r.stdout[:1800] or r.stderr[:1400])
d=json.loads(r.stdout.split('R58_RESULT=')[-1]);d['LocalSqlHashes']={x['Name']:x['Sha256'] for x in patches};(E/'PublicacionConversionSignosQA.json').write_text(json.dumps(d,indent=2)+'\n')
print(json.dumps({k:v for k,v in d.items() if k not in ['Database','LocalSqlHashes']}));print(json.dumps(d.get('Database')))
raise SystemExit(r.returncode)
