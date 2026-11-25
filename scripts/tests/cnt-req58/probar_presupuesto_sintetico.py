#!/usr/bin/env python3
"""Execute the R58 budget helper body on DUAL fixtures, never install it or read business data."""
import hashlib
import json
from pathlib import Path
import shlex
import subprocess

ROOT = Path(__file__).resolve().parents[4]
SOURCE = ROOT / 'OssmmasoftVerticalSlice/Features/Contabilidad/Sql/SP_CNT_AUT_PRES_GET.sql'
text = SOURCE.read_text()
assert text.count('FROM PRE.PRE_PRESUPUESTOS') == 1
assert all(token not in text.upper() for token in ['COMMIT;', 'INSERT INTO', 'DELETE FROM', 'UPDATE ', 'EXECUTE IMMEDIATE'])
# Only the source relation is substituted. The procedure's executable logic stays unchanged.
fixtures = [(101,13,'2022-01-01','2022-12-31'), (201,6,'2022-01-01','2022-12-31'),
            (102,13,'2023-01-01','2023-12-31'), (103,13,'2023-06-01','2023-08-31'),
            (104,13,'2024-01-01','2024-06-30'), (105,13,'2025-01-01','2025-06-30'),
            (106,13,'2025-07-01','2025-12-31'), (107,13,'2026-01-01','2026-12-31')]
rows = ' UNION ALL '.join(f"SELECT {i} CODIGO_PRESUPUESTO,{emp} CODIGO_EMPRESA,DATE '{start}' FECHA_DESDE,DATE '{end}' FECHA_HASTA FROM DUAL" for i,emp,start,end in fixtures)
local = text.replace('CREATE OR REPLACE PROCEDURE CNT.SP_CNT_AUT_PRES_GET', 'PROCEDURE get_budget', 1)
local = local.replace('FROM PRE.PRE_PRESUPUESTOS', 'FROM (' + rows + ')')
assert local.endswith('/\n')
local = local[:-2]
block = 'DECLARE\n' + local + '\nBEGIN get_budget(:desde,:hasta,:empresa,:presupuesto,:message); END;'
cases = [
 ('company13','2022-07-04','2022-07-04',13,101),
 ('company6','2022-07-04','2022-07-04',6,201),
 ('missing-company','2022-07-04','2022-07-04',14,None),
 ('null-company','2022-07-04','2022-07-04',None,None),
 ('invalid-company','2022-07-04','2022-07-04',0,None),
 ('overlap','2023-07-04','2023-07-04',13,None),
 ('overlap-starts-inside','2023-05-01','2023-06-02',13,None),
 ('outside-overlap','2023-02-01','2023-02-28',13,102),
 ('cross-year','2022-12-31','2023-01-01',13,None),
 ('partial-end','2024-06-29','2024-07-01',13,None),
 ('partial-start','2023-12-31','2024-01-02',13,None),
 ('before-only-budget','2021-12-31','2022-01-02',13,None),
 ('no-budget','2024-07-01','2024-07-31',13,None),
 ('adjacent-crossing','2025-06-30','2025-07-01',13,None),
 ('adjacent-before','2025-06-30','2025-06-30',13,105),
 ('adjacent-after','2025-07-01','2025-07-01',13,106),
 ('end-of-day','2026-12-31T23:59:59','2026-12-31T23:59:59',13,107),
 ('null-start',None,'2022-07-04',13,None),
 ('null-end','2022-07-04',None,13,None),
 ('reverse','2022-07-05','2022-07-04',13,None),
]
program=r'''
using System.Data;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Oracle.ManagedDataAccess.Client;
using OssmmasoftVerticalSlice.ContextDB;
var payload=JsonDocument.Parse(await File.ReadAllTextAsync(args[0])).RootElement;
try{
 var cfg=new ConfigurationBuilder().AddJsonFile("/srv/ossmmasoft-pruebas/shared/config/vertical.json").Build();
 if(cfg.GetValue<int>("settings:EmpresaConfig")!=13)throw new Exception("Company mismatch");
 using var cn=new ConnectionDB(cfg).GetCntConnection();await cn.OpenAsync();
 using(var c=cn.CreateCommand()){c.CommandText="SELECT SYS_CONTEXT('USERENV','DB_NAME'),SYS_CONTEXT('USERENV','SERVER_HOST'),SYS_CONTEXT('USERENV','SESSION_USER') FROM DUAL";using var r=await c.ExecuteReaderAsync();await r.ReadAsync();if(r.GetString(0)!="XE"||r.GetString(1)!="6d88ba053190"||r.GetString(2)!="CNT")throw new Exception("Identity mismatch");}
 using(var c=cn.CreateCommand()){c.CommandText="ROLLBACK";await c.ExecuteNonQueryAsync();}
 using(var c=cn.CreateCommand()){c.CommandText="SET TRANSACTION READ ONLY";await c.ExecuteNonQueryAsync();}
 var results=new List<object>();
 foreach(var item in payload.GetProperty("cases").EnumerateArray()){
  using var cmd=cn.CreateCommand();cmd.BindByName=true;cmd.CommandTimeout=20;cmd.CommandText=payload.GetProperty("block").GetString();
  foreach(var pair in new[]{("desde",1),("hasta",2)})cmd.Parameters.Add(pair.Item1,OracleDbType.Date).Value=item[pair.Item2].ValueKind==JsonValueKind.Null?DBNull.Value:DateTime.Parse(item[pair.Item2].GetString()!,System.Globalization.CultureInfo.InvariantCulture);
  cmd.Parameters.Add("empresa",OracleDbType.Int32).Value=item[3].ValueKind==JsonValueKind.Null?DBNull.Value:item[3].GetInt32();
  var number=cmd.Parameters.Add("presupuesto",OracleDbType.Decimal,ParameterDirection.Output);
  var message=cmd.Parameters.Add("message",OracleDbType.Varchar2,4000,null,ParameterDirection.Output);
  await cmd.ExecuteNonQueryAsync();
  int? actual=number.Value is Oracle.ManagedDataAccess.Types.OracleDecimal value&&!value.IsNull?value.ToInt32():null;
  int? expected=item[4].ValueKind==JsonValueKind.Null?null:item[4].GetInt32();
  var status=Convert.ToString(message.Value);var passed=actual==expected&&(expected.HasValue?status=="Success":status!="Success"&&!string.IsNullOrWhiteSpace(status));
  results.Add(new{Name=item[0].GetString(),Expected=expected,Actual=actual,Message=status,Passed=passed});
  if(!passed)Environment.ExitCode=1;
 }
 using(var c=cn.CreateCommand()){c.CommandText="ROLLBACK";await c.ExecuteNonQueryAsync();}
 Console.WriteLine("R58_RESULT="+JsonSerializer.Serialize(new{ReadOnlyTransaction=true,SyntheticOnly=true,BusinessTablesRead=false,DdlExecuted=false,Results=results}));
}catch(OracleException ex){Console.WriteLine("R58_ORACLE="+ex.Number);Environment.ExitCode=1;}
catch(Exception ex){Console.WriteLine("R58_ERROR="+ex.GetType().Name);Environment.ExitCode=1;}
'''
remote=r'''import tempfile,pathlib,subprocess,sys,json,shutil,os
p=pathlib.Path(tempfile.mkdtemp(prefix='r58-budget-fixtures-'))
try:
 data=json.load(sys.stdin);runtime=pathlib.Path('/srv/ossmmasoft-pruebas/current/vertical').resolve();assert runtime.name=='r57-prenomina-txn-20261124T091632Z'
 refs=''.join('<Reference Include="'+name+'"><HintPath>'+str(runtime/(name+'.dll'))+'</HintPath></Reference>' for name in ['OssmmasoftVerticalSlice','Oracle.ManagedDataAccess','Microsoft.Data.SqlClient'])
 (p/'Probe.csproj').write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable></PropertyGroup><ItemGroup><FrameworkReference Include="Microsoft.AspNetCore.App"/>'+refs+'</ItemGroup></Project>')
 (p/'Program.cs').write_text(data['program']);(p/'fixture.json').write_text(json.dumps(data['payload']))
 r=subprocess.run(['dotnet','run','--project',str(p/'Probe.csproj'),'--verbosity','quiet','--',str(p/'fixture.json')],capture_output=True,text=True,timeout=100,env={**os.environ,'TZ':'America/Caracas'})
 print('\n'.join(x for x in r.stdout.splitlines() if x.startswith('R58_') or ': error CS' in x));sys.exit(r.returncode)
finally:shutil.rmtree(p)
'''
r=subprocess.run(['ssh','-i',str(Path.home()/'.ssh/ubuntu_pruebas'),'-o','BatchMode=yes','-o','ConnectTimeout=10','ossmma@74.121.190.6','python3 -c '+shlex.quote(remote)],input=json.dumps({'program':program,'payload':{'cases':cases,'block':block}}),capture_output=True,text=True,timeout=120)
if 'R58_RESULT=' not in r.stdout:raise SystemExit(r.stdout[:1200] or r.stderr[:1000])
d=json.loads(r.stdout.split('R58_RESULT=')[-1]);d['SourceSha256']=hashlib.sha256(text.encode()).hexdigest();d['Scope']='Body of SP_CNT_AUT_PRES_GET; only FROM replaced with DUAL fixtures. Not installed.'
(ROOT/'Requerimientos/58 - Revision Compronbantes Automaticos/Evidencias/PruebasPresupuestoSinteticoQA.json').write_text(json.dumps(d,indent=2)+'\n')
print(json.dumps({'Cases':len(d['Results']),'Passed':sum(x['Passed'] for x in d['Results']),'ReadOnly':True,'SyntheticOnly':True,'DdlExecuted':False}))
raise SystemExit(r.returncode)
