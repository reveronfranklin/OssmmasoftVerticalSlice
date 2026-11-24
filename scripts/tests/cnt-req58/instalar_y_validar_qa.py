#!/usr/bin/env python3
"""Authorized R58 QA procedure installation and rolled-back previews. Never call CONF."""
import argparse,hashlib,json,re,shlex,subprocess
from pathlib import Path
ROOT=Path(__file__).resolve().parents[4]
SQL=ROOT/'OssmmasoftVerticalSlice/Features/Contabilidad/Sql'
E=ROOT/'Requerimientos/58 - Revision Compronbantes Automaticos/Evidencias'
parser=argparse.ArgumentParser(description=__doc__)
parser.add_argument('--instalacion-qa-autorizada',action='store_true',required=True)
parser.parse_args()
names=['SP_CNT_AUT_PRES_GET','SP_CNT_AUT_BUILD_OP','SP_CNT_AUT_BUILD_COMP02','SP_CNT_AUT_BUILD_COMP','SP_CNT_AUT_BUILD_CH_RET','SP_CNT_AUT_PREV','SP_CNT_AUT_CONF']
baseline=json.loads((E/'ManifiestoRestauracionBase.json').read_text())['BaselineObjects']
items=[]
for name in names:
 text=(SQL/(name+'.sql')).read_text();start=text.index('CREATE OR REPLACE PROCEDURE CNT.'+name+' (');ddl=text[start:].strip();assert ddl.endswith('/');ddl=ddl[:-1].rstrip()
 assert ddl.upper().count('CREATE OR REPLACE PROCEDURE')==1
 if name!='SP_CNT_AUT_CONF':assert not re.search(r'\b(COMMIT|ROLLBACK|EXECUTE IMMEDIATE)\b',ddl,re.I)
 items.append({'Name':name,'Sql':ddl,'Sha256':hashlib.sha256(ddl.encode()).hexdigest()})
comparisons=json.loads((E/'EvaluacionComp02FuentesQA.json').read_text())['Comparisons']
assert len(comparisons)==13 and all(x['Passed'] for x in comparisons)
payload={'Procedures':items,'Baseline':baseline,'Cases':[dict(x,Period=198 if x['Date'].startswith('2022-07') else 204) for x in comparisons]}
program=r'''
using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Oracle.ManagedDataAccess.Client;
using OssmmasoftVerticalSlice.ContextDB;
var input=JsonDocument.Parse(await File.ReadAllTextAsync(args[0])).RootElement;
string phase="configuration",backupDir="";bool started=false,restored=false;OracleConnection? cn=null;
var before=new Dictionary<string,string>();var compile=new List<object>();var previews=new List<object>();var dependencies=new List<object>();var hashes=new List<object>();
string sha(string s)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(s))).ToLowerInvariant();
OracleCommand cmd(string sql){var c=cn!.CreateCommand();c.BindByName=true;c.CommandTimeout=30;c.CommandText=sql;return c;}
async Task<string> source(string name){using var c=cmd("SELECT TEXT FROM ALL_SOURCE WHERE OWNER='CNT' AND TYPE='PROCEDURE' AND NAME=:name ORDER BY LINE");c.Parameters.Add("name",OracleDbType.Varchar2).Value=name;using var r=await c.ExecuteReaderAsync();var s=new StringBuilder();while(await r.ReadAsync())s.Append(r.GetString(0));return s.ToString();}
async Task<int> objectCount(string name){using var c=cmd("SELECT COUNT(*) FROM ALL_OBJECTS WHERE OWNER='CNT' AND OBJECT_NAME=:name");c.Parameters.Add("name",OracleDbType.Varchar2).Value=name;return Convert.ToInt32(await c.ExecuteScalarAsync());}
async Task<string> status(string name){using var c=cmd("SELECT STATUS FROM ALL_OBJECTS WHERE OWNER='CNT' AND OBJECT_TYPE='PROCEDURE' AND OBJECT_NAME=:name");c.Parameters.Add("name",OracleDbType.Varchar2).Value=name;return Convert.ToString(await c.ExecuteScalarAsync())??"MISSING";}
async Task<string> fingerprint(){using var hash=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);foreach(var sql in new[]{
 "SELECT CODIGO_COMPROBANTE,CODIGO_PERIODO,TIPO_COMPROBANTE_ID,ORIGEN_ID,NUMERO_COMPROBANTE,FECHA_COMPROBANTE,ESTADO_CMP,AJUSTE_MANUAL,VERSION_CMP,OBSERVACION,CODIGO_EMPRESA FROM CNT.CNT_COMPROBANTES WHERE CODIGO_EMPRESA=13 ORDER BY CODIGO_COMPROBANTE",
 "SELECT CODIGO_DETALLE_COMPROBANTE,CODIGO_COMPROBANTE,CODIGO_MAYOR,CODIGO_AUXILIAR,MONTO,REFERENCIA1,REFERENCIA2,REFERENCIA3,DESCRIPCION,CODIGO_EMPRESA FROM CNT.CNT_DETALLE_COMPROBANTE WHERE CODIGO_EMPRESA=13 ORDER BY CODIGO_DETALLE_COMPROBANTE"}){
 using var c=cmd(sql);using var r=await c.ExecuteReaderAsync();while(await r.ReadAsync()){var row=new List<string?>();for(int i=0;i<r.FieldCount;i++){var v=r.IsDBNull(i)?null:r.GetValue(i);row.Add(v is DateTime dt?dt.ToString("O",CultureInfo.InvariantCulture):Convert.ToString(v,CultureInfo.InvariantCulture));}hash.AppendData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(row)+"\n"));}}
 return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();}
async Task ddl(string sql){using var c=cmd(sql);await c.ExecuteNonQueryAsync();}
async Task restore(){foreach(var name in new[]{"SP_CNT_AUT_BUILD_OP","SP_CNT_AUT_BUILD_COMP","SP_CNT_AUT_BUILD_CH_RET","SP_CNT_AUT_PREV","SP_CNT_AUT_CONF"}){
 var raw=before[name];var text=Regex.Replace(raw,@"^\s*PROCEDURE\s+"+name+@"\s*\(","CREATE OR REPLACE PROCEDURE CNT."+name+" (",RegexOptions.IgnoreCase);await ddl(text);if(await status(name)!="VALID")throw new Exception("Restore compilation failed");}
 foreach(var name in new[]{"SP_CNT_AUT_BUILD_COMP02","SP_CNT_AUT_PRES_GET"})if(await objectCount(name)>0)await ddl("DROP PROCEDURE CNT."+name);restored=true;}
async Task<(List<Dictionary<string,string?>> Rows,string Message)> preview(int period,int type,int origin,DateTime day){
 using var tx=cn!.BeginTransaction();
 try{
 using var c=cmd("CNT.SP_CNT_AUT_PREV");c.CommandType=CommandType.StoredProcedure;c.Transaction=tx;
 c.Parameters.Add("p_CODIGO_PERIODO",OracleDbType.Int32).Value=period;c.Parameters.Add("p_TIPO_COMPROBANTE_ID",OracleDbType.Int32).Value=type;c.Parameters.Add("p_ORIGEN_ID",OracleDbType.Int32).Value=origin;c.Parameters.Add("p_FECHA_DESDE",OracleDbType.Date).Value=day;c.Parameters.Add("p_FECHA_HASTA",OracleDbType.Date).Value=day;c.Parameters.Add("p_USUARIO_ID",OracleDbType.Int32).Value=0;c.Parameters.Add("p_CODIGO_EMPRESA",OracleDbType.Int32).Value=13;c.Parameters.Add("p_ResultSet",OracleDbType.RefCursor).Direction=ParameterDirection.Output;var message=c.Parameters.Add("p_Message",OracleDbType.Varchar2,4000,null,ParameterDirection.Output);
 var rows=new List<Dictionary<string,string?>>();using(var r=await c.ExecuteReaderAsync()){while(await r.ReadAsync()){if(rows.Count>=5000)throw new Exception("Preview row bound");var row=new Dictionary<string,string?>();foreach(var col in new[]{"CODIGO_MAYOR","MAYOR","CODIGO_AUXILIAR","REFERENCIA1","MONTO"}){var i=r.GetOrdinal(col);row[col]=r.IsDBNull(i)?null:Convert.ToString(r.GetValue(i),CultureInfo.InvariantCulture);}rows.Add(row);}}
 var msg=Convert.ToString(message.Value)??"";tx.Rollback();return(rows,msg);
 }catch{tx.Rollback();throw;}
}
try{
 var configPath="/srv/ossmmasoft-pruebas/shared/config/vertical.json";var configHash=sha(await File.ReadAllTextAsync(configPath));var cfg=new ConfigurationBuilder().AddJsonFile(configPath).Build();if(cfg.GetValue<int>("settings:EmpresaConfig")!=13)throw new Exception("Company mismatch");
 cn=new ConnectionDB(cfg).GetCntConnection();await cn.OpenAsync();using(var c=cmd("SELECT SYS_CONTEXT('USERENV','DB_NAME'),SYS_CONTEXT('USERENV','SERVER_HOST'),SYS_CONTEXT('USERENV','SESSION_USER') FROM DUAL")){using var r=await c.ExecuteReaderAsync();await r.ReadAsync();if(r.GetString(0)!="XE"||r.GetString(1)!="6d88ba053190"||r.GetString(2)!="CNT")throw new Exception("Identity mismatch");}
 phase="preflight-sources";
 foreach(var item in input.GetProperty("Baseline").EnumerateArray()){var name=item.GetProperty("Name").GetString()!;var raw=await source(name);var actual=sha(raw);hashes.Add(new{Name=name,BeforeSha256=actual});if(actual!=item.GetProperty("CapturedSha256").GetString())throw new Exception("Live baseline differs: "+name);if(await status(name)!="VALID")throw new Exception("Invalid baseline");before[name]=raw;}
 foreach(var name in new[]{"SP_CNT_AUT_PRES_GET","SP_CNT_AUT_BUILD_COMP02"})if(await objectCount(name)!=0)throw new Exception("New object already exists");
 phase="preflight-dependencies";
 using(var c=cmd("SELECT DISTINCT OWNER,NAME,TYPE FROM ALL_DEPENDENCIES WHERE REFERENCED_OWNER='CNT' AND REFERENCED_NAME IN ('SP_CNT_AUT_BUILD_OP','SP_CNT_AUT_BUILD_COMP') ORDER BY OWNER,NAME,TYPE")){using var r=await c.ExecuteReaderAsync();while(await r.ReadAsync()){var owner=r.GetString(0);var name=r.GetString(1);var type=r.GetString(2);dependencies.Add(new{Owner=owner,Name=name,Type=type});if(owner!="CNT"||type!="PROCEDURE"||(name!="SP_CNT_AUT_PREV"&&name!="SP_CNT_AUT_CONF"))throw new Exception("Additional caller requires review");}}
 phase="preflight-periods";
 using(var c=cmd("SELECT COUNT(*) FROM CNT.CNT_PERIODOS WHERE CODIGO_EMPRESA=13 AND CODIGO_PERIODO IN (198,204) AND FECHA_CIERRE IS NULL")){if(Convert.ToInt32(await c.ExecuteScalarAsync())!=2)throw new Exception("Period closed or missing");}
 using(var c=cmd("SELECT COUNT(*) FROM CNT.CNT_DESCRIPTIVAS WHERE (DESCRIPCION_ID=18 AND TITULO_ID=5 AND TRIM(CODIGO)='02' OR DESCRIPCION_ID=125 AND TITULO_ID=1 AND TRIM(CODIGO)='COMP' OR DESCRIPCION_ID=19 AND TITULO_ID=5 AND TRIM(CODIGO)='03' OR DESCRIPCION_ID=120 AND TITULO_ID=1 AND TRIM(CODIGO)='CHEAUT') AND (CODIGO_EMPRESA=13 OR CODIGO_EMPRESA IS NULL)")){if(Convert.ToInt32(await c.ExecuteScalarAsync())!=4)throw new Exception("Catalog changed");}
 var financialBefore=await fingerprint();
 phase="backup";backupDir="/srv/ossmmasoft-pruebas/shared/req58/source-"+DateTime.UtcNow.ToString("yyyyMMddTHHmmssZ")+"-"+Guid.NewGuid().ToString("N")[..8];Directory.CreateDirectory(backupDir);File.SetUnixFileMode(backupDir,UnixFileMode.UserRead|UnixFileMode.UserWrite|UnixFileMode.UserExecute);
 foreach(var pair in before){var path=Path.Combine(backupDir,pair.Key+".txt");await File.WriteAllTextAsync(path,pair.Value);File.SetUnixFileMode(path,UnixFileMode.UserRead|UnixFileMode.UserWrite);}
 await File.WriteAllTextAsync(Path.Combine(backupDir,"manifest.json"),JsonSerializer.Serialize(hashes));File.SetUnixFileMode(Path.Combine(backupDir,"manifest.json"),UnixFileMode.UserRead|UnixFileMode.UserWrite);
 using(var c=cmd("ROLLBACK")){await c.ExecuteNonQueryAsync();}
 foreach(var item in input.GetProperty("Procedures").EnumerateArray()){
  var name=item.GetProperty("Name").GetString()!;var text=item.GetProperty("Sql").GetString()!;phase="install-"+name;
  if(sha(text)!=item.GetProperty("Sha256").GetString()||!text.StartsWith("CREATE OR REPLACE PROCEDURE CNT."+name+" ("))throw new Exception("DDL payload mismatch");
  started=true;await ddl(text);var state=await status(name);compile.Add(new{Name=name,Status=state});if(state!="VALID")throw new Exception("Compilation invalid");
 }
 phase="previews";
 foreach(var item in input.GetProperty("Cases").EnumerateArray()){
  var date=item.GetProperty("Date").GetString()!;phase="preview-"+date;var day=DateTime.Parse(date,CultureInfo.InvariantCulture);var actual=await preview(item.GetProperty("Period").GetInt32(),18,125,day);
  decimal debe=0,haber=0;bool accounts=true;foreach(var row in actual.Rows){var amount=decimal.Parse(row["MONTO"]!,CultureInfo.InvariantCulture);if(amount<0)debe-=amount;else haber+=amount;var major=row["MAYOR"]?.Trim();if(major is not("300" or "103")||major=="300"&&amount>=0||major=="103"&&amount<=0)accounts=false;}
  var passed=actual.Message=="Success"&&accounts&&actual.Rows.Count==item.GetProperty("OutputRows").GetInt32()&&debe==decimal.Parse(item.GetProperty("ExpectedDebe").GetString()!,CultureInfo.InvariantCulture)&&haber==decimal.Parse(item.GetProperty("ExpectedHaber").GetString()!,CultureInfo.InvariantCulture);
  previews.Add(new{Date=date,Lines=actual.Rows.Count,Debe=debe,Haber=haber,Message=actual.Message,Passed=passed,Rows=actual.Rows,WorkRolledBack=true});if(!passed)throw new Exception("Preview differs from captured source");
 }
 phase="incompatible-cheque";var invalid=await preview(198,19,120,new DateTime(2022,7,4));var invalidPassed=invalid.Rows.Count==0&&invalid.Message.Contains("incompatible",StringComparison.OrdinalIgnoreCase);if(!invalidPassed)throw new Exception("Incompatible type not explicit");
 phase="budget-overlap";string budgetMessage;using(var tx=cn.BeginTransaction()){using var c=cmd("CNT.SP_CNT_AUT_PRES_GET");c.CommandType=CommandType.StoredProcedure;c.Transaction=tx;c.Parameters.Add("p_FECHA_DESDE",OracleDbType.Date).Value=new DateTime(2022,12,31);c.Parameters.Add("p_FECHA_HASTA",OracleDbType.Date).Value=new DateTime(2022,12,31);c.Parameters.Add("p_CODIGO_EMPRESA",OracleDbType.Int32).Value=13;var budget=c.Parameters.Add("p_CODIGO_PRESUPUESTO",OracleDbType.Decimal,ParameterDirection.Output);var m=c.Parameters.Add("p_Message",OracleDbType.Varchar2,4000,null,ParameterDirection.Output);await c.ExecuteNonQueryAsync();budgetMessage=Convert.ToString(m.Value)??"";if(!budgetMessage.Contains("varios presupuestos",StringComparison.OrdinalIgnoreCase))throw new Exception("Overlap not rejected");tx.Rollback();}
 phase="postflight";var financialAfter=await fingerprint();if(financialBefore!=financialAfter)throw new Exception("Financial snapshot changed; review concurrent changes");if(configHash!=sha(await File.ReadAllTextAsync(configPath)))throw new Exception("Configuration changed concurrently");
 foreach(var item in input.GetProperty("Procedures").EnumerateArray())if(await status(item.GetProperty("Name").GetString()!)!="VALID")throw new Exception("Postflight invalid object");
 Console.WriteLine("R58_RESULT="+JsonSerializer.Serialize(new{Installed=true,Company=13,Database="XE",ServerHost="6d88ba053190",BackupDirectory=backupDir,BaselineVerified=true,Dependencies=dependencies,Compiled=compile,Previews=previews,InvalidTypeRejected=invalidPassed,InvalidTypeMessage=invalid.Message,BudgetOverlapRejected=true,BudgetOverlapMessage=budgetMessage,FinancialFingerprintBefore=financialBefore,FinancialFingerprintAfter=financialAfter,FinancialDataUnchanged=true,ConfigUnchanged=true,ConfirmExecuted=false,PreviewWorkRolledBack=true,ApplicationReleaseChanged=false}));
}catch(Exception ex){var failedPhase=phase;var failure=ex is OracleException oe?"ORA-"+oe.Number:ex.Message;var restoration="not-needed";
 if(started&&cn is not null){try{await restore();restoration="restored-baseline";}catch(Exception re){restoration="FAILED:"+(re is OracleException ro?"ORA-"+ro.Number:re.GetType().Name);}}
 Console.WriteLine("R58_RESULT="+JsonSerializer.Serialize(new{Installed=false,FailedPhase=failedPhase,Failure=failure,BackupDirectory=backupDir,Restored=restored,Restoration=restoration,Dependencies=dependencies,Compiled=compile,Previews=previews,ConfirmExecuted=false}));Environment.ExitCode=1;
}finally{cn?.Dispose();}
'''
remote=r'''import tempfile,pathlib,subprocess,sys,json,shutil,os
p=pathlib.Path(tempfile.mkdtemp(prefix='r58-authorized-install-'))
try:
 data=json.load(sys.stdin);runtime=pathlib.Path('/srv/ossmmasoft-pruebas/current/vertical').resolve();assert runtime.name=='r57-prenomina-txn-20261124T091632Z'
 refs=''.join('<Reference Include="'+name+'"><HintPath>'+str(runtime/(name+'.dll'))+'</HintPath></Reference>' for name in ['OssmmasoftVerticalSlice','Oracle.ManagedDataAccess','Microsoft.Data.SqlClient'])
 (p/'Probe.csproj').write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable></PropertyGroup><ItemGroup><FrameworkReference Include="Microsoft.AspNetCore.App"/>'+refs+'</ItemGroup></Project>')
 (p/'Program.cs').write_text(data['program']);(p/'payload.json').write_text(json.dumps(data['payload']))
 r=subprocess.run(['dotnet','run','--project',str(p/'Probe.csproj'),'--verbosity','quiet','--',str(p/'payload.json')],capture_output=True,text=True,timeout=170,env={**os.environ,'TZ':'America/Caracas'})
 print('\n'.join(x for x in r.stdout.splitlines() if x.startswith('R58_') or ': error CS' in x));sys.exit(r.returncode)
finally:shutil.rmtree(p)
'''
r=subprocess.run(['ssh','-i',str(Path.home()/'.ssh/ubuntu_pruebas'),'-o','BatchMode=yes','-o','ConnectTimeout=10','ossmma@74.121.190.6','python3 -c '+shlex.quote(remote)],input=json.dumps({'program':program,'payload':payload}),capture_output=True,text=True,timeout=190)
if 'R58_RESULT=' not in r.stdout:raise SystemExit(r.stdout[:1600] or r.stderr[:1200])
d=json.loads(r.stdout.split('R58_RESULT=')[-1]);d['InstallSqlSha256']={x['Name']:x['Sha256'] for x in items}
(E/'InstalacionYPreviewsQA.json').write_text(json.dumps(d,indent=2)+'\n')
print(json.dumps({k:v for k,v in d.items() if k not in ['Previews','InstallSqlSha256','FinancialFingerprintBefore','FinancialFingerprintAfter']}))
if 'Previews' in d:print(json.dumps({'PreviewDays':len(d['Previews']),'Passed':sum(x['Passed'] for x in d['Previews']),'Lines':sum(x['Lines'] for x in d['Previews'])}))
raise SystemExit(r.returncode)
