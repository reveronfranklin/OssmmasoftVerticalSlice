#!/usr/bin/env python3
"""Verify full filtered monetary totals and pagination using local PL/SQL in QA; no DDL."""
from pathlib import Path
import ast,json
import oracle_sintetico as oracle
ROOT=Path(__file__).resolve().parents[4]
sql=(ROOT/'OssmmasoftVerticalSlice/Features/Contabilidad/Sql/SP_CNT_CMP_GET_ALL.sql').read_text().strip().removesuffix('/').strip()
body=sql.replace('CREATE OR REPLACE PROCEDURE CNT.SP_CNT_CMP_GET_ALL (','PROCEDURE local_list (',1);assert body!=sql
statement='DECLARE\n'+body+'\nBEGIN local_list(:sz,:pg,:search,:period,:origin,:fd,:fh,:auto,:company,:rows,:msg,:total,:pages,:state,:type,:debeTotal,:haberTotal); END;'
tree=ast.parse((Path(__file__).parent/'probar_filtro_tipo.py').read_text());program=next(n.value.value for n in tree.body if isinstance(n,ast.Assign) and any(isinstance(t,ast.Name) and t.id=='program' for t in n.targets))
a=program.index('var cases=');b=program.index('var results=',a)
program=program[:a]+'''var cases=new List<(int? Type,int? Origin,int? Period,string? Search,string? State,int? Automatic,DateTime? From,DateTime? Until)>();
cases.Add((null,null,null,null,null,null,null,null));foreach(var t in types)cases.Add((t,null,null,null,null,null,null,null));
cases.Add((-999,null,null,null,null,null,null,null));cases.Add((18,125,198,null,"FINALIZADO",1,null,null));
cases.Add((18,125,null,null,"FINALIZADO",1,new DateTime(2023,1,1),new DateTime(2023,1,31)));
cases.Add((18,125,null,"23-01",null,null,new DateTime(2023,1,1),new DateTime(2023,1,31)));
cases.Add((19,125,null,null,null,null,null,null));cases.Add((null,125,198,null,null,null,null,null));
'''+program[b:]
program=program.replace('int size=test.Period.HasValue?10:1000;', 'int size=test.From.HasValue?2:test.Period.HasValue?10:1000;decimal expectedDebe=0,expectedHaber=0,rowDebe=0,rowHaber=0;')
program=program.replace('SELECT C.CODIGO_COMPROBANTE FROM CNT.CNT_COMPROBANTES C WHERE', 'SELECT C.CODIGO_COMPROBANTE,NVL(SUM(CASE WHEN D.MONTO>0 THEN D.MONTO ELSE 0 END),0),NVL(SUM(CASE WHEN D.MONTO<0 THEN -D.MONTO ELSE 0 END),0) FROM CNT.CNT_COMPROBANTES C LEFT JOIN CNT.CNT_DETALLE_COMPROBANTE D ON D.CODIGO_COMPROBANTE=C.CODIGO_COMPROBANTE AND D.CODIGO_EMPRESA=13 WHERE')
program=program.replace('C.CODIGO_EMPRESA=13 AND (:t', 'C.CODIGO_EMPRESA=13 AND (:fd IS NULL OR C.FECHA_COMPROBANTE>=:fd) AND (:fh IS NULL OR C.FECHA_COMPROBANTE<:fh+1) AND (:t')
needle='c.Parameters.Add("t",OracleDbType.Int32).Value='
program=program.replace(needle, 'c.CommandText+=" GROUP BY C.CODIGO_COMPROBANTE";c.Parameters.Add("fd",OracleDbType.Date).Value=(object?)test.From??DBNull.Value;c.Parameters.Add("fh",OracleDbType.Date).Value=(object?)test.Until??DBNull.Value;'+needle)
program=program.replace('expected.Add(r.GetInt32(0));','expected.Add(r.GetInt32(0));expectedDebe+=r.GetDecimal(1);expectedHaber+=r.GetDecimal(2);')
program=program.replace('page<=Math.Max(totalPages,1);', 'page<=Math.Max(totalPages,1)+1;')
program=program.replace('add("fd",OracleDbType.Date,null);add("fh",OracleDbType.Date,null);','add("fd",OracleDbType.Date,test.From);add("fh",OracleDbType.Date,test.Until);')
program=program.replace('rows++;if(test.Type.HasValue', 'rows++;rowDebe+=r.GetDecimal(r.GetOrdinal("TOTAL_DEBE"));rowHaber+=r.GetDecimal(r.GetOrdinal("TOTAL_HABER"));if(test.Type.HasValue')
program=program.replace('if(total!=expected.Count', 'if(((OracleDecimal)sumDebe.Value).Value!=expectedDebe||((OracleDecimal)sumHaber.Value).Value!=expectedHaber)throw new Exception("Whole-query monetary total mismatch");if(page>totalPages&&rows!=0)throw new Exception("Out-of-range page must be empty");if(total!=expected.Count')
program=program.replace('if(!actual.SetEquals(expected))', 'if(rowDebe!=expectedDebe||rowHaber!=expectedHaber)throw new Exception("Paged row totals differ from query totals");if(!actual.SetEquals(expected))')
program=program.replace('test.Automatic,Rows=', 'test.Automatic,test.From,test.Until,TotalDebe=expectedDebe,TotalHaber=expectedHaber,Rows=')
oracle.REMOTE=oracle.REMOTE.replace("runtime.name=='r57-prenomina-txn-20261124T091632Z'","runtime.name=='r58-catalogos-api-20261124T232218Z'")
if __name__ == "__main__":
    d,code=oracle.run_probe(program,{'Statement':statement});assert code==0
    (ROOT/'Requerimientos/58 - Revision Compronbantes Automaticos/Evidencias/TotalesConsultaSoloLecturaQA.json').write_text(json.dumps(d,indent=2)+'\n');print(json.dumps(d))
