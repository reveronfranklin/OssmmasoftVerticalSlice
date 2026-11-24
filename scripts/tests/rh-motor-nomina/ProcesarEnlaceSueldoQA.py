"""Read-only QA audit of the salary-to-recalculation link over the reviewed pilot population."""
import concurrent.futures
import hashlib
import json
from pathlib import Path
import subprocess
import sys

root=Path(__file__).resolve().parents[3]
req=root.parent/'Requerimientos/57 - Motor Parametrizable De Calculo De Nomina'

def audit(start):
    target=req/f'OrdinariaPilotoEnlaceEtapa{start:04d}QA.json'
    result=subprocess.run([sys.executable,str(Path(__file__).with_name('OrdinariaPilotoQA.py')),'EnlaceEtapa',str(start)],cwd=root,capture_output=True,text=True,timeout=290)
    if result.returncode:
        raise RuntimeError(f'Batch {start}: {result.stdout[:1800]}')
    data=json.loads(target.read_text())
    assert data['Status']=='OK'
    return start,data

# Each batch only reads its immutable captures and revisions. No DB writes.
results=[]
with concurrent.futures.ThreadPoolExecutor(max_workers=2) as pool:
    for start,data in pool.map(audit,range(0,132,10)):
        results.append(data)
        print(json.dumps({'Batch':start,'Cases':len(data['Details']['Cases']),'Counts':data['Details']['Counts']}),flush=True)
cases=[c for d in results for c in d['Details']['Cases']]
expected=json.loads((req/'OrdinariaPilotoResultadoRehabilitadoQA.json').read_text())['Cases']
assert len(cases)==132 and len({c['CaseHash'] for c in cases})==132
assert {(c['CaseHash'],c['Archive']) for c in cases}=={(c['CaseHash'],c['Archive']) for c in expected}
counts={}
for c in cases:counts[c['State']]=counts.get(c['State'],0)+1
out={'Status':'OK','Date':'2026-11-23','Company':13,'Type':10,'Period':5696,'Stage':'ENLACE_SUELDO_CAPTURADO_V1','CasesCount':132,'Counts':counts,'Checks':sum(len(d['Checks']) for d in results),'Cases':cases,'HelpersCompiledInProbeOnly':True,'NoPayrollWrites':True,'NoConfigurationWrites':True,'NoCurrentProviderReadsDuringEvaluation':True,'StageLinkEvaluated':True,'AllPayrollConceptsCovered':False,'HistoricalParityCertified':False,'OperationalPayrollReady':False,'RequirementComplete':False}
(req/'OrdinariaPilotoEnlaceEtapaResumenQA.json').write_text(json.dumps(out,indent=2)+'\n')
print(json.dumps({k:v for k,v in out.items() if k!='Cases'}),flush=True)
