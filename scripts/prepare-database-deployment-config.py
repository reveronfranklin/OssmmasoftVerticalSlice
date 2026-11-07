#!/usr/bin/env python3
"""Prepare R54 private configuration locally; no database or network access."""
import argparse
import json
import os
from pathlib import Path, PureWindowsPath

parser=argparse.ArgumentParser(description=__doc__)
parser.add_argument('--output',required=True,help='Private file OUTSIDE the workspace; existing files are never overwritten.')
parser.add_argument('--storage-path',required=True,help='Absolute private directory on the production server.')
parser.add_argument('--production',default=str(Path(__file__).resolve().parents[1]/'appsettings.json'))
parser.add_argument('--test',default=str(Path(__file__).resolve().parents[1]/'appsettings.Development.json'))
args=parser.parse_args()
workspace=Path(__file__).resolve().parents[2]
output=Path(args.output).resolve()
if output==workspace or workspace in output.parents:
    parser.error('The private file must be outside the workspace and version control.')
if not Path(args.storage_path).is_absolute() and not PureWindowsPath(args.storage_path).is_absolute():
    parser.error('--storage-path must be absolute on the production server.')
profiles=[]
for filename in [args.test,args.production]:
    data=json.loads(Path(filename).read_text(encoding='utf-8-sig'))
    profiles.append({'ExpectedDatabaseId':'','ConnectionStrings':{
        schema:data['ConnectionStrings']['DefaultConnection'+schema] for schema in ['RH', 'SIS', 'CNT', 'ADM', 'PRE', 'RM', 'CAT', 'BM', 'MFO']}})
value={'DatabaseDeployment':{'Enabled':True,'ExecutionEnabled':False,
       'StoragePath':args.storage_path,'AllowedSchemas':['RH', 'SIS', 'CNT', 'ADM', 'PRE', 'RM', 'CAT', 'BM', 'MFO'],
       'ExcludedSchemas':['BMC','RHC'],'Pruebas':profiles[0],'ProduccionLectura':profiles[1]}}
output.parent.mkdir(parents=True,exist_ok=True,mode=0o700)
fd=os.open(output,os.O_CREAT|os.O_EXCL|os.O_WRONLY,0o600)
with os.fdopen(fd,'w') as stream:json.dump(value,stream,indent=2)
print('Private configuration created. Contents and credentials were not printed.')
print('Deploy it privately to production and set DatabaseDeployment:PrivateConfigPath there.')
print('These are the supplied accounts; ask the DBA to restrict metadata accounts before use.')
print('Use Check connections to discover DBIDs, then configure ExpectedDatabaseId for both environments.')
