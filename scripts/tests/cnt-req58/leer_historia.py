#!/usr/bin/env python3
"""R58 read-only scoped historical headers plus details of the documented sample."""
import json
from decimal import Decimal
from leer_contexto import read
from diagnostico import EVIDENCE


def main():
    body = {'conexion': 'CNT', 'objeto': 'CNT_COMPROBANTES', 'pageSize': 500,
        'columnas': ['CODIGO_COMPROBANTE', 'CODIGO_PERIODO', 'TIPO_COMPROBANTE_ID', 'ORIGEN_ID', 'NUMERO_COMPROBANTE', 'FECHA_COMPROBANTE', 'ESTADO_CMP', 'AJUSTE_MANUAL', 'VERSION_CMP', 'CODIGO_EMPRESA'],
        'filtros': [{'columna': 'FECHA_COMPROBANTE', 'operador': 'gte', 'valor': '2022-07-01'},
                    {'columna': 'FECHA_COMPROBANTE', 'operador': 'lt', 'valor': '2026-11-25'}],
        'orden': [{'columna': 'CODIGO_COMPROBANTE', 'direccion': 'asc'}]}
    rows = []
    for page in range(100):
        result = read('HistoricoCabeceras_' + str(page + 1), 'consultarDatos', body)
        if result['datos']['truncado']:
            raise RuntimeError('Historico truncado')
        rows.extend(result['datos']['filas'])
        print(json.dumps({'Page': page + 1, 'Rows': len(rows), 'HasMore': result['datos']['hasMore']}), flush=True)
        if not result['datos']['hasMore']:
            break
        if not result['nextCursor']:
            raise RuntimeError('Cursor ausente')
        body['cursor'] = result['nextCursor']
    else:
        raise RuntimeError('Limite 50000; no se declara completo')
    ids = [r['CODIGO_COMPROBANTE'] for r in rows]
    if len(set(ids)) != len(ids):
        raise RuntimeError('Identidades repetidas durante paginacion')
    (EVIDENCE / 'CabecerasHistoricasQA.json').write_text(json.dumps({'Rows': rows, 'CompleteWithinRange': True,
        'DateFrom': '2022-07-01', 'DateToExclusive': '2026-11-25', 'Company': 13,
        'SnapshotBetweenPages': False}, indent=2) + '\n')
    sample = json.loads((EVIDENCE / 'ComprobantesMuestraQA.json').read_text())['data']['resultado']['datos']['filas']
    sample_ids = [r['CODIGO_COMPROBANTE'] for r in sample]
    result = read('DetallesMuestraQA', 'consultarDatos', {'conexion': 'CNT', 'objeto': 'CNT_DETALLE_COMPROBANTE', 'pageSize': 500,
        'columnas': ['CODIGO_DETALLE_COMPROBANTE', 'CODIGO_COMPROBANTE', 'CODIGO_MAYOR', 'CODIGO_AUXILIAR', 'MONTO', 'REFERENCIA1', 'REFERENCIA2', 'REFERENCIA3', 'DESCRIPCION', 'CODIGO_EMPRESA'],
        'filtros': [{'columna': 'CODIGO_COMPROBANTE', 'operador': 'in', 'valor': sample_ids}],
        'orden': [{'columna': 'CODIGO_DETALLE_COMPROBANTE', 'direccion': 'asc'}]})
    if result['datos']['hasMore'] or result['datos']['truncado']:
        raise RuntimeError('Detalle de muestra incompleto')
    details = result['datos']['filas']
    auxiliaries = sorted({r['CODIGO_AUXILIAR'] for r in details})
    if auxiliaries:
        read('AuxiliaresMuestraQA', 'consultarDatos', {'conexion': 'CNT', 'objeto': 'CNT_AUXILIARES', 'pageSize': 500,
            'columnas': ['CODIGO_AUXILIAR', 'CODIGO_MAYOR', 'SEGMENTO1', 'SEGMENTO2', 'DENOMINACION', 'CODIGO_PROVEEDOR', 'CODIGO_EMPRESA'],
            'filtros': [{'columna': 'CODIGO_AUXILIAR', 'operador': 'in', 'valor': auxiliaries}],
            'orden': [{'columna': 'CODIGO_AUXILIAR', 'direccion': 'asc'}]})
    for header in sample:
        lines = [r for r in details if r['CODIGO_COMPROBANTE'] == header['CODIGO_COMPROBANTE']]
        amounts = [Decimal(r['MONTO']) for r in lines]
        print(json.dumps({'Comprobante': header['NUMERO_COMPROBANTE'], 'Rows': len(lines),
            'Debe': str(-sum((v for v in amounts if v < 0), Decimal(0))),
            'Haber': str(sum((v for v in amounts if v > 0), Decimal(0)))}), flush=True)


if __name__ == '__main__':
    main()
