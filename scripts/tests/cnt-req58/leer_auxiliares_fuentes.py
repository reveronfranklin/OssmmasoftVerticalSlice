#!/usr/bin/env python3
"""Use the already approved CNT company13 policy for source supplier auxiliaries."""
import json
from diagnostico import EVIDENCE
from leer_contexto import read
source=json.loads((EVIDENCE/'ConciliacionFuentesQA.json').read_text())
pre=next(x['Rows'] for x in source['Results'] if x['Name']=='ImportesCompromisos')
ids=sorted({r['CODIGO_PROVEEDOR'] for r in pre})
body={'conexion':'CNT','objeto':'CNT_AUXILIARES','pageSize':500,'columnas':['CODIGO_AUXILIAR','CODIGO_MAYOR','CODIGO_PROVEEDOR','CODIGO_EMPRESA','SEGMENTO1','SEGMENTO2','DENOMINACION'],
 'filtros':[{'columna':'CODIGO_PROVEEDOR','operador':'in','valor':ids}],
 'orden':[{'columna':'CODIGO_AUXILIAR','direccion':'asc'}]}
assert len(ids)<=100
rows=[]
for page in range(20):
 d=read('AuxiliaresFuentes_'+str(page+1),'consultarDatos',body);assert not d['datos']['truncado']
 rows+=d['datos']['filas']
 if not d['datos']['hasMore']:break
 body['cursor']=d['nextCursor'];assert body['cursor']
else:raise RuntimeError('Page bound')
# Include auxiliaries referenced by configured maps even if not linked to a supplier.
maps=read('AuxPucConciliacion','consultarDatos',{'conexion':'CNT','objeto':'CNT_AUXILIARES_PUC','pageSize':500,'columnas':['CODIGO_AUXILIAR_PUC','CODIGO_AUXILIAR','CODIGO_PUC','TIPO_DOCUMENTO_ID','CODIGO_EMPRESA'],'orden':[{'columna':'CODIGO_AUXILIAR_PUC','direccion':'asc'}]})['datos']
assert not maps['hasMore'] and not maps['truncado']
extra=sorted({r['CODIGO_AUXILIAR'] for r in maps['filas']}-{r['CODIGO_AUXILIAR'] for r in rows})
for batch in range(0,len(extra),100):
 req={**body,'filtros':[{'columna':'CODIGO_AUXILIAR','operador':'in','valor':extra[batch:batch+100]}]};req.pop('cursor',None)
 d=read('AuxiliaresMapeados_'+str(batch),'consultarDatos',req)['datos'];assert not d['hasMore'] and not d['truncado'];rows+=d['filas']
(EVIDENCE/'AuxiliaresFuentesCompQA.json').write_text(json.dumps({'Company':13,'SupplierIds':ids,'Rows':rows,'Maps':maps['filas'],'SingleSnapshotWithSources':False},indent=2)+'\n')
print(json.dumps({'Suppliers':len(ids),'Auxiliaries':len(rows),'Maps':len(maps['filas'])}))
