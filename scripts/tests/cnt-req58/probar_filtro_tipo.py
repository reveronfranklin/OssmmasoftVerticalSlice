#!/usr/bin/env python3
"""Exercise the exact local GET_ALL body as a local PL/SQL procedure, SELECTs only."""
import json,re
from pathlib import Path
import oracle_sintetico as oracle
ROOT=Path(__file__).resolve().parents[4]
sql=(ROOT/'OssmmasoftVerticalSlice/Features/Contabilidad/Sql/SP_CNT_CMP_GET_ALL.sql').read_text().strip()
assert sql.endswith('/');sql=sql[:-1].strip()
body=sql.replace('CREATE OR REPLACE PROCEDURE CNT.SP_CNT_CMP_GET_ALL (','PROCEDURE local_list (',1)
assert body!=sql and not re.search(r'\b(INSERT|UPDATE|DELETE|COMMIT|EXECUTE IMMEDIATE)\b',body,re.I)
statement='DECLARE\n'+body+'\nBEGIN local_list(:sz,:pg,:search,:period,:origin,:fd,:fh,:auto,:company,:rows,:msg,:total,:pages,:state,:type,:debeTotal,:haberTotal); END;'
program=r'''
using System.Data;using System.Text.Json;using Microsoft.Extensions.Configuration;using Oracle.ManagedDataAccess.Client;using Oracle.ManagedDataAccess.Types;using OssmmasoftVerticalSlice.ContextDB;
var input=JsonDocument.Parse(await File.ReadAllTextAsync(args[0])).RootElement;var cfg=new ConfigurationBuilder().AddJsonFile("/srv/ossmmasoft-pruebas/shared/config/vertical.json").Build();if(cfg.GetValue<int>("settings:EmpresaConfig")!=13)throw new Exception("Company mismatch");using var cn=new ConnectionDB(cfg).GetCntConnection();await cn.OpenAsync();
using(var c=cn.CreateCommand()){c.CommandText="SELECT SYS_CONTEXT('USERENV','DB_NAME'),SYS_CONTEXT('USERENV','SERVER_HOST'),SYS_CONTEXT('USERENV','SESSION_USER') FROM DUAL";using var r=await c.ExecuteReaderAsync();await r.ReadAsync();if(r.GetString(0)!="XE"||r.GetString(1)!="6d88ba053190"||r.GetString(2)!="CNT")throw new Exception("QA identity mismatch");}
using(var c=cn.CreateCommand()){c.CommandText="SET TRANSACTION READ ONLY";await c.ExecuteNonQueryAsync();}
var types=new List<int>();using(var c=cn.CreateCommand()){c.CommandText="SELECT DISTINCT TIPO_COMPROBANTE_ID FROM CNT.CNT_COMPROBANTES WHERE CODIGO_EMPRESA=13 AND TIPO_COMPROBANTE_ID IS NOT NULL ORDER BY TIPO_COMPROBANTE_ID";using var r=await c.ExecuteReaderAsync();while(await r.ReadAsync()){if(types.Count>100)throw new Exception("Type bound");types.Add(r.GetInt32(0));}}
var cases=new List<(int? Type,int? Origin,int? Period,string? Search,string? State,int? Automatic)>();cases.Add((null,null,null,null,null,null));foreach(var t in types)cases.Add((t,null,null,null,null,null));cases.Add((-999,null,null,null,null,null));cases.Add((18,125,198,null,"FINALIZADO",1));cases.Add((18,125,198,"22-07",null,null));cases.Add((19,125,null,null,null,null));cases.Add((null,125,198,null,null,null));
var results=new List<object>();
foreach(var test in cases){
 int size=test.Period.HasValue?10:1000;
 var expected=new HashSet<int>();using(var c=cn.CreateCommand()){c.BindByName=true;c.CommandText=@"SELECT C.CODIGO_COMPROBANTE FROM CNT.CNT_COMPROBANTES C WHERE C.CODIGO_EMPRESA=13 AND (:t IS NULL OR C.TIPO_COMPROBANTE_ID=:t) AND (:o IS NULL OR C.ORIGEN_ID=:o) AND (:p IS NULL OR C.CODIGO_PERIODO=:p) AND (:s IS NULL OR UPPER(C.NUMERO_COMPROBANTE) LIKE '%'||UPPER(:s)||'%') AND (:st IS NULL OR C.ESTADO_CMP=:st) AND (:a IS NULL OR (:a=1 AND C.EXTRA1='AUTOMATICO') OR (:a=0 AND NVL(C.EXTRA1,' ')<>'AUTOMATICO'))";c.Parameters.Add("t",OracleDbType.Int32).Value=(object?)test.Type??DBNull.Value;c.Parameters.Add("o",OracleDbType.Int32).Value=(object?)test.Origin??DBNull.Value;c.Parameters.Add("p",OracleDbType.Int32).Value=(object?)test.Period??DBNull.Value;c.Parameters.Add("s",OracleDbType.Varchar2).Value=(object?)test.Search??DBNull.Value;c.Parameters.Add("st",OracleDbType.Varchar2).Value=(object?)test.State??DBNull.Value;c.Parameters.Add("a",OracleDbType.Int32).Value=(object?)test.Automatic??DBNull.Value;using var r=await c.ExecuteReaderAsync();while(await r.ReadAsync()){if(expected.Count>20000)throw new Exception("Row bound");expected.Add(r.GetInt32(0));}}
 var actual=new HashSet<int>();var totalPages=1;int total=0;
 for(int page=1;page<=Math.Max(totalPages,1);page++){
  using var c=cn.CreateCommand();c.BindByName=true;c.CommandText=input.GetProperty("Statement").GetString();c.CommandTimeout=30;
  void add(string n,OracleDbType t,object? value)=>c.Parameters.Add(n,t).Value=value??DBNull.Value;
  add("sz",OracleDbType.Int32,size);add("pg",OracleDbType.Int32,page);add("search",OracleDbType.Varchar2,test.Search);add("period",OracleDbType.Int32,test.Period);add("origin",OracleDbType.Int32,test.Origin);add("fd",OracleDbType.Date,null);add("fh",OracleDbType.Date,null);add("auto",OracleDbType.Int32,test.Automatic);add("company",OracleDbType.Int32,13);c.Parameters.Add("rows",OracleDbType.RefCursor).Direction=ParameterDirection.Output;var msg=c.Parameters.Add("msg",OracleDbType.Varchar2,4000,null,ParameterDirection.Output);var count=c.Parameters.Add("total",OracleDbType.Int32,ParameterDirection.Output);var pages=c.Parameters.Add("pages",OracleDbType.Int32,ParameterDirection.Output);add("state",OracleDbType.Varchar2,test.State);add("type",OracleDbType.Int32,test.Type);var sumDebe=c.Parameters.Add("debeTotal",OracleDbType.Decimal,ParameterDirection.Output);var sumHaber=c.Parameters.Add("haberTotal",OracleDbType.Decimal,ParameterDirection.Output);
  int rows=0;using(var r=await c.ExecuteReaderAsync()){while(await r.ReadAsync()){rows++;if(test.Type.HasValue&&r.GetInt32(r.GetOrdinal("TIPO_COMPROBANTE_ID"))!=test.Type)throw new Exception("Wrong type");if(!actual.Add(r.GetInt32(r.GetOrdinal("CODIGO_COMPROBANTE"))))throw new Exception("Duplicate page row");}}
  if(!string.Equals(Convert.ToString(msg.Value),"Success",StringComparison.OrdinalIgnoreCase))throw new Exception("Procedure failure");total=((OracleDecimal)count.Value).ToInt32();totalPages=((OracleDecimal)pages.Value).ToInt32();if(total!=expected.Count||totalPages!=(int)Math.Ceiling(expected.Count/(decimal)size)||rows>size)throw new Exception("Pagination total mismatch");
 }
 if(!actual.SetEquals(expected))throw new Exception("Selection mismatch");results.Add(new{test.Type,test.Origin,test.Period,test.Search,test.State,test.Automatic,Rows=actual.Count,PageSize=size,Pages=totalPages,Passed=true});
}
using(var c=cn.CreateCommand()){c.CommandText="ROLLBACK";await c.ExecuteNonQueryAsync();}
Console.WriteLine("R58_RESULT="+JsonSerializer.Serialize(new{ReadOnly=true,DdlExecuted=false,LocalProcedureBody=true,Company=13,Cases=results}));
'''
# Pin this probe to the already verified current R58 API; do not change shared helper.
oracle.REMOTE=oracle.REMOTE.replace("runtime.name=='r57-prenomina-txn-20261124T091632Z'","runtime.name=='7b24b1b686203d01b382a59ec9acb0d43512716a-45486326254-1'")
d,code=oracle.run_probe(program,{'Statement':statement});assert code==0
(ROOT/'Requerimientos/58 - Revision Compronbantes Automaticos/Evidencias/FiltroTipoSoloLecturaQA.json').write_text(json.dumps(d,indent=2)+'\n')
print(json.dumps(d))
