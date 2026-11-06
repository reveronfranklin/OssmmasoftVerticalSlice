#!/usr/bin/env python3
"""Refresh exact menu routes, including literal getStaticPaths parameters."""
import json
import re
from pathlib import Path


def page_routes(page: Path, frontend: Path) -> set[str]:
    route = '/apps/' + page.relative_to(frontend).as_posix()[:-4]
    if route.endswith('/index'):
        route = route[:-6]
    if '[' not in route:
        return {route}
    # Only accept paths explicitly enumerated by the page; never turn a dynamic
    # segment into a wildcard granting arbitrary aliases or resource identifiers.
    source = page.read_text()
    if 'getStaticPaths' not in source:
        return set()
    paths = re.search(r'\bpaths\s*:\s*\[(.*?)\]\s*,', source, re.S)
    if not paths:
        return set()
    result = set()
    for parameters in re.findall(r'\bparams\s*:\s*\{([^{}]*)\}', paths.group(1)):
        values = dict(re.findall(r'''\b(\w+)\s*:\s*['"]([^'"\r\n]+)['"]''', parameters))
        candidate = route
        for key, value in values.items():
            if any(char in value for char in '/\\?#') or value in {'.', '..'}:
                continue
            candidate = candidate.replace(f'[{key}]', value)
        if '[' not in candidate:
            result.add(candidate)
    return result


def main():
    project = Path(__file__).resolve().parents[1]
    frontend = project.parent / 'NextOssmasoft' / 'src' / 'pages' / 'apps'
    if not frontend.is_dir():
        raise SystemExit(f'Frontend no encontrado: {frontend}')
    routes = set()
    for page in frontend.rglob('*.tsx'):
        routes.update(page_routes(page, frontend))
    output = project / 'Assets' / 'MenuRoutes.json'
    output.write_text(json.dumps(sorted(routes), indent=2) + '\n')
    print(f'{len(routes)} rutas exactas en {output}')


if __name__ == '__main__':
    main()
