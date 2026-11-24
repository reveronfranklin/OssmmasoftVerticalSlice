#!/usr/bin/env python3
"""Read-only QA join: child scope inherited exclusively from the approved company-13 sample headers."""
from pathlib import Path
import json, subprocess, shlex
root=Path(__file__).resolve().parents[4]
program=r"""
using System.Text.Json;
using System.Data;
using Microsoft.Extensions.Configuration;
using Oracle.ManagedDataAccess.Client;
using OssmmasoftVerticalSlice.ContextDB;
string phase="configuration";try{
var cfg=new ConfigurationBuilder().AddJsonFile("/srv/ossmmasoft-pruebas/shared/config/vertical.json").Build();
if(cfg.GetValue<int>("settings:EmpresaConfig")!=13)throw new Exception("Company mismatch");
using var cn=new ConnectionDB(cfg).GetCntConnection();await cn.OpenAsync();
using(var c=cn.CreateCommand()){c.CommandText="SELECT SYS_CONTEXT('USERENV','DB_NAME'),SYS_CONTEXT('USERENV','SERVER_HOST'),SYS_CONTEXT('USERENV','SESSION_USER') FROM DUAL";using var r=await c.ExecuteReaderAsync();await r.ReadAsync();if(r.GetString(0)!="XE"||r.GetString(1)!="6d88ba053190"||r.GetString(2)!="CNT")throw new Exception("Identity mismatch");}
phase="read-only";using(var c=cn.CreateCommand()){c.CommandText="ROLLBACK";await c.ExecuteNonQueryAsync();}
using(var c=cn.CreateCommand()){c.CommandText="SET TRANSACTION READ ONLY";await c.ExecuteNonQueryAsync();}
phase="sample-query";var result=new List<Dictionary<string,string?>>();
using(var c=cn.CreateCommand()){
 c.BindByName=true;c.CommandTimeout=20;
 c.CommandText="SELECT C.CODIGO_COMPROBANTE,C.NUMERO_COMPROBANTE,D.CODIGO_DETALLE_COMPROBANTE,D.CODIGO_EMPRESA,D.CODIGO_MAYOR,D.CODIGO_AUXILIAR,TO_CHAR(D.MONTO,'TM9','NLS_NUMERIC_CHARACTERS=''.,''') MONTO,D.REFERENCIA1,D.REFERENCIA2 FROM CNT.CNT_COMPROBANTES C LEFT JOIN CNT.CNT_DETALLE_COMPROBANTE D ON D.CODIGO_COMPROBANTE=C.CODIGO_COMPROBANTE AND (D.CODIGO_EMPRESA=C.CODIGO_EMPRESA OR D.CODIGO_EMPRESA IS NULL) WHERE C.CODIGO_EMPRESA=:emp AND C.CODIGO_COMPROBANTE IN (:a,:b,:c) AND C.NUMERO_COMPROBANTE IN ('22-07-02-001','22-07-02-002','22-07-02-003') ORDER BY C.CODIGO_COMPROBANTE,D.CODIGO_DETALLE_COMPROBANTE";
 c.Parameters.Add("emp",OracleDbType.Int32).Value=13;c.Parameters.Add("a",OracleDbType.Int32).Value=9052;c.Parameters.Add("b",OracleDbType.Int32).Value=9075;c.Parameters.Add("c",OracleDbType.Int32).Value=9082;
 using var r=await c.ExecuteReaderAsync();while(await r.ReadAsync()){if(result.Count>=100)throw new Exception("Row limit");var row=new Dictionary<string,string?>();for(int i=0;i<r.FieldCount;i++)row[r.GetName(i)]=r.IsDBNull(i)?null:Convert.ToString(r.GetValue(i),System.Globalization.CultureInfo.InvariantCulture);result.Add(row);}
}
var counts=new List<Dictionary<string,string?>>();
using(var c=cn.CreateCommand()){c.BindByName=true;c.CommandTimeout=20;c.CommandText="SELECT C.NUMERO_COMPROBANTE,COUNT(D.CODIGO_DETALLE_COMPROBANTE) TOTAL_DETALLES,SUM(CASE WHEN D.CODIGO_EMPRESA=C.CODIGO_EMPRESA THEN 1 ELSE 0 END) EMPRESA_COINCIDE,SUM(CASE WHEN D.CODIGO_DETALLE_COMPROBANTE IS NOT NULL AND D.CODIGO_EMPRESA IS NULL THEN 1 ELSE 0 END) EMPRESA_NULA,SUM(CASE WHEN D.CODIGO_EMPRESA<>C.CODIGO_EMPRESA THEN 1 ELSE 0 END) EMPRESA_DISTINTA FROM CNT.CNT_COMPROBANTES C LEFT JOIN CNT.CNT_DETALLE_COMPROBANTE D ON D.CODIGO_COMPROBANTE=C.CODIGO_COMPROBANTE WHERE C.CODIGO_EMPRESA=:emp AND C.CODIGO_COMPROBANTE IN (9052,9075,9082) GROUP BY C.NUMERO_COMPROBANTE ORDER BY C.NUMERO_COMPROBANTE";c.Parameters.Add("emp",OracleDbType.Int32).Value=13;using var r=await c.ExecuteReaderAsync();while(await r.ReadAsync()){var row=new Dictionary<string,string?>();for(int i=0;i<r.FieldCount;i++)row[r.GetName(i)]=r.IsDBNull(i)?null:Convert.ToString(r.GetValue(i),System.Globalization.CultureInfo.InvariantCulture);counts.Add(row);}}
using(var c=cn.CreateCommand()){c.CommandText="ROLLBACK";await c.ExecuteNonQueryAsync();}
Console.WriteLine("R58_RESULT="+JsonSerializer.Serialize(new{ReadOnlyTransaction=true,Company=13,Scope="Sample header company13; child company13 or null only",OtherCompanyDetailsIncluded=false,Rows=result,CountsByHeader=counts}));
}catch(OracleException ex){Console.WriteLine("R58_ORACLE="+phase+":"+ex.Number);Environment.ExitCode=1;}catch(Exception ex){Console.WriteLine("R58_ERROR="+phase+":"+ex.GetType().Name);Environment.ExitCode=1;}
"""
remote=r'''import tempfile,pathlib,subprocess,sys,json,shutil,os
p=pathlib.Path(tempfile.mkdtemp(prefix='r58-readonly-'))
try:
 data=json.load(sys.stdin);runtime=pathlib.Path('/srv/ossmmasoft-pruebas/current/vertical').resolve();assert runtime.name=='r57-prenomina-txn-20261124T091632Z'
 refs=''.join('<Reference Include="'+name+'"><HintPath>'+str(runtime/(name+'.dll'))+'</HintPath></Reference>' for name in ['OssmmasoftVerticalSlice','Oracle.ManagedDataAccess','Microsoft.Data.SqlClient'])
 (p/'Probe.csproj').write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable></PropertyGroup><ItemGroup><FrameworkReference Include="Microsoft.AspNetCore.App"/>'+refs+'</ItemGroup></Project>')
 (p/'Program.cs').write_text(data['program']);r=subprocess.run(['dotnet','run','--project',str(p/'Probe.csproj'),'--verbosity','quiet'],capture_output=True,text=True,timeout=100,env={**os.environ,'TZ':'America/Caracas'})
 if r.returncode:print('R58_FAILED='+str(r.returncode));print('\n'.join(x for x in r.stdout.splitlines() if x.startswith('R58_')));print('\n'.join(x for x in r.stdout.splitlines() if ': error CS' in x));sys.exit(r.returncode)
 print('\n'.join(x for x in r.stdout.splitlines() if x.startswith('R58_RESULT=')))
finally:shutil.rmtree(p)
'''
r=subprocess.run(['ssh','-i',str(Path.home()/'.ssh/ubuntu_pruebas'),'-o','BatchMode=yes','-o','ConnectTimeout=10','ossmma@74.121.190.6','python3 -c '+shlex.quote(remote)],input=json.dumps({'program':program}),capture_output=True,text=True,timeout=120)
if r.returncode or 'R58_RESULT=' not in r.stdout:raise SystemExit(r.stdout[:1500] or r.stderr[:1000])
d=json.loads(r.stdout.split('R58_RESULT=')[-1]);(root/'Requerimientos/58 - Revision Compronbantes Automaticos/Evidencias/DetalleLegacyPorCabeceraQA.json').write_text(json.dumps(d,indent=2)+'\n');print(json.dumps({'ReadOnly':True,'Rows':len(d['Rows']),'CountsByHeader':d['CountsByHeader'],'LegacyNullCompanyRows':sum(x['CODIGO_DETALLE_COMPROBANTE'] is not None and x['CODIGO_EMPRESA'] is None for x in d['Rows'])}))
