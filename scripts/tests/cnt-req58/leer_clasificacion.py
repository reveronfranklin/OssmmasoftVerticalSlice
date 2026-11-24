#!/usr/bin/env python3
"""R58: approved classification only; no amounts or writes; do not infer types from names."""
import json
from collections import defaultdict
from diagnostico import EVIDENCE
from leer_contexto import read

def fetch(name,alias,table,cols,order,filters):
 body={'conexion':alias,'objeto':table,'columnas':cols,'orden':[{'columna':order,'direccion':'asc'}],'filtros':filters,'pageSize':500}
 rows=[]
 for page in range(30):
  result=read(name+'_'+str(page+1),'consultarDatos',body);data=result['datos'];assert not data['truncado']
  rows+=data['filas']
  if not data['hasMore']:return rows
  body['cursor']=result['nextCursor'];assert body['cursor']
 raise RuntimeError('Page bound: '+name)

def in_batches(name,alias,table,cols,key,field,values):
 rows=[];values=sorted(set(values))
 for i in range(0,len(values),100):
  rows+=fetch(name+'_'+str(i),alias,table,cols,key,[{'columna':field,'operador':'in','valor':values[i:i+100]}])
 return list({r[key]:r for r in rows}.values())

headers=json.loads((EVIDENCE/'CabecerasHistoricasQA.json').read_text())['Rows']
byheader={r['CODIGO_COMPROBANTE']:r for r in headers}
cat=json.loads((EVIDENCE/'CatalogosAutorizadosQA.json').read_text())['data']['resultado']['datos']['filas']
origins={r['DESCRIPCION_ID']:r['CODIGO'] for r in cat if r['TITULO_ID']=='1' and r['CODIGO_EMPRESA'] in [None,'13']}
refs=defaultdict(set)
for path in EVIDENCE.glob('ProyeccionDetalle_*.json'):
 for r in json.loads(path.read_text())['data']['resultado']['datos']['filas']:
  h=byheader[r['CODIGO_COMPROBANTE']]
  if r['REFERENCIA1']:refs[origins.get(h['ORIGEN_ID'],'?')].add(r['REFERENCIA1'])
pre=in_batches('ClasificacionComp','PRE','PRE_COMPROMISOS',
 ['CODIGO_COMPROMISO','NUMERO_COMPROMISO','CODIGO_SOLICITUD','CODIGO_PRESUPUESTO','FECHA_COMPROMISO','STATUS','CODIGO_EMPRESA'],
 'CODIGO_COMPROMISO','NUMERO_COMPROMISO',refs['COMP']|refs['ANCOMP'])
op=in_batches('ClasificacionOp','ADM','ADM_ORDEN_PAGO',
 ['CODIGO_ORDEN_PAGO','NUMERO_ORDEN_PAGO','TIPO_ORDEN_PAGO_ID','CODIGO_PRESUPUESTO','FECHA_ORDEN_PAGO','STATUS','CODIGO_EMPRESA'],
 'CODIGO_ORDEN_PAGO','NUMERO_ORDEN_PAGO',refs['ODPAUT']|refs['ANODPAUT'])
sol=in_batches('ClasificacionSol','ADM','ADM_SOLICITUDES',['CODIGO_SOLICITUD','TIPO_SOLICITUD_ID','CODIGO_EMPRESA'],
 'CODIGO_SOLICITUD','CODIGO_SOLICITUD',[r['CODIGO_SOLICITUD'] for r in pre])
titles=fetch('ClasificacionTitulos','ADM','ADM_TITULOS',['TITULO_ID','CODIGO','TITULO','CODIGO_EMPRESA'],'TITULO_ID',[])
applicable=[r for r in titles if r['CODIGO_EMPRESA'] in [None,'13']]
tipids=[r['TITULO_ID'] for r in applicable if r['CODIGO'] in ['TIP_SOLI','TSC']]
desc=in_batches('ClasificacionCatalogo','ADM','ADM_DESCRIPTIVAS',['DESCRIPCION_ID','TITULO_ID','CODIGO','DESCRIPCION','EXTRA3','CODIGO_EMPRESA'],
 'DESCRIPCION_ID','TITULO_ID',tipids)
used=[r['TIPO_SOLICITUD_ID'] for r in sol]+[r['TIPO_ORDEN_PAGO_ID'] for r in op]
if used:
 desc+=in_batches('ClasificacionTiposUsados','ADM','ADM_DESCRIPTIVAS',['DESCRIPCION_ID','TITULO_ID','CODIGO','DESCRIPCION','EXTRA3','CODIGO_EMPRESA'],
 'DESCRIPCION_ID','DESCRIPCION_ID',[x for x in used if x is not None])
desc=list({r['DESCRIPCION_ID']:r for r in desc if r['CODIGO_EMPRESA'] in [None,'13']}.values())
report={'Company':13,'AmountsRead':False,'SnapshotAcrossRequests':False,'Scope':'Sources whose numbers appear in previously inventoried visible details. Numbers may repeat across budgets.',
 'ReferenceCounts':{k:len(v) for k,v in refs.items()},'Commitments':pre,'PaymentOrders':op,'Requests':sol,'ApplicableTitles':applicable,'ApplicableDescriptions':desc}
(EVIDENCE/'ClasificacionAdmPreQA.json').write_text(json.dumps(report,indent=2)+'\n')
print(json.dumps({'Commitments':len(pre),'PaymentOrders':len(op),'Requests':len(sol),'ApplicableTitles':len(applicable),'ApplicableDescriptions':len(desc),'ReferenceCounts':report['ReferenceCounts']}))
