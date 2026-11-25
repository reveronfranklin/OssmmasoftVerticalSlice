from pathlib import Path
import json,subprocess,sys,collections
base=Path(__file__).resolve().parents[3]
req=base.parent/'Requerimientos/57 - Motor Parametrizable De Calculo De Nomina'
paths=[req/'OrdinariaPilotoLote00QA.json']+[req/('OrdinariaPilotoLoteS'+str(n).zfill(4)+'QA.json') for n in range(10,132,2)]
assert all(p.exists() for p in paths),'Captura de poblacion aun incompleta'
raw=[c for f in paths for c in json.loads(f.read_text())['Details']['Cases']]
assert len(raw)==132 and len({c['CaseHash'] for c in raw})==132
missing=[c for c in raw if c['Run'] is None and c['Archive'] is not None]
def run(mode,offset):
 p=subprocess.run([sys.executable,str(base/'scripts/tests/rh-motor-nomina/OrdinariaPilotoQA.py'),mode,str(offset)],capture_output=True,text=True,timeout=285)
 print(p.stdout.strip(),flush=True)
 if p.returncode:print(p.stderr[-400:],flush=True);raise SystemExit(p.returncode)
for offset in range(0,len(missing),2):run('Reencolar',offset)
recovered={c['CaseHash']:c for f in req.glob('OrdinariaPilotoReencolar*QA.json') for c in json.loads(f.read_text())['Details']['Cases'] if c.get('Run') is not None}
assert all(c['CaseHash'] in recovered for c in missing),'Quedan solicitudes sin encolar; repetir recuperacion idempotente'
for offset in range(0,132,10):run('AuditarLote',offset)
audits=[json.loads((req/('OrdinariaPilotoAudit'+str(offset).zfill(4)+'QA.json')).read_text()) for offset in range(0,132,10)]
rows=[c for audit in audits for c in audit['Details']['Cases']]
assert len(rows)==132 and len({c['CaseHash'] for c in rows})==132
counts=dict(collections.Counter(c['State'] for c in rows));reasons=dict(collections.Counter(reason for c in rows for reason in c['SelectionReasons']))
report={'Date':'2026-11-23','Company':13,'Type':10,'Class':'N','Period':5696,'PayrollDate':'2026-11-23','AssignedPersons':137,'Eligible':132,'OutsideEligibility':5,'VacantRelations':191,'AuditedCases':132,'RecoveredQueueRequests':len(missing),'FinalStates':counts,'SourceSelectionReasons':reasons,'Checks':sum(len(a['Checks']) for a in audits),'Cases':rows,'AllWorkerJobsTerminal':all(c['State'] not in ('PENDIENTE','EN_CURSO') for c in rows),'AllMathSucceeded':all(c['State'].startswith('CALCULADO_') for c in rows),'FinancialApplicationEnabled':False,'NoPayrollWrites':True,'HistoricalParityCertified':False,'RequirementComplete':False,'HttpSisAuthenticationTested':False}
(req/'OrdinariaPilotoResultadoFinalQA.json').write_text(json.dumps(report,indent=2)+'\n')
print(json.dumps({k:report[k] for k in ['AuditedCases','FinalStates','SourceSelectionReasons','RecoveredQueueRequests','AllWorkerJobsTerminal','AllMathSucceeded','Checks']}),flush=True)
