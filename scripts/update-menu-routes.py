#!/usr/bin/env python3
"""Refresh R50's checked-in route inventory from the sibling Next frontend."""
import json
from pathlib import Path

project = Path(__file__).resolve().parents[1]
frontend = project.parent / 'NextOssmasoft' / 'src' / 'pages' / 'apps'
if not frontend.is_dir():
    raise SystemExit(f'Frontend no encontrado: {frontend}')
routes = set()
for page in frontend.rglob('*.tsx'):
    route = '/apps/' + page.relative_to(frontend).as_posix()[:-4]
    routes.add(route[:-6] if route.endswith('/index') else route)
output = project / 'Assets' / 'MenuRoutes.json'
output.write_text(json.dumps(sorted(routes), indent=2) + '\n')
print(f'{len(routes)} rutas en {output}')
