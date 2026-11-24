#!/usr/bin/env python3
"""Read approved company13 details for the previously inventoried headers; no generation."""
import json
from decimal import Decimal
from collections import defaultdict
from leer_contexto import read
from diagnostico import EVIDENCE

headers=json.loads((EVIDENCE/'CabecerasHistoricasQA.json').read_text())['Rows']
ids=[h['CODIGO_COMPROBANTE'] for h in headers]
rows=[]
for batch in range(0,len(ids),100):
    request={'conexion':'CNT','objeto':'CNT_DETALLE_COMPROBANTE','pageSize':500,
        'columnas':['CODIGO_DETALLE_COMPROBANTE','CODIGO_COMPROBANTE','CODIGO_MAYOR','CODIGO_AUXILIAR','MONTO','CODIGO_EMPRESA'],
        'filtros':[{'columna':'CODIGO_COMPROBANTE','operador':'in','valor':ids[batch:batch+100]}],
        'orden':[{'columna':'CODIGO_DETALLE_COMPROBANTE','direccion':'asc'}]}
    for page in range(100):
        data=read('DetallesHistoricos_'+str(batch)+'_'+str(page),'consultarDatos',request)
        assert not data['datos']['truncado']
        rows+=data['datos']['filas']
        if not data['datos']['hasMore']:break
        request['cursor']=data['nextCursor'];assert request['cursor']
    else:raise RuntimeError('Limite de paginas alcanzado')
    print(json.dumps({'HeaderBatchStart':batch,'DetailsRead':len(rows)}),flush=True)
assert len({r['CODIGO_DETALLE_COMPROBANTE'] for r in rows})==len(rows)
(EVIDENCE/'DetallesHistoricosQA.json').write_text(json.dumps({'Company':13,'Rows':rows,'Headers':len(headers),'Scope':'Only company13 children of inventoried headers','SnapshotBetweenPages':False,'CompleteWithinScope':True},indent=2)+'\n')
groups=defaultdict(list)
for r in rows:groups[r['CODIGO_COMPROBANTE']].append(r)
summary=[]
for h in headers:
    ds=groups[h['CODIGO_COMPROBANTE']];a=[Decimal(r['MONTO']) for r in ds]
    summary.append({**h,'DetallesVisiblesEmpresa':len(ds),'Debe':str(-sum((x for x in a if x<0),Decimal(0))),'Haber':str(sum((x for x in a if x>0),Decimal(0)))})
(EVIDENCE/'ResumenHistoricosQA.json').write_text(json.dumps({'Rows':summary,'HeadersWithVisibleDetails':sum(bool(x['DetallesVisiblesEmpresa']) for x in summary),'HeadersWithoutVisibleDetails':sum(not x['DetallesVisiblesEmpresa'] for x in summary),'VisibleDetails':len(rows)},indent=2)+'\n')
print(json.dumps({'Headers':len(headers),'VisibleDetails':len(rows),'WithVisibleDetails':sum(bool(x['DetallesVisiblesEmpresa']) for x in summary)}))
