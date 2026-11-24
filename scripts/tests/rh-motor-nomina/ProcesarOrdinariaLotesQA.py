from pathlib import Path
import json,subprocess,sys
base=Path(__file__).resolve().parents[3]
req=base.parent/'Requerimientos/57 - Motor Parametrizable De Calculo De Nomina'
pop=json.loads((req/'OrdinariaPilotoPoblacionQA.json').read_text())['Details']
assert pop['Type']==10 and pop['Date']=='2026-11-23' and pop['Class']=='N' and pop['EligibleSingleCargo']==132
for start in range(10,132,2):
 target=req/('OrdinariaPilotoLoteS'+str(start).zfill(4)+'QA.json')
 if target.exists():
  saved=json.loads(target.read_text());d=saved['Details'];assert saved['Status']=='OK' and d['PopulationHash']==pop['PopulationHash'] and d['BatchStart']==start
  print(json.dumps({'Start':start,'RecoveredEvidence':True,'Cases':len(d['Cases']),'Counts':d['Counts']}),flush=True)
  continue
 run=subprocess.run([sys.executable,str(base/'scripts/tests/rh-motor-nomina/OrdinariaPilotoQA.py'),'LotePequeno',str(start)],capture_output=True,text=True,timeout=285)
 if run.returncode:
  print(json.dumps({'StoppedAtStart':start,'PreviousBatchesPreserved':True,'Failure':run.stdout[-1800:]+run.stderr[-400:]}),flush=True);raise SystemExit(run.returncode)
 print(run.stdout.strip(),flush=True)
print(json.dumps({'AllBatchEvidenceWritten':True,'ExpectedCases':132,'FinalWorkerAuditStillRequired':True}),flush=True)
