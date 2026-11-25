"""Complete bounded worker batches before reading large result documents."""
import json
from pathlib import Path
import subprocess
import sys
root=Path(__file__).resolve().parents[3]
req=root.parent/'Requerimientos/57 - Motor Parametrizable De Calculo De Nomina'
for start in range(0,132,4):
    audited=[req/f'OrdinariaPilotoCadenaLote{n:04d}QA.json' for n in range(start,min(start+4,132),2)]
    if all(p.exists() and json.loads(p.read_text())['Status']=='OK' for p in audited):continue
    checkpoint=req/f'OrdinariaPilotoCadenaEncolar{start:04d}QA.json'
    if checkpoint.exists():
        data=json.loads(checkpoint.read_text())
        expected=json.loads((req/'OrdinariaPilotoResultadoRehabilitadoQA.json').read_text())['Cases'][start:start+4]
        if data['Status']=='OK' and 'CandidateCases' in data['Details']:
            assert {(c['CaseHash'],c['Archive']) for c in data['Details']['CandidateCases']}=={(c['CaseHash'],c['Archive']) for c in expected}
            assert all(c['State']=='COMPLETADO' for c in data['Details']['Cases'])
            continue
    result=subprocess.run([sys.executable,str(Path(__file__).with_name('OrdinariaPilotoQA.py')),'CadenaEncolar',str(start)],cwd=root,capture_output=True,text=True,timeout=290)
    if result.returncode:raise RuntimeError(f'Batch {start}: {result.stdout[:1800]} {result.stderr[:1800]}')
    data=json.loads((req/f'OrdinariaPilotoCadenaEncolar{start:04d}QA.json').read_text())
    assert data['Status']=='OK' and all(c['State']=='COMPLETADO' for c in data['Details']['Cases'])
    print(json.dumps({'Batch':start,'Runs':[c['Run'] for c in data['Details']['Cases']],'WorkerSeconds':[c['WorkerSeconds'] for c in data['Details']['Cases']]}),flush=True)
print('WORKER_QUEUE_PHASE_COMPLETE_DOCUMENT_AUDIT_STILL_REQUIRED',flush=True)
