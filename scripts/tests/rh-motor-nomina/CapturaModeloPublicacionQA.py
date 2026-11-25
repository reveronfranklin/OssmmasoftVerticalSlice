import os
from pathlib import Path
os.chdir(Path(__file__).resolve().parents[4])
from pathlib import Path
import re,json,subprocess,shlex
v=Path('output/r57-cap-model-front-qa/version.txt').read_text().strip();expected=json.loads(Path('output/r57-corrida-captura-qa/api.expected.json').read_text())
routes=[]
for p in Path('OssmmasoftVerticalSlice/Features/RhMotorNomina').glob('*.cs'):routes+=re.findall(r'HttpPost\("([^\"]+)"',p.read_text())
script='''import pathlib,urllib.request,urllib.error,hashlib,socket,subprocess,json
v=VERSION;routes=ROUTES;expected=EXPECTED
b=pathlib.Path('/srv/ossmmasoft-pruebas')
assert (b/'current/vertical').resolve().name=='r57-cap-model-20261122T235432Z'
assert (b/'current/frontend').resolve().name==v
p=b/'current/vertical';assert all((p/n).is_file() and hashlib.sha256((p/n).read_bytes()).hexdigest()==h for n,h in expected.items())
cfg=json.loads((b/'shared/config/vertical.json').read_text());assert not cfg.get('RhMotorNomina',{}).get('AplicacionEnabled',False)
assert urllib.request.urlopen('http://127.0.0.1:5101/health').status==200
checks=0
for port in [5101,80]:
 for route in routes:
  for auth in [None,'Bearer invalid']:
   h={'Content-Type':'application/json','Host':'74.121.190.6'}
   if auth:h['Authorization']=auth
   req=urllib.request.Request('http://127.0.0.1:'+str(port)+('/vertical' if port==80 else '')+'/api/RhMotorNomina/'+route,data=b'{}',headers=h)
   try:status=urllib.request.urlopen(req).status
   except urllib.error.HTTPError as e:status=e.code
   assert status==401,(route,status)
   checks+=1
for path in ['/login/','/apps/rh/motor-nomina/','/apps/sis/usuarios/']:
 assert urllib.request.urlopen(urllib.request.Request('http://127.0.0.1'+path,headers={'Host':'74.121.190.6'})).status==200
for unit in ['ossmmasoft-vertical.service','ossmmasoft-frontend.service','ossmmasoft-legacy.service']:
 assert subprocess.run(['systemctl','--user','is-active',unit],capture_output=True,text=True).stdout.strip()=='active'
s=socket.socket();assert s.connect_ex(('127.0.0.1',3108))!=0;s.close()
preview=json.loads((b/'staging'/v/'FrontendPreview.json').read_text());assert preview['PreviewStopped']
for asset,h in preview['ProfileAssets'].items():
 data=urllib.request.urlopen(urllib.request.Request('http://127.0.0.1/_next/'+asset[len('.next/'):],headers={'Host':'74.121.190.6'})).read();assert hashlib.sha256(data).hexdigest()==h
print(json.dumps({'BuildId':preview['BuildId'],'NewUiAssets':len(preview['ProfileAssets']),'ApiVersion':'r57-cap-model-20261122T235432Z','FrontendVersion':v,'RuntimeHashes':len(expected),'Routes':len(routes),'UnauthorizedChecks':checks,'Pages':3,'ServicesActive':3,'PreviewClosed':True,'PayrollWrites':False,'ApplicationEnabled':False}))
'''.replace('VERSION',repr(v)).replace('ROUTES',repr(routes)).replace('EXPECTED',repr(expected))
r=subprocess.run(['ssh','-i',str(Path.home()/'.ssh/ubuntu_pruebas'),'-o','BatchMode=yes','-o','ConnectTimeout=10','ossmma@74.121.190.6','python3 -c '+shlex.quote(script)],capture_output=True,text=True)
if r.returncode:raise SystemExit(r.stderr[:1500])
d=json.loads(r.stdout);Path('Requerimientos/57 - Motor Parametrizable De Calculo De Nomina/CapturaModeloPublicacionQA.json').write_text(json.dumps(d,indent=2)+'\n');print(d)
