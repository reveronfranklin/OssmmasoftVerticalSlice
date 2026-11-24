#!/usr/bin/env python3
"""Run only the separately approved ten R58 source-reconciliation SELECTs in QA."""
import argparse,hashlib,json,re
from pathlib import Path
from oracle_sintetico import run_probe
ROOT=Path(__file__).resolve().parents[4]
path=ROOT/'Requerimientos/58 - Revision Compronbantes Automaticos/ConsultasConciliacionFuentes.Propuesta.json'
parser=argparse.ArgumentParser(description=__doc__)
parser.add_argument('--ejecutar-lectura-aprobada',action='store_true',required=True)
parser.parse_args()
doc=json.loads(path.read_text())
assert doc['Company']==13 and doc['MaxRowsPerQuery']==5000 and len(doc['Queries'])==10
assert doc['Database']=='XE' and doc['ServerHost']=='6d88ba053190'
for item in doc['Queries']:
 assert item['Sql'].startswith('SELECT ')
 assert ';' not in item['Sql'] and not re.search(r'\b(?:INSERT|UPDATE|DELETE|MERGE|CREATE|ALTER|DROP|EXECUTE|COMMIT|FOR UPDATE)\b',item['Sql'],re.I)
program=r'''
using System.Data;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Oracle.ManagedDataAccess.Client;
using OssmmasoftVerticalSlice.ContextDB;
var payload=JsonDocument.Parse(await File.ReadAllTextAsync(args[0])).RootElement;
string phase="configuration";
try{
 var cfg=new ConfigurationBuilder().AddJsonFile("/srv/ossmmasoft-pruebas/shared/config/vertical.json").Build();
 if(cfg.GetValue<int>("settings:EmpresaConfig")!=13)throw new Exception("Company mismatch");
 using var cn=new ConnectionDB(cfg).GetCntConnection();await cn.OpenAsync();
 using(var c=cn.CreateCommand()){c.CommandText="SELECT SYS_CONTEXT('USERENV','DB_NAME'),SYS_CONTEXT('USERENV','SERVER_HOST'),SYS_CONTEXT('USERENV','SESSION_USER') FROM DUAL";using var r=await c.ExecuteReaderAsync();await r.ReadAsync();if(r.GetString(0)!="XE"||r.GetString(1)!="6d88ba053190"||r.GetString(2)!="CNT")throw new Exception("Identity mismatch");}
 using(var c=cn.CreateCommand()){c.CommandText="ROLLBACK";await c.ExecuteNonQueryAsync();}
 using(var c=cn.CreateCommand()){c.CommandText="SET TRANSACTION READ ONLY";await c.ExecuteNonQueryAsync();}
 var results=new List<object>();
 foreach(var item in payload.GetProperty("Queries").EnumerateArray()){
  phase=item.GetProperty("Name").GetString()!;var sql=item.GetProperty("Sql").GetString()!;
  if(!sql.StartsWith("SELECT ")||sql.Contains(';')||Regex.IsMatch(sql,@"\b(INSERT|UPDATE|DELETE|MERGE|CREATE|ALTER|DROP|EXECUTE|COMMIT)\b",RegexOptions.IgnoreCase))throw new Exception("Statement refused");
  using var c=cn.CreateCommand();c.BindByName=true;c.CommandTimeout=20;c.InitialLONGFetchSize=-1;c.CommandText=sql;
  using var r=await c.ExecuteReaderAsync();var rows=new List<Dictionary<string,string?>>();
  while(await r.ReadAsync()){
   if(rows.Count==5000)throw new Exception("Row bound exceeded");
   var row=new Dictionary<string,string?>();
   for(int i=0;i<r.FieldCount;i++){
    var value=r.IsDBNull(i)?null:r.GetValue(i);var s=value is DateTime dt?dt.ToString("O",CultureInfo.InvariantCulture):Convert.ToString(value,CultureInfo.InvariantCulture);
    if(s?.Length>100000)throw new Exception("Text bound exceeded");row[r.GetName(i)]=s;
   }
   rows.Add(row);
  }
  results.Add(new{Name=phase,Complete=true,Rows=rows});
 }
 using(var c=cn.CreateCommand()){c.CommandText="ROLLBACK";await c.ExecuteNonQueryAsync();}
 Console.WriteLine("R58_RESULT="+JsonSerializer.Serialize(new{ReadOnlyTransaction=true,Company=13,Database="XE",ServerHost="6d88ba053190",ProgramExecution=false,DdlExecuted=false,DatabaseWrites=false,SingleSnapshot=true,Results=results}));
}catch(OracleException ex){Console.WriteLine("R58_ORACLE="+phase+":"+ex.Number);Environment.ExitCode=1;}
catch(Exception ex){Console.WriteLine("R58_ERROR="+phase+":"+ex.GetType().Name);Environment.ExitCode=1;}
'''
d,code=run_probe(program,doc)
assert not code and len(d['Results'])==10 and all(x['Complete'] for x in d['Results'])
d['ApprovedProposalSha256']=hashlib.sha256(path.read_bytes()).hexdigest()
(ROOT/'Requerimientos/58 - Revision Compronbantes Automaticos/Evidencias/ConciliacionFuentesQA.json').write_text(json.dumps(d,indent=2)+'\n')
print(json.dumps({'ReadOnly':True,'Company':13,'Queries':[{'Name':x['Name'],'Rows':len(x['Rows'])} for x in d['Results']]}))
