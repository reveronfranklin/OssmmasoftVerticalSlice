#!/usr/bin/env python3
"""Compare local catalog procedure with requested ODC/TDC SELECTs in QA, no DDL."""
from pathlib import Path
import json,re
import oracle_sintetico as oracle
ROOT=Path(__file__).resolve().parents[4]
sql=(ROOT/'OssmmasoftVerticalSlice/Features/Contabilidad/Sql/SP_CNT_CAT_DESC_GET.sql').read_text().strip().removesuffix('/').strip()
assert not re.search(r'\b(INSERT|UPDATE|DELETE|COMMIT|EXECUTE IMMEDIATE)\b',sql,re.I)
body=sql.replace('CREATE OR REPLACE PROCEDURE CNT.SP_CNT_CAT_DESC_GET (','PROCEDURE local_catalog (',1)
statement='DECLARE\n'+body+'\nBEGIN local_catalog(:id,:search,:company,:rows,:msg,:code); END;'
program=r'''
using System.Data;using System.Text.Json;using Microsoft.Extensions.Configuration;using Oracle.ManagedDataAccess.Client;using OssmmasoftVerticalSlice.ContextDB;
var input=JsonDocument.Parse(await File.ReadAllTextAsync(args[0])).RootElement;var cfg=new ConfigurationBuilder().AddJsonFile("/srv/ossmmasoft-pruebas/shared/config/vertical.json").Build();if(cfg.GetValue<int>("settings:EmpresaConfig")!=13)throw new Exception("Company mismatch");using var cn=new ConnectionDB(cfg).GetCntConnection();await cn.OpenAsync();
using(var c=cn.CreateCommand()){c.CommandText="SELECT SYS_CONTEXT('USERENV','DB_NAME'),SYS_CONTEXT('USERENV','SERVER_HOST'),SYS_CONTEXT('USERENV','SESSION_USER') FROM DUAL";using var r=await c.ExecuteReaderAsync();await r.ReadAsync();if(r.GetString(0)!="XE"||r.GetString(1)!="6d88ba053190"||r.GetString(2)!="CNT")throw new Exception("Identity mismatch");}
using(var c=cn.CreateCommand()){c.CommandText="SET TRANSACTION READ ONLY";await c.ExecuteNonQueryAsync();}
var result=new List<object>();
foreach(var test in new[]{(Code:"ODC",Id:(int?)null),(Code:"TDC",Id:(int?)null),(Code:(string?)null,Id:(int?)1),(Code:(string?)null,Id:(int?)5)}){
 var expected=new List<(int Id,string Description,string Code)>();
 using(var c=cn.CreateCommand()){
 c.BindByName=true;
 if(test.Code is not null){c.CommandText="SELECT A.DESCRIPCION_ID,A.DESCRIPCION,A.CODIGO FROM CNT.CNT_DESCRIPTIVAS A,CNT.CNT_TITULOS B WHERE B.CODIGO=:code AND A.TITULO_ID=B.TITULO_ID ORDER BY "+(test.Code=="ODC"?"2":"3");c.Parameters.Add("code",OracleDbType.Varchar2).Value=test.Code;}
 else {c.CommandText="SELECT DESCRIPCION_ID,DESCRIPCION,CODIGO FROM CNT.CNT_DESCRIPTIVAS WHERE TITULO_ID=:id AND (CODIGO_EMPRESA IS NULL OR CODIGO_EMPRESA=13) ORDER BY CODIGO,DESCRIPCION";c.Parameters.Add("id",OracleDbType.Int32).Value=test.Id;}
 using var r=await c.ExecuteReaderAsync();while(await r.ReadAsync()){if(expected.Count>=5000)throw new Exception("Bound exceeded");expected.Add((r.GetInt32(0),r.IsDBNull(1)?"":r.GetString(1),r.IsDBNull(2)?"":r.GetString(2)));}}
 var actual=new List<(int Id,string Description,string Code)>();
 using(var c=cn.CreateCommand()){c.BindByName=true;c.CommandText=input.GetProperty("Statement").GetString();c.CommandTimeout=30;c.Parameters.Add("id",OracleDbType.Int32).Value=(object?)test.Id??DBNull.Value;c.Parameters.Add("search",OracleDbType.Varchar2).Value=DBNull.Value;c.Parameters.Add("company",OracleDbType.Int32).Value=13;c.Parameters.Add("rows",OracleDbType.RefCursor).Direction=ParameterDirection.Output;var msg=c.Parameters.Add("msg",OracleDbType.Varchar2,4000,null,ParameterDirection.Output);c.Parameters.Add("code",OracleDbType.Varchar2).Value=(object?)test.Code??DBNull.Value;
 using(var r=await c.ExecuteReaderAsync()){while(await r.ReadAsync()){if(actual.Count>=5000)throw new Exception("Bound exceeded");actual.Add((r.GetInt32(r.GetOrdinal("ID")),r.IsDBNull(r.GetOrdinal("DESCRIPCION"))?"":r.GetString(r.GetOrdinal("DESCRIPCION")),r.IsDBNull(r.GetOrdinal("CODIGO"))?"":r.GetString(r.GetOrdinal("CODIGO"))));}}
 if(!string.Equals(Convert.ToString(msg.Value),"Success",StringComparison.OrdinalIgnoreCase))throw new Exception("Procedure failure");}
 if(!actual.OrderBy(x=>x.Id).SequenceEqual(expected.OrderBy(x=>x.Id)))throw new Exception("Catalog differs");
 if(test.Code is not null&&!actual.Select(x=>test.Code=="ODC"?x.Description:x.Code).SequenceEqual(expected.Select(x=>test.Code=="ODC"?x.Description:x.Code)))throw new Exception("Sort differs");
 result.Add(new{test.Code,test.Id,Rows=actual.Count,Passed=true});
}
using(var c=cn.CreateCommand()){c.CommandText="ROLLBACK";await c.ExecuteNonQueryAsync();}Console.WriteLine("R58_RESULT="+JsonSerializer.Serialize(new{ReadOnly=true,DdlExecuted=false,Company=13,Cases=result}));
'''
oracle.REMOTE=oracle.REMOTE.replace("runtime.name=='r57-prenomina-txn-20261124T091632Z'","runtime.name=='r58-filtro-tipo-api-20261124T222626Z'")
d,code=oracle.run_probe(program,{'Statement':statement});assert code==0
(ROOT/'Requerimientos/58 - Revision Compronbantes Automaticos/Evidencias/CatalogosCodigoSoloLecturaQA.json').write_text(json.dumps(d,indent=2)+'\n');print(json.dumps(d))
