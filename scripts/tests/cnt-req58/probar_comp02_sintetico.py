#!/usr/bin/env python3
"""Test the real COMP02 SELECT and child guard against DUAL fixtures; no DML/DDL."""
import hashlib,json,re
from copy import deepcopy
from pathlib import Path
from oracle_sintetico import run_probe
ROOT=Path(__file__).resolve().parents[4]
source=(ROOT/'OssmmasoftVerticalSlice/Features/Contabilidad/Sql/SP_CNT_AUT_BUILD_COMP02.sql').read_text()
start=source.index('        WITH fuentes AS (');end=source.index('\n    ) LOOP',start)
selector=source[start:end]
a=source.index('    SELECT COUNT(*) INTO v_cuentas\n      FROM PRE.PRE_COMPROMISOS')
b=source.index(';',a)
guard=source[a:b].replace(' INTO v_cuentas','')
base={
 'PRE.PRE_COMPROMISOS':[['CODIGO_COMPROMISO','NUMERO_COMPROMISO','FECHA_COMPROMISO','MOTIVO','CODIGO_PROVEEDOR','CODIGO_EMPRESA','CODIGO_PRESUPUESTO','STATUS'],
                       [[101,'C-1','DATE:2022-07-04','Fixture',777,13,77,'AP']]],
 'PRE.PRE_DETALLE_COMPROMISOS':[['CODIGO_COMPROMISO','CODIGO_DETALLE_COMPROMISO'],[[101,5]]],
 'PRE.PRE_PUC_COMPROMISOS':[['CODIGO_PUC_COMPROMISO','CODIGO_PUC','CODIGO_DETALLE_COMPROMISO','MONTO','CODIGO_EMPRESA','CODIGO_PRESUPUESTO'],[[1,80,5,100,13,77]]],
 'CNT.CNT_MAYORES':[['CODIGO_MAYOR','NUMERO_MAYOR','CODIGO_EMPRESA'],[[420,'300',13],[730,'103',13],[901,'101',13],[912,'128',13],[917,'133',13]]],
 'CNT.CNT_AUXILIARES':[['CODIGO_AUXILIAR','CODIGO_MAYOR','CODIGO_PROVEEDOR','CODIGO_EMPRESA','SEGMENTO1','SEGMENTO2','DENOMINACION'],
                     [[4201,420,777,13,'001','000','300 default'],[7301,730,777,13,'001','000','103 default'],[9011,901,777,13,'001','000','Other phase'],[9121,912,777,13,'001','000','Other phase'],[9171,917,777,13,'001','000','Other phase']]],
 'CNT.CNT_AUXILIARES_PUC':[['CODIGO_AUXILIAR','CODIGO_PUC','TIPO_DOCUMENTO_ID','CODIGO_EMPRESA'],[]]}

def literal(value,col):
 if value is None:return 'CAST(NULL AS '+('VARCHAR2(100)' if col in ['TIPO_DOCUMENTO_ID'] else 'NUMBER')+')'
 if isinstance(value,str):
  if value.startswith('DATE:'):return "DATE '"+value[5:]+"'"
  return "'"+value.replace("'","''")+"'"
 return str(value)

def inline(cols,rows):
 if not rows:return 'SELECT '+','.join(literal(None,c)+' '+c for c in cols)+' FROM DUAL WHERE 1=0'
 return ' UNION ALL '.join('SELECT '+','.join(literal(v,c)+' '+c for c,v in zip(cols,row))+' FROM DUAL' for row in rows)

def query(sql,tables):
 for name,(cols,rows) in sorted(tables.items(),key=lambda x:-len(x[0])):
  sql=re.sub(r'\b'+re.escape(name)+r'\b',lambda _: '('+inline(cols,rows)+')',sql)
 for key in ['CODIGO_EMPRESA','CODIGO_PRESUPUESTO','FECHA_DESDE','FECHA_HASTA']:
  sql=re.sub(r'\bp_'+key+r'\b',':'+key,sql)
 assert not re.search(r'\b(?:PRE|CNT)\.',sql)
 assert not re.search(r'\b(?:INSERT|DELETE|UPDATE|CREATE|EXECUTE|COMMIT)\b',sql,re.I)
 return sql

def full_block(tables):
 body=source[source.index('CREATE OR REPLACE PROCEDURE'):].removesuffix('/\n')
 body=body.replace('CREATE OR REPLACE PROCEDURE CNT.SP_CNT_AUT_BUILD_COMP02','PROCEDURE build_comp02',1)
 body=body.replace('DELETE FROM CNT.CNT_AUT_LINE_WRK WHERE RUN_ID = p_RUN_ID;', 'clear_lines;')
 start=body.index('            INSERT INTO CNT.CNT_AUT_LINE_WRK (')
 end=body.index('\n            );',start)+len('\n            );')
 body=body[:start]+"            emit_line(r.NUMERO_MAYOR, r.CODIGO_AUXILIAR, v_monto);"+body[end:]
 for name,(cols,rows) in sorted(tables.items(),key=lambda x:-len(x[0])):
  body=re.sub(r'\b'+re.escape(name)+r'\b',lambda _: '('+inline(cols,rows)+')',body)
 assert not re.search(r'\b(?:INSERT|DELETE FROM|UPDATE|CREATE|EXECUTE|COMMIT)\b',body,re.I)
 assert not re.search(r'\b(?:PRE|CNT)\.',body)
 return """DECLARE
 TYPE t_line IS RECORD (major VARCHAR2(10),aux NUMBER,monto NUMBER);
 TYPE t_lines IS TABLE OF t_line INDEX BY BINARY_INTEGER;
 lines t_lines;
 n NUMBER := 0;
 d NUMBER := 0;
 h NUMBER := 0;
 PROCEDURE clear_lines IS BEGIN lines.DELETE; n:=0; END;
 PROCEDURE emit_line(m VARCHAR2,a NUMBER,v NUMBER) IS BEGIN n:=n+1;lines(n).major:=m;lines(n).aux:=a;lines(n).monto:=v;END;
 """+body+"""
 BEGIN
 build_comp02('fixture',:FECHA_DESDE,:FECHA_HASTA,:CODIGO_PRESUPUESTO,:CODIGO_EMPRESA,:MESSAGE);
 FOR i IN 1..n LOOP IF lines(i).monto<0 THEN d:=d-lines(i).monto;ELSE h:=h+lines(i).monto;END IF;END LOOP;
 :LINE_COUNT:=n;:DEBE:=d;:HABER:=h;
 END;"""

cases=[]
def add(name,tables,valid,lines=0,total='0',aux=None):
 cases.append({'Name':name,'Query':query(selector,tables),'Guard':query(guard,tables),
               'FullBlock':full_block(tables),'ExpectedValid':valid,'ExpectedLines':lines,'ExpectedTotal':total,'ExpectedAux':aux or []})
add('only-first-phase-no-fixed-ids',base,True,2,'100',[4201,7301])
t=deepcopy(base);t['CNT.CNT_AUXILIARES'][1].append([4202,420,999,13,'002','000','Mapped non-vendor'])
t['CNT.CNT_AUXILIARES_PUC'][1].append([4202,80,' 002 ',13]);add('mapped-before-vendor',t,True,2,'100',[4202,7301])
t['CNT.CNT_AUXILIARES_PUC'][1].append([4202,80,'02',13]);add('repeated-map-does-not-multiply',t,True,2,'100',[4202,7301])
t=deepcopy(base);t['CNT.CNT_AUXILIARES'][1].append([4202,420,999,13,'002','000','Mapped'])
t['PRE.PRE_PUC_COMPROMISOS'][1]=[[1,80,5,60,13,77],[2,81,5,40,13,77]]
t['CNT.CNT_AUXILIARES_PUC'][1].append([4202,80,'02',13]);add('allocate-per-puc-not-whole-document',t,True,3,'100',[4201,4202,7301])
t['PRE.PRE_PUC_COMPROMISOS'][1]=[[1,80,5,0.005,13,77],[2,81,5,0.005,13,77]];add('reject-rounding-imbalance',t,False)
t=deepcopy(base);t['CNT.CNT_AUXILIARES'][1].append([4202,420,777,13,'002','000','Duplicate vendor']);add('ambiguous-vendor',t,False)
t['CNT.CNT_AUXILIARES_PUC'][1]=[[4201,80,'02',13],[4202,80,'02',13]];add('ambiguous-map',t,False)
t=deepcopy(base);t['CNT.CNT_AUXILIARES'][1]=[r for r in t['CNT.CNT_AUXILIARES'][1] if r[0]!=7301];add('missing-haber-not-silent',t,False)
t=deepcopy(base);t['CNT.CNT_AUXILIARES_PUC'][1].append([4202,80,'02',6]);t['CNT.CNT_AUXILIARES'][1].append([4202,420,999,6,'002','000','Other company']);add('ignore-other-company-map',t,True,2,'100',[4201,7301])
t=deepcopy(base);t['PRE.PRE_COMPROMISOS'][1][0][5]=6;add('exclude-other-company-source',t,True)
t=deepcopy(base);t['PRE.PRE_COMPROMISOS'][1][0][6]=88;add('exclude-other-budget-source',t,True)
t=deepcopy(base);t['PRE.PRE_COMPROMISOS'][1][0][7]='AN';add('exclude-cancelled-source',t,True)
t=deepcopy(base);t['PRE.PRE_PUC_COMPROMISOS'][1][0][4]=None;add('reject-null-child-company',t,False)
t=deepcopy(base);t['PRE.PRE_PUC_COMPROMISOS'][1][0][5]=88;add('reject-child-budget-mismatch',t,False)
t=deepcopy(base);t['PRE.PRE_PUC_COMPROMISOS'][1][0][3]=0;add('zero-source-no-lines',t,True)
program=r'''
using System.Data;
using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Oracle.ManagedDataAccess.Client;
using OssmmasoftVerticalSlice.ContextDB;
var payload=JsonDocument.Parse(await File.ReadAllTextAsync(args[0])).RootElement;
string phase="open";
try{
 var cfg=new ConfigurationBuilder().AddJsonFile("/srv/ossmmasoft-pruebas/shared/config/vertical.json").Build();
 if(cfg.GetValue<int>("settings:EmpresaConfig")!=13)throw new Exception("Company mismatch");
 using var cn=new ConnectionDB(cfg).GetCntConnection();await cn.OpenAsync();
 using(var c=cn.CreateCommand()){c.CommandText="SELECT SYS_CONTEXT('USERENV','DB_NAME'),SYS_CONTEXT('USERENV','SERVER_HOST'),SYS_CONTEXT('USERENV','SESSION_USER') FROM DUAL";using var r=await c.ExecuteReaderAsync();await r.ReadAsync();if(r.GetString(0)!="XE"||r.GetString(1)!="6d88ba053190"||r.GetString(2)!="CNT")throw new Exception("Identity mismatch");}
 using(var c=cn.CreateCommand()){c.CommandText="ROLLBACK";await c.ExecuteNonQueryAsync();}
 using(var c=cn.CreateCommand()){c.CommandText="SET TRANSACTION READ ONLY";await c.ExecuteNonQueryAsync();}
 OracleCommand command(string sql){var c=cn.CreateCommand();c.BindByName=true;c.CommandTimeout=20;c.CommandText=sql;c.Parameters.Add("CODIGO_EMPRESA",OracleDbType.Int32).Value=13;c.Parameters.Add("CODIGO_PRESUPUESTO",OracleDbType.Int32).Value=77;c.Parameters.Add("FECHA_DESDE",OracleDbType.Date).Value=new DateTime(2022,7,4);c.Parameters.Add("FECHA_HASTA",OracleDbType.Date).Value=new DateTime(2022,7,4);return c;}
 var results=new List<object>();
 foreach(var item in payload.GetProperty("cases").EnumerateArray()){
  phase=item.GetProperty("Name").GetString()+":guard";
  using var guard=command(item.GetProperty("Guard").GetString()!);var badChild=Convert.ToInt32(await guard.ExecuteScalarAsync());
  phase=item.GetProperty("Name").GetString()+":selector";
  using var cmd=command(item.GetProperty("Query").GetString()!);using var r=await cmd.ExecuteReaderAsync();
  var valid=badChild==0;int lines=0;decimal debe=0,haber=0;var aux=new HashSet<int>();
  var totals=new Dictionary<int,(decimal Debe,decimal Haber)>();
  while(await r.ReadAsync()){
   if(r.GetDecimal(r.GetOrdinal("MIN_CAND"))!=1||r.GetDecimal(r.GetOrdinal("MAX_CAND"))!=1)valid=false;
   var amount=Math.Abs(Math.Round(r.GetDecimal(r.GetOrdinal("MONTO")),2,MidpointRounding.AwayFromZero));
   var major=r.GetString(r.GetOrdinal("NUMERO_MAYOR"));if(major!="300"&&major!="103")valid=false;
   if(amount!=0){lines++;if(major=="300")debe+=amount;else haber+=amount;if(!r.IsDBNull(r.GetOrdinal("CODIGO_AUXILIAR")))aux.Add(r.GetInt32(r.GetOrdinal("CODIGO_AUXILIAR")));}
   var id=r.GetInt32(r.GetOrdinal("CODIGO_COMPROMISO"));var t=totals.GetValueOrDefault(id);if(major=="300")t.Debe+=amount;else t.Haber+=amount;totals[id]=t;
  }
  valid=valid&&totals.Values.All(t=>t.Debe==t.Haber);
  var expected=item.GetProperty("ExpectedValid").GetBoolean();var expectedAux=item.GetProperty("ExpectedAux").EnumerateArray().Select(x=>x.GetInt32()).ToHashSet();
  var passed=valid==expected&&(!valid||(lines==item.GetProperty("ExpectedLines").GetInt32()&&debe==decimal.Parse(item.GetProperty("ExpectedTotal").GetString()!,CultureInfo.InvariantCulture)&&haber==debe&&aux.SetEquals(expectedAux)));
  phase=item.GetProperty("Name").GetString()+":full-block";
  using var full=command(item.GetProperty("FullBlock").GetString()!);
  var msg=full.Parameters.Add("MESSAGE",OracleDbType.Varchar2,4000,null,ParameterDirection.Output);
  var cnt=full.Parameters.Add("LINE_COUNT",OracleDbType.Decimal,ParameterDirection.Output);
  var dd=full.Parameters.Add("DEBE",OracleDbType.Decimal,ParameterDirection.Output);
  var hh=full.Parameters.Add("HABER",OracleDbType.Decimal,ParameterDirection.Output);
  await full.ExecuteNonQueryAsync();
  var status=Convert.ToString(msg.Value);var fullValid=status=="Success";
  var actualCount=((Oracle.ManagedDataAccess.Types.OracleDecimal)cnt.Value).ToInt32();
  var actualD=((Oracle.ManagedDataAccess.Types.OracleDecimal)dd.Value).Value;
  var actualH=((Oracle.ManagedDataAccess.Types.OracleDecimal)hh.Value).Value;
  passed=passed&&fullValid==expected&&(expected?actualCount==item.GetProperty("ExpectedLines").GetInt32()&&actualD==debe&&actualH==haber:actualCount==0);
  results.Add(new{Name=item.GetProperty("Name").GetString(),ExpectedValid=expected,ActualValid=valid,Lines=lines,Debe=debe,Haber=haber,BadChildRows=badChild,FullBlockValid=fullValid,FullBlockLines=actualCount,Message=status,Passed=passed});if(!passed)Environment.ExitCode=1;
 }
 using(var c=cn.CreateCommand()){c.CommandText="ROLLBACK";await c.ExecuteNonQueryAsync();}
 Console.WriteLine("R58_RESULT="+JsonSerializer.Serialize(new{ReadOnlyTransaction=true,SyntheticOnly=true,BusinessTablesRead=false,DdlExecuted=false,WorkTableWritten=false,Results=results}));
}catch(OracleException ex){Console.WriteLine("R58_ORACLE="+phase+":"+ex.Number+":"+ex.Message.Split('\n')[0]);Environment.ExitCode=1;}
catch(Exception ex){Console.WriteLine("R58_ERROR="+ex.GetType().Name);Environment.ExitCode=1;}
'''
def main():
 d,code=run_probe(program,{'cases':cases});d['SourceSha256']=hashlib.sha256(source.encode()).hexdigest();d['Scope']='Real selector, guard and anonymous PL/SQL body. Business sources replaced by DUAL fixtures; work INSERT/DELETE replaced by an in-memory collection. No DDL, real work writes, dispatcher or installed business programs.'
 (ROOT/'Requerimientos/58 - Revision Compronbantes Automaticos/Evidencias/PruebasComp02SinteticoQA.json').write_text(json.dumps(d,indent=2)+'\n')
 print(json.dumps({'Cases':len(d['Results']),'Passed':sum(r['Passed'] for r in d['Results']),'ReadOnly':True,'SyntheticOnly':True,'DdlExecuted':False}))
 raise SystemExit(code)

if __name__ == '__main__':
 main()
