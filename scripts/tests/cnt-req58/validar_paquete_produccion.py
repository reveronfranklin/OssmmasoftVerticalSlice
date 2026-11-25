#!/usr/bin/env python3
"""Validate the production bundle against QA dictionary/read-only metadata; never runs its DDL."""
from pathlib import Path
import sys,importlib.util,json
ROOT=Path(__file__).resolve().parents[4]
spec=importlib.util.spec_from_file_location('r58_bundle',ROOT/'OssmmasoftVerticalSlice/scripts/build-cnt-r58-production.py');bundle=importlib.util.module_from_spec(spec);spec.loader.exec_module(bundle)
import oracle_sintetico as oracle
program=r'''
using System.Text;using System.Text.Json;using System.Text.RegularExpressions;using System.Security.Cryptography;using Oracle.ManagedDataAccess.Client;using Oracle.ManagedDataAccess.Types;using Microsoft.Extensions.Configuration;using OssmmasoftVerticalSlice.ContextDB;
var input=JsonDocument.Parse(await File.ReadAllTextAsync(args[0])).RootElement;string phase="identity";
try {
var cfg=new ConfigurationBuilder().AddJsonFile("/srv/ossmmasoft-pruebas/shared/config/vertical.json").Build();if(cfg.GetValue<int>("settings:EmpresaConfig")!=13)throw new Exception("Company mismatch");using var cn=new ConnectionDB(cfg).GetCntConnection();await cn.OpenAsync();
OracleCommand cmd(string sql){var c=cn.CreateCommand();c.BindByName=true;c.CommandText=sql;c.CommandTimeout=30;return c;}
using(var c=cmd("SELECT SYS_CONTEXT('USERENV','DB_NAME'),SYS_CONTEXT('USERENV','SERVER_HOST'),SYS_CONTEXT('USERENV','SESSION_USER') FROM DUAL")){using var r=await c.ExecuteReaderAsync();await r.ReadAsync();if(r.GetString(0)!="XE"||r.GetString(1)!="6d88ba053190"||r.GetString(2)!="CNT")throw new Exception("Not QA");}
using(var c=cmd("SET TRANSACTION READ ONLY")){await c.ExecuteNonQueryAsync();}
phase="production-guard";bool rejected=false;try{using var c=cmd(input.GetProperty("Guard").GetString()!);await c.ExecuteNonQueryAsync();}catch(OracleException ex)when(ex.Number==20058){rejected=true;}if(!rejected)throw new Exception("Production guard failed");
phase="preflight";using(var c=cmd(input.GetProperty("Preflight").GetString()!)){c.Parameters.Add("R58_EMPRESA",OracleDbType.Int32).Value=13;await c.ExecuteNonQueryAsync();}
string canonical(string text){var start=Regex.Match(text,@"\b(?:CREATE OR REPLACE\s+)?PROCEDURE\s+",RegexOptions.IgnoreCase);if(!start.Success)throw new Exception("No procedure");text=text[start.Index..].Trim();text=Regex.Replace(text,@"\ACREATE OR REPLACE\s+","",RegexOptions.IgnoreCase);return Regex.Replace(text,@"\APROCEDURE\s+(?:CNT\.)?([A-Z0-9_]+)\s*\(","PROCEDURE $1(",RegexOptions.IgnoreCase).Replace("\r\n","\n");}
var sources=new Dictionary<string,string>();var checks=new List<object>();
phase="published-source";foreach(var item in input.GetProperty("Sources").EnumerateObject()){
 var raw=new StringBuilder();using(var c=cmd("SELECT TEXT FROM ALL_SOURCE WHERE OWNER='CNT' AND NAME=:n AND TYPE='PROCEDURE' ORDER BY LINE")){c.Parameters.Add("n",OracleDbType.Varchar2).Value=item.Name;using var r=await c.ExecuteReaderAsync();while(await r.ReadAsync())raw.Append(r.GetString(0));}
 var text=raw.ToString();if(canonical(text)!=canonical(item.Value.GetString()!))throw new Exception("Source mismatch:"+item.Name);sources[item.Name]=text;
 using(var c=cmd("SELECT STATUS FROM ALL_OBJECTS WHERE OWNER='CNT' AND OBJECT_NAME=:n AND OBJECT_TYPE='PROCEDURE'")){c.Parameters.Add("n",OracleDbType.Varchar2).Value=item.Name;if(Convert.ToString(await c.ExecuteScalarAsync())!="VALID")throw new Exception("Invalid:"+item.Name);}
 checks.Add(new{Name=item.Name,MatchesQa=true,Valid=true});
}
phase="backup-writer";using(var c=cmd("BEGIN DBMS_OUTPUT.DISABLE; DBMS_OUTPUT.ENABLE(1000000); END;")){await c.ExecuteNonQueryAsync();}
using(var c=cmd(input.GetProperty("Backup").GetString()!)){await c.ExecuteNonQueryAsync();}
string backup;
using(var c=cmd(@"DECLARE l_text CLOB;l_line VARCHAR2(32767);l_status NUMBER;BEGIN DBMS_LOB.CREATETEMPORARY(l_text,TRUE);LOOP DBMS_OUTPUT.GET_LINE(l_line,l_status);EXIT WHEN l_status<>0;IF l_line IS NOT NULL THEN DBMS_LOB.WRITEAPPEND(l_text,LENGTH(l_line),l_line);END IF;DBMS_LOB.WRITEAPPEND(l_text,1,CHR(10));END LOOP;:output:=l_text;END;")){var p=c.Parameters.Add("output",OracleDbType.Clob, System.Data.ParameterDirection.Output);await c.ExecuteNonQueryAsync();backup=((OracleClob)p.Value).Value;}
foreach(var item in sources){var begin="-- R58 RESTORE BEGIN "+item.Key+"\n";var end="-- R58 RESTORE END "+item.Key+"\n";var a=backup.IndexOf(begin,StringComparison.Ordinal)+begin.Length;var b=backup.IndexOf(end,a,StringComparison.Ordinal);if(a<begin.Length||b<0)throw new Exception("Backup section missing");var restored=backup[a..b].Trim();if(!restored.EndsWith('/'))throw new Exception("Missing restore terminator");if(canonical(restored[..^1])!=canonical(item.Value))throw new Exception("Backup differs:"+item.Key);}
if(Regex.Matches(backup,"CREATE OR REPLACE").Count!=10)throw new Exception("Incomplete backup");
using(var c=cmd("ROLLBACK")){await c.ExecuteNonQueryAsync();}
Console.WriteLine("R58_RESULT="+JsonSerializer.Serialize(new{Passed=true,ReadOnly=true,DdlExecuted=false,BusinessProgramsExecuted=false,ProductionConnected=false,ProductionGuardRejectedQa=rejected,PreflightPassed=true,SourceChecks=checks,BackupRoundTripProcedures=10,BackupBytes=Encoding.UTF8.GetByteCount(backup),BackupSha256=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(backup))).ToLowerInvariant()}));
}catch(OracleException ex){Console.WriteLine("R58_RESULT="+JsonSerializer.Serialize(new{Passed=false,Phase=phase,OracleCode=ex.Number,Message=ex.Message}));Environment.ExitCode=1;}catch(Exception ex){Console.WriteLine("R58_RESULT="+JsonSerializer.Serialize(new{Passed=false,Phase=phase,Message=ex.Message}));Environment.ExitCode=1;}
'''
strip=lambda text:text.strip().removesuffix('/').strip()
oracle.REMOTE=oracle.REMOTE.replace("runtime.name=='r57-prenomina-txn-20261124T091632Z'","runtime.name=='r58-totales-api-20261125T001319Z'")
d,code=oracle.run_probe(program,{'Guard':strip(bundle.GUARD),'Preflight':strip(bundle.PREFLIGHT),'Backup':strip(bundle.BACKUP),'Sources':{k:strip(v) for k,v in bundle.SOURCES.items()}})
(ROOT/'Requerimientos/58 - Revision Compronbantes Automaticos/Produccion/VerificacionQA.json').write_text(json.dumps(d,indent=2)+'\n');print(json.dumps(d));assert code==0 and d['Passed']
