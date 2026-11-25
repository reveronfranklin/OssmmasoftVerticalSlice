"""Run a bounded QA read-only probe. Each caller enforces scope and transaction mode."""
import json
from pathlib import Path
import shlex
import subprocess

REMOTE = 'import tempfile,pathlib,subprocess,sys,json,shutil,os\np=pathlib.Path(tempfile.mkdtemp(prefix=\'r58-budget-fixtures-\'))\ntry:\n data=json.load(sys.stdin);runtime=pathlib.Path(\'/srv/ossmmasoft-pruebas/current/vertical\').resolve();assert runtime.name==\'r57-prenomina-txn-20261124T091632Z\'\n refs=\'\'.join(\'<Reference Include="\'+name+\'"><HintPath>\'+str(runtime/(name+\'.dll\'))+\'</HintPath></Reference>\' for name in [\'OssmmasoftVerticalSlice\',\'Oracle.ManagedDataAccess\',\'Microsoft.Data.SqlClient\'])\n (p/\'Probe.csproj\').write_text(\'<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable></PropertyGroup><ItemGroup><FrameworkReference Include="Microsoft.AspNetCore.App"/>\'+refs+\'</ItemGroup></Project>\')\n (p/\'Program.cs\').write_text(data[\'program\']);(p/\'fixture.json\').write_text(json.dumps(data[\'payload\']))\n r=subprocess.run([\'dotnet\',\'run\',\'--project\',str(p/\'Probe.csproj\'),\'--verbosity\',\'quiet\',\'--\',str(p/\'fixture.json\')],capture_output=True,text=True,timeout=100,env={**os.environ,\'TZ\':\'America/Caracas\'})\n print(\'\\n\'.join(x for x in r.stdout.splitlines() if x.startswith(\'R58_\') or \': error CS\' in x));sys.exit(r.returncode)\nfinally:shutil.rmtree(p)\n'

def run_probe(program, payload):
    result = subprocess.run(['ssh', '-i', str(Path.home()/'.ssh/ubuntu_pruebas'),
        '-o', 'BatchMode=yes', '-o', 'ConnectTimeout=10', 'ossmma@74.121.190.6',
        'python3 -c '+shlex.quote(REMOTE)], input=json.dumps({'program':program,'payload':payload}),
        capture_output=True,text=True,timeout=120)
    if 'R58_RESULT=' not in result.stdout:
        raise RuntimeError(result.stdout[:1200] or result.stderr[:1000])
    return json.loads(result.stdout.split('R58_RESULT=')[-1]), result.returncode
