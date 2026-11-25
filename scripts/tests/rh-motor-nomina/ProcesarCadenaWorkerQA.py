"""QA worker audit, bounded concurrency and idempotent per immutable case."""
import concurrent.futures
import json
from pathlib import Path
import subprocess
import sys
root=Path(__file__).resolve().parents[3]
req=root.parent/'Requerimientos/57 - Motor Parametrizable De Calculo De Nomina'
def process(start):
    target=req/f'OrdinariaPilotoCadenaLote{start:04d}QA.json'
    if target.exists():
        data=json.loads(target.read_text())
        expected=json.loads((req/'OrdinariaPilotoResultadoRehabilitadoQA.json').read_text())['Cases'][start:start+2]
        assert data['Status']=='OK' and data['Details']['RealPublishedWorker'] is True
        assert {(c['CaseHash'],c['Archive']) for c in data['Details']['Cases']}=={(c['CaseHash'],c['Archive']) for c in expected}
        return start,data
    result=subprocess.run([sys.executable,str(Path(__file__).with_name('OrdinariaPilotoQA.py')),'CadenaLote',str(start)],cwd=root,capture_output=True,text=True,timeout=290)
    if result.returncode:raise RuntimeError(f'Batch {start}: {result.stdout[:1800]} {result.stderr[:1800]}')
    data=json.loads(target.read_text());assert data['Status']=='OK'
    return start,data
results=[]
# Two audit processes; compiled verifier is reused by source/runtime hash.
with concurrent.futures.ThreadPoolExecutor(max_workers=2) as pool:
    for start,data in pool.map(process,range(0,132,2)):
        results.append(data)
        print(json.dumps({'Batch':start,'Runs':[c['Run'] for c in data['Details']['Cases']],'Checks':len(data['Checks'])}),flush=True)
cases=[c for d in results for c in d['Details']['Cases']]
expected=json.loads((req/'OrdinariaPilotoResultadoRehabilitadoQA.json').read_text())['Cases']
assert len(cases)==132 and len({c['Run'] for c in cases})==len({c['CaseHash'] for c in cases})==132
assert {(c['CaseHash'],c['Archive']) for c in cases}=={(c['CaseHash'],c['Archive']) for c in expected}
out={'Status':'OK','Date':'2026-11-23','Company':13,'Type':10,'Period':5696,'CasesCount':132,'Checks':sum(len(d['Checks']) for d in results),'Cases':cases,'RealPublishedWorker':True,'ExactReplay':True,'NoPayrollWrites':True,'FinancialApplicationEnabled':False,'HistoricalParityCertified':False,'HttpSisAuthenticationTested':False,'OperationalPayrollReady':False,'RequirementComplete':False}
(req/'CadenaSueldoPoblacionWorkerQA.json').write_text(json.dumps(out,indent=2)+'\n')
print(json.dumps({k:v for k,v in out.items() if k!='Cases'}),flush=True)
