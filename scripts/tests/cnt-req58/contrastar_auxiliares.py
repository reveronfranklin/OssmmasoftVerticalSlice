#!/usr/bin/env python3
"""R58: read approved company13 auxiliaries for the already inventoried detail IDs."""
import json
from collections import Counter
from diagnostico import EVIDENCE
from leer_contexto import read

rows=json.loads((EVIDENCE/'DetallesHistoricosQA.json').read_text())['Rows']
ids=sorted({int(r['CODIGO_AUXILIAR']) for r in rows if r['CODIGO_AUXILIAR'] is not None})
aux=[]
for i in range(0,len(ids),100):
 body={'conexion':'CNT','objeto':'CNT_AUXILIARES','columnas':['CODIGO_AUXILIAR','CODIGO_MAYOR','CODIGO_PROVEEDOR','CODIGO_EMPRESA','FECHA_FIN_VIGENCIA'],
       'filtros':[{'columna':'CODIGO_AUXILIAR','operador':'in','valor':ids[i:i+100]}],
       'orden':[{'columna':'CODIGO_AUXILIAR','direccion':'asc'}],'pageSize':500}
 data=read('AuxiliaresDetalle_'+str(i),'consultarDatos',body)['datos']
 assert not data['hasMore'] and not data['truncado']
 aux+=data['filas']
byid={r['CODIGO_AUXILIAR']:r for r in aux};assert len(byid)==len(aux)
missing=[];different=[];matching=[]
for r in rows:
 a=byid.get(r['CODIGO_AUXILIAR'])
 if a is None:missing.append(r)
 elif a['CODIGO_MAYOR']!=r['CODIGO_MAYOR']:different.append({'Detalle':r,'Auxiliar':a})
 else:matching.append(r)
report={'Company':13,'DetailRows':len(rows),'UniqueAuxiliaryIds':len(ids),'AuxiliariesVisible':len(aux),
        'MatchingMajorRows':len(matching),'MissingAuxiliaryWithinCompanyRows':missing,'DifferentMajorRows':different,
        'MissingByDetailMajor':dict(Counter(r['CODIGO_MAYOR'] for r in missing)),
        'Limitation':'Missing in company13 does not prove global absence. Reads across calls are not one snapshot.'}
(EVIDENCE/'IntegridadMayorAuxiliarQA.json').write_text(json.dumps(report,indent=2)+'\n')
print(json.dumps({'Details':len(rows),'UniqueAuxiliaries':len(ids),'Matching':len(matching),'MissingWithinCompany':len(missing),'DifferentMajor':len(different),'MissingByMajor':report['MissingByDetailMajor']}))
