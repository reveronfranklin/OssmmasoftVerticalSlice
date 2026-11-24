#!/usr/bin/env python3
"""Offline comparison of approved source snapshot, saved CNT detail and installed view."""
import json
from collections import Counter,defaultdict
from decimal import Decimal,ROUND_HALF_UP
from diagnostico import EVIDENCE as E
read=lambda name:json.loads((E/name).read_text())
d=read('ConciliacionFuentesQA.json');rs={x['Name']:x['Rows'] for x in d['Results']}
headers={r['CODIGO_COMPROBANTE']:r for r in read('CabecerasHistoricasQA.json')['Rows']}
details=[]
for path in E.glob('ProyeccionDetalle_*.json'):details+=json.loads(path.read_text())['data']['resultado']['datos']['filas']
window=lambda value:value[:7] in ['2022-07','2023-01']
expected=[r for r in details if window(headers[r['CODIGO_COMPROBANTE']]['FECHA_COMPROBANTE']) and headers[r['CODIGO_COMPROBANTE']]['ESTADO_CMP']=='FINALIZADO']
key=lambda r:(r['CODIGO_COMPROBANTE'],r['CODIGO_MAYOR'],r['CODIGO_AUXILIAR'],r['REFERENCIA1'],r['REFERENCIA2'],Decimal(r['MONTO']))
a=Counter(map(key,expected));b=Counter(map(key,rs['MayorMuestras']))
missing=list((a-b).elements());extra=list((b-a).elements())
# Match commitments by number, exact day and resolved budget; never by number alone.
source=defaultdict(list)
for r in rs['ImportesCompromisos']:source[(r['NUMERO_COMPROMISO'],r['FECHA_COMPROMISO'][:10],r['CODIGO_PRESUPUESTO'])].append(r)
budgets=rs['PresupuestosMuestra'];comparison=[]
for hid,h in headers.items():
 if h['ORIGEN_ID']!='125' or not window(h['FECHA_COMPROBANTE']):continue
 ds=[r for r in expected if r['CODIGO_COMPROBANTE']==hid]
 if not ds:continue
 date=h['FECHA_COMPROBANTE'][:10]
 budget=[p['CODIGO_PRESUPUESTO'] for p in budgets if p['FECHA_DESDE'][:10]<=date<=p['FECHA_HASTA'][:10]]
 assert len(budget)==1
 for ref in sorted({r['REFERENCIA1'] for r in ds}):
  ss=source.get((ref,date,budget[0]),[])
  recorded=defaultdict(Decimal)
  for r in ds:
   if r['REFERENCIA1']==ref:recorded[r['CODIGO_MAYOR']]+=Decimal(r['MONTO'])
  amount=sum((Decimal(r['MONTO']) for r in ss),Decimal(0)).quantize(Decimal('.01'),rounding=ROUND_HALF_UP)
  comparison.append({'Header':hid,'Number':h['NUMERO_COMPROBANTE'],'Date':date,'Reference':ref,'Budget':budget[0],
   'SourceRows':len(ss),'SourceDocuments':len({r['CODIGO_COMPROMISO'] for r in ss}),'SourceAmount':str(amount),
   'RecordedMajor3':str(recorded['3']),'RecordedMajor9':str(recorded['9']),
   'MagnitudeMatches':bool(ss) and abs(recorded['3'])==abs(amount) and abs(recorded['9'])==abs(amount),
   'MatchesDocumentedSign':recorded['9']==-abs(amount) and recorded['3']==abs(amount)})
viewtext=rs['DefinicionMayor'][0]['TEXT'];(E/'VistaMayorInstaladaQA.txt').write_text(viewtext+'\n')
report={'Company':13,'Windows':['2022-07','2023-01'],'SavedDetailRows':len(expected),'InstalledViewRows':len(rs['MayorMuestras']),
 'MissingCount':len(missing),'UnexpectedCount':len(extra),'MissingKeys':[list(map(str,k)) for k in missing],
 'UnexpectedKeys':[list(map(str,k)) for k in extra],'DetailMajor3Rows':sum(r['CODIGO_MAYOR']=='3' for r in expected),
 'ViewMajor3Rows':sum(r['CODIGO_MAYOR']=='3' for r in rs['MayorMuestras']),'CommitmentComparison':comparison,
 'FullUiOrPaginationCertified':False,'HistoricalWritesPerformed':False,
 'Limitations':['CNT detail capture is earlier than the source/view snapshot; no assertion of an atomic snapshot across those files.',
 'No rows from original screenshot headers; they remain empty. Magnitude parity is not sign parity or full acceptance.']}
(E/'ContrasteFuentesMayorQA.json').write_text(json.dumps(report,indent=2)+'\n')
print(json.dumps({'SavedDetails':len(expected),'ViewRows':len(rs['MayorMuestras']),'Missing':len(missing),'Unexpected':len(extra),
 'Major3InDetail':report['DetailMajor3Rows'],'Major3InView':report['ViewMajor3Rows'],
 'CommitmentsCompared':len(comparison),'MagnitudeMatches':sum(x['MagnitudeMatches'] for x in comparison),
 'DocumentedSignMatches':sum(x['MatchesDocumentedSign'] for x in comparison)}))
