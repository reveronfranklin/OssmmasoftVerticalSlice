#!/usr/bin/env python3
"""Evaluate the real COMP02 selector against the approved captured source amounts, read-only."""
import json,hashlib
from collections import defaultdict
from decimal import Decimal,ROUND_HALF_UP
from pathlib import Path
from copy import deepcopy
from probar_comp02_sintetico import base,query,selector,guard,source
from oracle_sintetico import run_probe
ROOT=Path(__file__).resolve().parents[4]
E=ROOT/'Requerimientos/58 - Revision Compronbantes Automaticos/Evidencias'
doc=json.loads((E/'ConciliacionFuentesQA.json').read_text())
src=next(x['Rows'] for x in doc['Results'] if x['Name']=='ImportesCompromisos')
config=json.loads((E/'AuxiliaresFuentesCompQA.json').read_text())
majors=json.loads((E/'MayoresQA.json').read_text())['data']['resultado']['datos']['filas']
assert len({r['CODIGO_PUC_COMPROMISO'] for r in src})==len(src)
num=lambda x:None if x is None else int(x)
context=[];inputs={}
for date in sorted({r['FECHA_COMPROMISO'][:10] for r in src if r['STATUS']!='AN'}):
 rows=[r for r in src if r['FECHA_COMPROMISO'][:10]==date]
 budgets={r['CODIGO_PRESUPUESTO'] for r in rows if r['STATUS']!='AN'};assert len(budgets)==1
 budget=next(iter(budgets));tables=deepcopy(base)
 headers={r['CODIGO_COMPROMISO']:r for r in rows}
 tables['PRE.PRE_COMPROMISOS'][1]=[[num(r['CODIGO_COMPROMISO']),r['NUMERO_COMPROMISO'],'DATE:'+date,'Descripcion omitida en conciliacion',num(r['CODIGO_PROVEEDOR']),13,num(r['CODIGO_PRESUPUESTO']),r['STATUS']] for r in headers.values()]
 tables['PRE.PRE_DETALLE_COMPROMISOS'][1]=[list(x) for x in sorted({(num(r['CODIGO_COMPROMISO']),num(r['CODIGO_DETALLE_COMPROMISO'])) for r in rows})]
 # Decimal values remain numeric literals, never binary floating-point conversions.
 tables['PRE.PRE_PUC_COMPROMISOS'][1]=[[num(r['CODIGO_PUC_COMPROMISO']),num(r['CODIGO_PUC']),num(r['CODIGO_DETALLE_COMPROMISO']),Decimal(r['MONTO']),num(r['CODIGO_EMPRESA']),num(r['PRES_PARTIDA'])] for r in rows]
 tables['CNT.CNT_MAYORES'][1]=[[num(r['CODIGO_MAYOR']),r['NUMERO_MAYOR'],13] for r in majors]
 tables['CNT.CNT_AUXILIARES'][1]=[[num(r['CODIGO_AUXILIAR']),num(r['CODIGO_MAYOR']),num(r['CODIGO_PROVEEDOR']),13,r['SEGMENTO1'],r['SEGMENTO2'],'Aux '+r['CODIGO_AUXILIAR']] for r in config['Rows']]
 tables['CNT.CNT_AUXILIARES_PUC'][1]=[[num(r['CODIGO_AUXILIAR']),num(r['CODIGO_PUC']),r['TIPO_DOCUMENTO_ID'],13] for r in config['Maps']]
 context.append({'Name':date,'Date':date,'Budget':num(budget),'Query':query(selector,tables),'Guard':query(guard,tables)})
 inputs[date]=rows
program=r'''
using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Oracle.ManagedDataAccess.Client;
using OssmmasoftVerticalSlice.ContextDB;
var payload=JsonDocument.Parse(await File.ReadAllTextAsync(args[0])).RootElement;string phase="open";
try{
 var cfg=new ConfigurationBuilder().AddJsonFile("/srv/ossmmasoft-pruebas/shared/config/vertical.json").Build();if(cfg.GetValue<int>("settings:EmpresaConfig")!=13)throw new Exception("Company mismatch");
 using var cn=new ConnectionDB(cfg).GetCntConnection();await cn.OpenAsync();
 using(var c=cn.CreateCommand()){c.CommandText="SELECT SYS_CONTEXT('USERENV','DB_NAME'),SYS_CONTEXT('USERENV','SERVER_HOST'),SYS_CONTEXT('USERENV','SESSION_USER') FROM DUAL";using var r=await c.ExecuteReaderAsync();await r.ReadAsync();if(r.GetString(0)!="XE"||r.GetString(1)!="6d88ba053190"||r.GetString(2)!="CNT")throw new Exception("Identity mismatch");}
 using(var c=cn.CreateCommand()){c.CommandText="ROLLBACK";await c.ExecuteNonQueryAsync();}using(var c=cn.CreateCommand()){c.CommandText="SET TRANSACTION READ ONLY";await c.ExecuteNonQueryAsync();}
 var result=new List<object>();
 foreach(var item in payload.GetProperty("cases").EnumerateArray()){
  phase=item.GetProperty("Name").GetString()!;
  OracleCommand command(string sql){var c=cn.CreateCommand();c.BindByName=true;c.CommandTimeout=20;c.CommandText=sql;c.Parameters.Add("CODIGO_EMPRESA",OracleDbType.Int32).Value=13;c.Parameters.Add("CODIGO_PRESUPUESTO",OracleDbType.Int32).Value=item.GetProperty("Budget").GetInt32();var dt=DateTime.Parse(item.GetProperty("Date").GetString()!,CultureInfo.InvariantCulture);c.Parameters.Add("FECHA_DESDE",OracleDbType.Date).Value=dt;c.Parameters.Add("FECHA_HASTA",OracleDbType.Date).Value=dt;return c;}
  using var guard=command(item.GetProperty("Guard").GetString()!);var count=Convert.ToInt32(await guard.ExecuteScalarAsync());
  using var cmd=command(item.GetProperty("Query").GetString()!);using var r=await cmd.ExecuteReaderAsync();var rows=new List<Dictionary<string,string?>>();
  while(await r.ReadAsync()){if(rows.Count>=5000)throw new Exception("Row bound");var row=new Dictionary<string,string?>();for(int i=0;i<r.FieldCount;i++)row[r.GetName(i)]=r.IsDBNull(i)?null:Convert.ToString(r.GetValue(i),CultureInfo.InvariantCulture);rows.Add(row);}
  result.Add(new{Name=phase,InvalidChildren=count,Rows=rows});
 }
 using(var c=cn.CreateCommand()){c.CommandText="ROLLBACK";await c.ExecuteNonQueryAsync();}
 Console.WriteLine("R58_RESULT="+JsonSerializer.Serialize(new{ReadOnlyTransaction=true,DdlExecuted=false,DatabaseWrites=false,WorkWritten=false,Results=result}));
}catch(OracleException ex){Console.WriteLine("R58_ORACLE="+phase+":"+ex.Number);Environment.ExitCode=1;}
catch(Exception ex){Console.WriteLine("R58_ERROR="+phase+":"+ex.GetType().Name);Environment.ExitCode=1;}
'''
d,code=run_probe(program,{'cases':context});assert code==0
money=lambda x:Decimal(x).quantize(Decimal('.01'),rounding=ROUND_HALF_UP)
comparisons=[]
for case in d['Results']:
 expected=defaultdict(Decimal);actual=defaultdict(lambda:defaultdict(Decimal));failures=[]
 for row in inputs[case['Name']]:
  if row['STATUS']=='AN' or row['CODIGO_PROVEEDOR'] in ['8127','8128']:continue
  expected[row['CODIGO_COMPROMISO']]+=Decimal(row['MONTO'])
 expected={k:money(v) for k,v in expected.items() if v!=0}
 if case['InvalidChildren']:failures.append('Partidas con empresa/presupuesto inconsistentes')
 for row in case['Rows']:
  if row['MIN_CAND']!='1' or row['MAX_CAND']!='1':failures.append('Cardinalidad de auxiliar distinta de uno: '+row['CODIGO_COMPROMISO'])
  if row['NUMERO_MAYOR'] not in ['300','103']:failures.append('Mayor inesperado')
  actual[row['CODIGO_COMPROMISO']][row['NUMERO_MAYOR']]+=abs(money(row['MONTO']))
 for id in set(expected)|set(actual):
  if actual[id]['300']!=abs(expected.get(id,Decimal(0))) or actual[id]['103']!=abs(expected.get(id,Decimal(0))):failures.append('Diferencia de importe: '+id)
 comparisons.append({'Date':case['Name'],'Documents':len(expected),'OutputRows':len(case['Rows']),'ExpectedDebe':str(sum((abs(v) for v in expected.values()),Decimal(0))),'ExpectedHaber':str(sum((abs(v) for v in expected.values()),Decimal(0))),'Passed':not failures,'Failures':failures})
d['Comparisons']=comparisons;d['SourceSha256']=hashlib.sha256(source.encode()).hexdigest();d['SourceReceiptSha256']=hashlib.sha256((E/'ConciliacionFuentesQA.json').read_bytes()).hexdigest();d['ConfigurationSingleSnapshotWithSources']=False
d['Scope']='Real approved captured amounts and configuration materialized into DUAL. Executes real SELECT/guard, not WORK writes or installed dispatcher. Descriptions substituted. Not full integrated acceptance.'
(E/'EvaluacionComp02FuentesQA.json').write_text(json.dumps(d,indent=2)+'\n')
print(json.dumps({'Days':len(comparisons),'PassedDays':sum(x['Passed'] for x in comparisons),'Documents':sum(x['Documents'] for x in comparisons),'OutputRows':sum(x['OutputRows'] for x in comparisons),'Debe':str(sum(Decimal(x['ExpectedDebe']) for x in comparisons)),'Failures':[x for x in comparisons if not x['Passed']]}))
raise SystemExit(0 if all(x['Passed'] for x in comparisons) else 1)
