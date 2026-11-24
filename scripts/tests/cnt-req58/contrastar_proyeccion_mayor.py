#!/usr/bin/env python3
"""R58: count distinct persisted lines lost by the local view's UNION projection."""
import json
from collections import defaultdict,Counter
from decimal import Decimal
from diagnostico import EVIDENCE
from leer_contexto import read
base=json.loads((EVIDENCE/'DetallesHistoricosQA.json').read_text())['Rows']
ids=sorted({int(r['CODIGO_DETALLE_COMPROBANTE']) for r in base})
rows=[]
for i in range(0,len(ids),100):
 body={'conexion':'CNT','objeto':'CNT_DETALLE_COMPROBANTE',
       'columnas':['CODIGO_DETALLE_COMPROBANTE','CODIGO_COMPROBANTE','CODIGO_MAYOR','CODIGO_AUXILIAR','MONTO','REFERENCIA1','REFERENCIA2','DESCRIPCION','CODIGO_EMPRESA'],
       'filtros':[{'columna':'CODIGO_DETALLE_COMPROBANTE','operador':'in','valor':ids[i:i+100]}],
       'orden':[{'columna':'CODIGO_DETALLE_COMPROBANTE','direccion':'asc'}],'pageSize':500}
 data=read('ProyeccionDetalle_'+str(i),'consultarDatos',body)['datos']
 assert not data['hasMore'] and not data['truncado']
 rows+=data['filas']
assert {int(r['CODIGO_DETALLE_COMPROBANTE']) for r in rows}==set(ids),'Data changed; inventory again'
headers=json.loads((EVIDENCE/'ResumenHistoricosQA.json').read_text())['Rows']
final={h['CODIGO_COMPROBANTE'] for h in headers if h['ESTADO_CMP']=='FINALIZADO'}
groups=defaultdict(list)
for r in rows:
 if r['CODIGO_COMPROBANTE'] not in final:continue
 fields=['CODIGO_COMPROBANTE','CODIGO_MAYOR','CODIGO_AUXILIAR','REFERENCIA1','REFERENCIA2','DESCRIPCION','CODIGO_EMPRESA']
 key=tuple(r[x] for x in fields)+(Decimal(r['MONTO']),)
 groups[key].append(r)
duplicates=[{'DetalleIds':[r['CODIGO_DETALLE_COMPROBANTE'] for r in group],
             'Cabecera':group[0]['CODIGO_COMPROBANTE'],'Mayor':group[0]['CODIGO_MAYOR'],
             'LineasColapsadas':len(group)-1,'MontoCadaLinea':group[0]['MONTO']} for group in groups.values() if len(group)>1]
lost=Counter()
for group in duplicates:lost[group['Mayor']]+=group['LineasColapsadas']
report={'Company':13,'Source':'975 previously inventoried details; finalized headers only',
        'InstalledViewDefinitionVerified':False,'LocalProjectionUsesUnion':True,
        'ProjectedRows':sum(len(g) for g in groups.values()),'DistinctRows':len(groups),
        'CollapsedLines':sum(x['LineasColapsadas'] for x in duplicates),'CollapsedByMajor':dict(lost),
        'Groups':duplicates,'Limitation':'Models local projection. Does not certify installed view or claim original screenshot account disappearance is explained.'}
(EVIDENCE/'ProyeccionMayorQA.json').write_text(json.dumps(report,indent=2)+'\n')
print(json.dumps({k:v for k,v in report.items() if k not in ['Groups','Limitation']}))
