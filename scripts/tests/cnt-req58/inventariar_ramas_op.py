#!/usr/bin/env python3
"""Offline source inventory. Branch counts are not accounting acceptance."""
from pathlib import Path
import hashlib,json,re
ROOT=Path(__file__).resolve().parents[4]
E=ROOT/'Requerimientos/58 - Revision Compronbantes Automaticos/Evidencias'
legacy=Path('/tmp/req58/legacycnt.txt').read_text()
installed=(E/'Fuentes/SP_CNT_AUT_BUILD_OP.txt').read_text()

def without_comments(sql):
 out=[];i=0;quoted=False
 while i<len(sql):
  if sql[i]=="'":
   out.append(sql[i]);i+=1
   if quoted and i<len(sql) and sql[i]=="'":out.append(sql[i]);i+=1;continue
   quoted=not quoted;continue
  if not quoted and sql[i:i+2]=='--':
   j=sql.find('\n',i);j=len(sql) if j<0 else j;out.append(' '*(j-i));i=j;continue
  if not quoted and sql[i:i+2]=='/*':
   j=sql.find('*/',i+2);assert j>=0;j+=2;out.append(''.join('\n' if c=='\n' else ' ' for c in sql[i:j]));i=j;continue
  out.append(sql[i]);i+=1
 return ''.join(out)

def describe(block):
 clean=without_comments(block);matches=list(re.finditer(r"SELECT\s*'([DH])'\s+COLUMNA_BALANCE",clean,re.I));rows=[]
 for i,m in enumerate(matches):
  part=clean[m.start():matches[i+1].start() if i+1<len(matches) else len(clean)]
  major=re.search(r"NVL\(CM.NUMERO_MAYOR,\s*'([^']+)'\)",part,re.I)
  rows.append({'Index':i+1,'Column':m.group(1),'MajorLabel':major.group(1) if major else None,
               'Tables':list(dict.fromkeys(re.findall(r'\b(?:ADM|PRE)_[A-Z_]+\b',part)))})
 return rows
normal_start=legacy.index("ELSIF CNT_DESCRIPTIVA_ID ('ODPAUT'")
an_start=legacy.index("ELSIF CNT_DESCRIPTIVA_ID ('ANODPAUT'")
end=legacy.index('-- CHEQUES (AUTOM',an_start)
report={'Date':'2026-11-24','FormsTextSha256':hashlib.sha256(legacy.encode()).hexdigest(),
 'InstalledBuilderSha256':hashlib.sha256(installed.encode()).hexdigest(),
 'FormsNormal':describe(legacy[normal_start:an_start]),'FormsAnnulment':describe(legacy[an_start:end]),
 'InstalledSharedBuilder':describe(installed),'InstalledAnnulmentNegatesAfterInsert':bool(re.search(r'SET\s+MONTO\s*=\s*-MONTO',without_comments(installed),re.I)),
 'MonetaryAcceptance':False}
(E/'InventarioSubramasOpQA.json').write_text(json.dumps(report,indent=2)+'\n')
print(json.dumps({'FormsNormal':len(report['FormsNormal']),'FormsAnnulment':len(report['FormsAnnulment']),'InstalledShared':len(report['InstalledSharedBuilder']),'AnnulmentNegates':report['InstalledAnnulmentNegatesAfterInsert']}))
