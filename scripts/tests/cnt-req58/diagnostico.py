#!/usr/bin/env python3
"""R58: bounded, read-only OracleAnalisis inventory; credentials stay in local settings."""
import concurrent.futures
import json
from pathlib import Path
import subprocess

ROOT = Path(__file__).resolve().parents[4]
EVIDENCE = ROOT / 'Requerimientos/58 - Revision Compronbantes Automaticos/Evidencias'
HELPER = ROOT / 'OssmmasoftVerticalSlice/scripts/oracle-analysis-request.py'
SETTINGS = ROOT / 'OssmmasoftVerticalSlice/appsettings.Development.json'
BASE = 'http://74.121.190.6/vertical/api'


def request(item):
    name, operation, body = item
    command = ['python3', str(HELPER), '--url', BASE, '--settings', str(SETTINGS),
               '--allow-http', '--operation', operation, '--body', json.dumps(body)]
    result = subprocess.run(command, capture_output=True, text=True, timeout=75)
    try:
        document = json.loads(result.stdout)
    except ValueError:
        raise RuntimeError(f'{name}: respuesta no JSON; codigo {result.returncode}') from None
    (EVIDENCE / (name + '.json')).write_text(json.dumps(document, indent=2) + '\n')
    data = document.get('data') or {}
    summary = {'name': name, 'isValid': document.get('isValid'),
               'message': document.get('message'), 'code': data.get('code')}
    return summary


def main():
    EVIDENCE.mkdir(parents=True, exist_ok=True)
    identity = request(('ConexionCntQA', 'verificarConexion', {'conexion': 'CNT'}))
    doc = json.loads((EVIDENCE / 'ConexionCntQA.json').read_text())
    actual = (doc.get('data') or {}).get('resultado', {})
    if not identity['isValid'] or any(actual.get(k) != v for k, v in
            {'database': 'XE', 'serverHost': '6d88ba053190', 'sessionUser': 'CNT', 'schema': 'CNT'}.items()):
        raise SystemExit('Identidad CNT distinta al destino QA esperado; inventario detenido.')
    tasks = [(name, 'describirObjeto', {'conexion': 'CNT', 'nombre': name}) for name in
             ['CNT_DESCRIPTIVAS', 'CNT_COMPROBANTES', 'CNT_DETALLE_COMPROBANTE',
              'CNT_MAYORES', 'CNT_AUXILIARES', 'CNT_PERIODOS']]
    tasks += [('ObjetosAutomaticosQA', 'objetos', {'conexion': 'CNT', 'tipo': 'PROCEDURE', 'buscar': 'SP_CNT_AUT', 'pageSize': 100}),
              ('ArgumentosBuildCompQA', 'argumentos', {'conexion': 'CNT', 'nombre': 'SP_CNT_AUT_BUILD_COMP'}),
              ('FuenteBuildCompQA', 'fuente', {'conexion': 'CNT', 'nombre': 'SP_CNT_AUT_BUILD_COMP', 'tipo': 'PROCEDURE', 'desdeLinea': 1, 'cantidadLineas': 200}),
              ('CatalogosCntQA', 'consultarDatos', {'conexion': 'CNT', 'objeto': 'CNT_DESCRIPTIVAS',
                  'columnas': ['DESCRIPCION_ID', 'TITULO_ID', 'CODIGO', 'DESCRIPCION', 'CODIGO_EMPRESA'],
                  'filtros': [{'columna': 'TITULO_ID', 'operador': 'in', 'valor': [1, 5]}],
                  'orden': [{'columna': 'DESCRIPCION_ID', 'direccion': 'asc'}], 'pageSize': 500})]
    with concurrent.futures.ThreadPoolExecutor(max_workers=2) as pool:
        summaries = list(pool.map(request, tasks))
    (EVIDENCE / 'InventarioInicialQA.json').write_text(json.dumps({'IdentityVerified': True,
        'WritesRequested': False, 'SqlExecutedByThisClient': False, 'Requests': summaries}, indent=2) + '\n')
    print(json.dumps(summaries, ensure_ascii=False))


if __name__ == '__main__':
    main()
