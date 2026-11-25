#!/usr/bin/env python3
"""Real local SQL expressions on DUAL fixtures; no CONF, DML or installation."""
import json,re,hashlib
from pathlib import Path
from oracle_sintetico import run_probe
ROOT=Path(__file__).resolve().parents[4];SQL=ROOT/'OssmmasoftVerticalSlice/Features/Contabilidad/Sql';E=ROOT/'Requerimientos/58 - Revision Compronbantes Automaticos/Evidencias'
conf=(SQL/'SP_CNT_AUT_CONF.sql').read_text()
args=re.findall(r'line.DESCRIPCION,\s*([^,\n]+),',conf)
assert len(args)==4 and all(x.strip()=='-line.MONTO' for x in args)
expressions=[]
for name in ['SP_CNT_CMP_GET_ALL','SP_CNT_CMP_GET_ID']:
 text=(SQL/(name+'.sql')).read_text()
 d=re.search(r'(SUM\(CASE WHEN MONTO[^\n]+?\)) AS TOTAL_DEBE',text).group(1)
 h=re.search(r'(SUM\(CASE WHEN MONTO[^\n]+?\)) AS TOTAL_HABER',text).group(1)
 expressions.append((name,d,h))
# Independent cases include an unbalanced pending voucher and its reversal.
fixtures=[('pending','-125.35','70','125.35','70'),('balanced','-100','100','100','100'),('reversal','125.35','-70','70','125.35'),('rounding','-0.005','0.005','0.01','0.01')]
cases=[]
for name,d,h in expressions:
 for label,a,b,ed,eh in fixtures:
  query=f"WITH work_rows AS (SELECT ROUND({a},2) MONTO FROM DUAL UNION ALL SELECT ROUND({b},2) FROM DUAL), physical_rows AS (SELECT {args[0].strip().replace('line.','w.')} MONTO FROM work_rows w) SELECT {d} DEBE,{h} HABER,SUM(MONTO) SALDO_ORACLE FROM physical_rows"
  cases.append({'Name':name+':'+label,'Sql':query,'Debe':ed,'Haber':eh})
program=r'''
using System.Globalization;using System.Text.Json;using Microsoft.Extensions.Configuration;using Oracle.ManagedDataAccess.Client;using OssmmasoftVerticalSlice.ContextDB;
var payload=JsonDocument.Parse(await File.ReadAllTextAsync(args[0])).RootElement;
try{
 var cfg=new ConfigurationBuilder().AddJsonFile("/srv/ossmmasoft-pruebas/shared/config/vertical.json").Build();if(cfg.GetValue<int>("settings:EmpresaConfig")!=13)throw new Exception("Company mismatch");using var cn=new ConnectionDB(cfg).GetCntConnection();await cn.OpenAsync();
 using(var c=cn.CreateCommand()){c.CommandText="SELECT SYS_CONTEXT('USERENV','DB_NAME'),SYS_CONTEXT('USERENV','SERVER_HOST'),SYS_CONTEXT('USERENV','SESSION_USER') FROM DUAL";using var r=await c.ExecuteReaderAsync();await r.ReadAsync();if(r.GetString(0)!="XE"||r.GetString(1)!="6d88ba053190"||r.GetString(2)!="CNT")throw new Exception("Identity mismatch");}
 using(var c=cn.CreateCommand()){c.CommandText="ROLLBACK";await c.ExecuteNonQueryAsync();}using(var c=cn.CreateCommand()){c.CommandText="SET TRANSACTION READ ONLY";await c.ExecuteNonQueryAsync();}
 var results=new List<object>();foreach(var item in payload.GetProperty("cases").EnumerateArray()){
  using var c=cn.CreateCommand();c.CommandTimeout=20;c.CommandText=item.GetProperty("Sql").GetString();using var r=await c.ExecuteReaderAsync();await r.ReadAsync();var d=r.GetDecimal(0);var h=r.GetDecimal(1);var saldo=r.GetDecimal(2);var passed=d==decimal.Parse(item.GetProperty("Debe").GetString()!,CultureInfo.InvariantCulture)&&h==decimal.Parse(item.GetProperty("Haber").GetString()!,CultureInfo.InvariantCulture)&&saldo==d-h;
  results.Add(new{Name=item.GetProperty("Name").GetString(),Debe=d,Haber=h,SaldoOracle=saldo,Passed=passed});if(!passed)Environment.ExitCode=1;
 }
 using(var c=cn.CreateCommand()){c.CommandText="ROLLBACK";await c.ExecuteNonQueryAsync();}
 Console.WriteLine("R58_RESULT="+JsonSerializer.Serialize(new{ReadOnlyTransaction=true,SyntheticOnly=true,ConfExecuted=false,DdlExecuted=false,DatabaseWrites=false,Results=results}));
}catch(OracleException ex){Console.WriteLine("R58_ORACLE="+ex.Number);Environment.ExitCode=1;}
'''
d,code=run_probe(program,{'cases':cases});d['ConfSourceSha256']=hashlib.sha256(conf.encode()).hexdigest();d['Scope']='Exact persistence argument from four branches and header aggregate expressions; DUAL fixtures. Does not execute CONF or validate its complete side effects.'
(E/'PruebasConversionSqlQA.json').write_text(json.dumps(d,indent=2)+'\n');print(json.dumps({'Cases':len(cases),'Passed':sum(x['Passed'] for x in d['Results']),'ReadOnly':True,'ConfExecuted':False,'DdlExecuted':False}));raise SystemExit(code)
