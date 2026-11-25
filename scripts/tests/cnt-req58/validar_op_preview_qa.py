#!/usr/bin/env python3
"""Authorized rolled-back OP previews using captured source totals. No DDL or CONF."""
import argparse,ast,json,subprocess,shlex,hashlib
from collections import defaultdict
from decimal import Decimal,ROUND_HALF_UP
from pathlib import Path
ROOT=Path(__file__).resolve().parents[4];E=ROOT/'Requerimientos/58 - Revision Compronbantes Automaticos/Evidencias'
parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--previews-qa-autorizados',action='store_true',required=True);parser.add_argument('--offset',type=int,default=0);parser.add_argument('--limit',type=int,default=3);args=parser.parse_args();assert 0<=args.offset and 1<=args.limit<=6
assert json.loads((E/'InstalacionYPreviewsQA.json').read_text())['Installed']
doc=json.loads((E/'ConciliacionFuentesQA.json').read_text());rs={x['Name']:x['Rows'] for x in doc['Results']}
amounts=defaultdict(Decimal)
for row in rs['ImportesPucOp']:amounts[row['CODIGO_ORDEN_PAGO']]+=Decimal(row['MONTO'])
groups=defaultdict(list)
for row in rs['CabecerasOp']:
 date=(row['FECHA_UPD'] if row['STATUS']=='AN' else row['FECHA_ORDEN_PAGO'])[:10]
 assert date[:7] in ['2022-07','2023-01']
 groups[(date,row['STATUS']=='AN')].append(row)
cases=[]
for (date,annul),rows in sorted(groups.items()):
 total=sum((abs(amounts[r['CODIGO_ORDEN_PAGO']].quantize(Decimal('.01'),rounding=ROUND_HALF_UP)) for r in rows),Decimal(0))
 cases.append({'Date':date,'Annulment':annul,'Origin':121 if annul else 122,'Type':19,'Period':198 if date.startswith('2022-07') else 204,'ExpectedTotal':str(total),'ExpectedDocuments':len(rows),'References':sorted(r['NUMERO_ORDEN_PAGO'] for r in rows)})
# The installed selector ignores type in OP: compare one real context03 vs04 without certifying either pair.
normal=next(x for x in cases if not x['Annulment']);cases.append({**normal,'Type':20,'CompareWith03':True})
total_cases=len(cases);cases=cases[args.offset:args.offset+args.limit];assert cases
# Reuse only the installer runtime/helper TEXT, not its executing Python module or DDL flow.
tree=ast.parse((Path(__file__).parent/'instalar_y_validar_qa.py').read_text())
constants={t.targets[0].id:t.value.value for t in tree.body if isinstance(t,ast.Assign) and len(t.targets)==1 and isinstance(t.targets[0],ast.Name) and isinstance(t.value,ast.Constant) and isinstance(t.value.value,str)}
base=constants['program'];fp=base[base.index('async Task<string> fingerprint()'):base.index('async Task ddl(')]
pv=base[base.index('async Task<(List<Dictionary<string,string?>> Rows,string Message)> preview('):base.index('\ntry{\n var configPath=')]
program=r'''
using System.Data;using System.Globalization;using System.Security.Cryptography;using System.Text;using System.Text.Json;
using Microsoft.Extensions.Configuration;using Oracle.ManagedDataAccess.Client;using OssmmasoftVerticalSlice.ContextDB;
var input=JsonDocument.Parse(await File.ReadAllTextAsync(args[0])).RootElement;OracleConnection? cn=null;string phase="open";
OracleCommand cmd(string sql){var c=cn!.CreateCommand();c.BindByName=true;c.CommandTimeout=30;c.CommandText=sql;return c;}
'''+fp+pv+r'''
try{
 var cfg=new ConfigurationBuilder().AddJsonFile("/srv/ossmmasoft-pruebas/shared/config/vertical.json").Build();if(cfg.GetValue<int>("settings:EmpresaConfig")!=13)throw new Exception("Company mismatch");cn=new ConnectionDB(cfg).GetCntConnection();await cn.OpenAsync();
 using(var c=cmd("SELECT SYS_CONTEXT('USERENV','DB_NAME'),SYS_CONTEXT('USERENV','SERVER_HOST'),SYS_CONTEXT('USERENV','SESSION_USER') FROM DUAL")){using var r=await c.ExecuteReaderAsync();await r.ReadAsync();if(r.GetString(0)!="XE"||r.GetString(1)!="6d88ba053190"||r.GetString(2)!="CNT")throw new Exception("Identity mismatch");}
 var before=await fingerprint();if(before!=input.GetProperty("ExpectedFinancialHash").GetString())throw new Exception("Financial snapshot differs from last installation receipt");var results=new List<object>();var outcomes=new List<bool>();
 foreach(var item in input.GetProperty("Cases").EnumerateArray()){
  var date=item.GetProperty("Date").GetString()!;var origin=item.GetProperty("Origin").GetInt32();var type=item.GetProperty("Type").GetInt32();var annul=item.GetProperty("Annulment").GetBoolean();phase=date+":"+origin+":"+type;Console.WriteLine("R58_PROGRESS="+phase);Console.Out.Flush();
  var actual=await preview(item.GetProperty("Period").GetInt32(),type,origin,DateTime.Parse(date,CultureInfo.InvariantCulture));decimal debe=0,haber=0;var signed=true;var refs=new HashSet<string>();
  foreach(var row in actual.Rows){var amount=decimal.Parse(row["MONTO"]!,CultureInfo.InvariantCulture);if(amount<0)debe-=amount;else haber+=amount;var m=row["MAYOR"]?.Trim();if(m is not("103" or "101"))signed=false;else if(m=="103"?(annul?amount<=0:amount>=0):(annul?amount>=0:amount<=0))signed=false;refs.Add(row["REFERENCIA1"]!);}
  var expected=decimal.Parse(item.GetProperty("ExpectedTotal").GetString()!,CultureInfo.InvariantCulture);var expectedRefs=item.GetProperty("References").EnumerateArray().Select(x=>x.GetString()!).ToHashSet();
  var passed=actual.Message=="Success"&&signed&&debe==expected&&haber==expected&&refs.SetEquals(expectedRefs)&&actual.Rows.Count==2*item.GetProperty("ExpectedDocuments").GetInt32();outcomes.Add(passed);
  results.Add(new{Date=date,Origin=origin,Type=type,Annulment=annul,Lines=actual.Rows.Count,ExpectedTotal=expected,Debe=debe,Haber=haber,Message=actual.Message,Passed=passed,Rows=actual.Rows,WorkRolledBack=true});
 }
 var after=await fingerprint();if(before!=after)throw new Exception("Financial snapshot changed");
 Console.WriteLine("R58_RESULT="+JsonSerializer.Serialize(new{Company=13,InstalledProcedure="SP_CNT_AUT_PREV",DdlExecuted=false,ConfirmExecuted=false,PreviewWorkRolledBack=true,FinancialDataUnchanged=true,FinancialFingerprintBefore=before,FinancialFingerprintAfter=after,Results=results,AllMatchedCapturedSources=outcomes.All(x=>x)}));
}catch(OracleException ex){Console.WriteLine("R58_ORACLE="+phase+":"+ex.Number);Environment.ExitCode=1;}catch(Exception ex){Console.WriteLine("R58_ERROR="+phase+":"+ex.GetType().Name);Environment.ExitCode=1;}finally{cn?.Dispose();}
'''
assert 'SP_CNT_AUT_CONF' not in program and 'CREATE OR REPLACE' not in program
remote=constants['remote'].replace('r58-authorized-install-','r58-op-preview-')
remote=remote.replace('finally:shutil.rmtree(p)', '''finally:
 import signal,time
 for process in pathlib.Path('/proc').iterdir():
  if not process.name.isdigit():continue
  try:
   if str(p).encode() in (process/'cmdline').read_bytes() and int(process.name)!=os.getpid() and process.stat().st_uid==os.getuid():
    os.kill(int(process.name),signal.SIGTERM);time.sleep(0.2)
    if process.exists():os.kill(int(process.name),signal.SIGKILL)
  except (FileNotFoundError,PermissionError,ProcessLookupError):pass
 shutil.rmtree(p)
''')
r=subprocess.run(['ssh','-i',str(Path.home()/'.ssh/ubuntu_pruebas'),'-o','BatchMode=yes','-o','ConnectTimeout=10','ossmma@74.121.190.6','python3 -c '+shlex.quote(remote)],input=json.dumps({'program':program,'payload':{'Cases':cases,'ExpectedFinancialHash':json.loads((E/'InstalacionYPreviewsQA.json').read_text())['FinancialFingerprintAfter']}}),capture_output=True,text=True,timeout=190)
if 'R58_RESULT=' not in r.stdout:raise SystemExit(r.stdout[:1800] or r.stderr[:1000])
d=json.loads(r.stdout.split('R58_RESULT=')[-1]);d['CapturedSourceSha256']=hashlib.sha256((E/'ConciliacionFuentesQA.json').read_bytes()).hexdigest()
d['Scope']='Only COMPRE/E scenarios present in the approved source sample. Matching monetary outputs does not certify origin/type compatibility or other OP branches.'
d['CaseOffset']=args.offset;d['TotalCaseCount']=total_cases
(E/('PreviewsOpQA_'+str(args.offset)+'.json')).write_text(json.dumps(d,indent=2)+'\n')
print(json.dumps({'Cases':len(d['Results']),'Matched':sum(x['Passed'] for x in d['Results']),'AnnulmentCases':sum(x['Annulment'] for x in d['Results']),'FinancialDataUnchanged':d['FinancialDataUnchanged'],'Failures':[{k:v for k,v in x.items() if k!='Rows'} for x in d['Results'] if not x['Passed']]}))
raise SystemExit(r.returncode)
