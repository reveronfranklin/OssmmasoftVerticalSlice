#!/usr/bin/env python3
"""User-requested CNT source audit. Dictionary SELECTs only; never invoke writers."""
import json,hashlib,re
from pathlib import Path
from oracle_sintetico import run_probe
ROOT=Path(__file__).resolve().parents[4];E=ROOT/'Requerimientos/58 - Revision Compronbantes Automaticos/Evidencias'
program=r'''
using System.Globalization;using System.Text.Json;using Microsoft.Extensions.Configuration;using Oracle.ManagedDataAccess.Client;using OssmmasoftVerticalSlice.ContextDB;
string phase="open";
try{
 var cfg=new ConfigurationBuilder().AddJsonFile("/srv/ossmmasoft-pruebas/shared/config/vertical.json").Build();if(cfg.GetValue<int>("settings:EmpresaConfig")!=13)throw new Exception("Company mismatch");using var cn=new ConnectionDB(cfg).GetCntConnection();await cn.OpenAsync();
 using(var c=cn.CreateCommand()){c.CommandText="SELECT SYS_CONTEXT('USERENV','DB_NAME'),SYS_CONTEXT('USERENV','SERVER_HOST'),SYS_CONTEXT('USERENV','SESSION_USER') FROM DUAL";using var r=await c.ExecuteReaderAsync();await r.ReadAsync();if(r.GetString(0)!="XE"||r.GetString(1)!="6d88ba053190"||r.GetString(2)!="CNT")throw new Exception("Identity mismatch");}
 using(var c=cn.CreateCommand()){c.CommandText="ROLLBACK";await c.ExecuteNonQueryAsync();}using(var c=cn.CreateCommand()){c.CommandText="SET TRANSACTION READ ONLY";await c.ExecuteNonQueryAsync();}
 async Task<List<Dictionary<string,string?>>> read(string sql,int limit){using var c=cn.CreateCommand();c.CommandText=sql;c.BindByName=true;c.CommandTimeout=30;using var r=await c.ExecuteReaderAsync();var rows=new List<Dictionary<string,string?>>();while(await r.ReadAsync()){if(rows.Count==limit)throw new Exception("Row bound");var row=new Dictionary<string,string?>();for(int i=0;i<r.FieldCount;i++){var v=r.IsDBNull(i)?null:r.GetValue(i);row[r.GetName(i)]=v is DateTime dt?dt.ToString("O",CultureInfo.InvariantCulture):Convert.ToString(v,CultureInfo.InvariantCulture);}rows.Add(row);}return rows;}
 phase="triggers";var triggers=await read("SELECT TRIGGER_NAME,TRIGGER_TYPE,TRIGGERING_EVENT,TABLE_OWNER,TABLE_NAME,STATUS FROM ALL_TRIGGERS WHERE OWNER='CNT' AND TABLE_OWNER='CNT' AND TABLE_NAME IN ('CNT_COMPROBANTES','CNT_DETALLE_COMPROBANTE') ORDER BY TRIGGER_NAME",100);
 phase="columns";var columns=await read("SELECT COLUMN_NAME,DATA_TYPE,NULLABLE FROM ALL_TAB_COLUMNS WHERE OWNER='CNT' AND TABLE_NAME='CNT_DETALLE_COMPROBANTE' ORDER BY COLUMN_ID",100);
 phase="dependencies";var dependencies=await read("SELECT DISTINCT OWNER,NAME,TYPE,REFERENCED_NAME,REFERENCED_TYPE FROM ALL_DEPENDENCIES WHERE OWNER='CNT' AND REFERENCED_OWNER='CNT' AND REFERENCED_NAME IN ('CNT_COMPROBANTES','CNT_DETALLE_COMPROBANTE') ORDER BY NAME,TYPE,REFERENCED_NAME",500);
 phase="source";var source=await read(@"SELECT S.NAME,S.TYPE,S.LINE,S.TEXT FROM ALL_SOURCE S WHERE S.OWNER='CNT' AND S.TYPE IN ('PROCEDURE','FUNCTION','PACKAGE BODY','TRIGGER') AND ((S.NAME,S.TYPE) IN (SELECT D.NAME,D.TYPE FROM ALL_DEPENDENCIES D WHERE D.OWNER='CNT' AND D.REFERENCED_OWNER='CNT' AND D.REFERENCED_NAME IN ('CNT_COMPROBANTES','CNT_DETALLE_COMPROBANTE')) OR (S.NAME,S.TYPE) IN (SELECT X.NAME,X.TYPE FROM ALL_SOURCE X WHERE X.OWNER='CNT' AND (UPPER(X.TEXT) LIKE '%CNT_DETALLE_COMPROBANTE%' OR UPPER(X.TEXT) LIKE '%CNT_COMPROBANTES%'))) ORDER BY S.NAME,S.TYPE,S.LINE",50000);
 using(var c=cn.CreateCommand()){c.CommandText="ROLLBACK";await c.ExecuteNonQueryAsync();}
 Console.WriteLine("R58_RESULT="+JsonSerializer.Serialize(new{ReadOnlyTransaction=true,Company=13,Owner="CNT",BusinessTablesRead=false,ProgramsInvoked=false,DdlExecuted=false,Triggers=triggers,Columns=columns,Dependencies=dependencies,Source=source}));
}catch(OracleException ex){Console.WriteLine("R58_ORACLE="+phase+":"+ex.Number);Environment.ExitCode=1;}catch(Exception ex){Console.WriteLine("R58_ERROR="+phase+":"+ex.GetType().Name);Environment.ExitCode=1;}
'''
d,code=run_probe(program,{});assert code==0
folder=E/'EscritoresCNT';folder.mkdir(exist_ok=True);groups={}
for row in d.pop('Source'):groups.setdefault((row['NAME'],row['TYPE']),[]).append(row)
manifest=[]
for (name,kind),rows in groups.items():
 assert [int(r['LINE']) for r in rows]==list(range(1,len(rows)+1)),name
 text=''.join(r['TEXT'] or '' for r in rows);file=name+'.'+kind.replace(' ','_')+'.txt';(folder/file).write_text(text)
 manifest.append({'Name':name,'Type':kind,'Lines':len(rows),'Sha256':hashlib.sha256(text.encode()).hexdigest(),'File':file,'Wrapped':bool(re.search(r'\bwrapped\b',text[:500],re.I))})
d['Sources']=manifest;d['Scope']='CNT only: direct dependencies and source mentions of voucher header/detail. Dynamic SQL not mentioning full table names may require further tracing.'
(E/'AuditoriaEscritoresCntQA.json').write_text(json.dumps(d,indent=2)+'\n')
print(json.dumps({'ReadOnly':True,'Triggers':d['Triggers'],'SourceObjects':len(manifest),'SourceLines':sum(x['Lines'] for x in manifest),'HasDebeColumn':any(x['COLUMN_NAME']=='DEBE' for x in d['Columns']),'HasHaberColumn':any(x['COLUMN_NAME']=='HABER' for x in d['Columns'])}))
