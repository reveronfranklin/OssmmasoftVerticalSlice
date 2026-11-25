#!/usr/bin/env python3
"""R58 approved read-only sources and catalogs. Never execute a preview or a program."""
import hashlib
import json
from pathlib import Path
import time
from diagnostico import EVIDENCE, request


def read(name, operation, body):
    result = request((name, operation, body))
    if not result['isValid']:
        raise RuntimeError(str(result))
    document = json.loads((EVIDENCE / (name + '.json')).read_text())
    time.sleep(2.2)
    return document['data']['resultado']


def main():
    folder = EVIDENCE / 'Fuentes'
    folder.mkdir(exist_ok=True)
    manifests = []
    names = ['SP_CNT_AUT_BUILD_COMP', 'SP_CNT_AUT_BUILD_OP', 'SP_CNT_AUT_BUILD_CH_RET',
             'SP_CNT_AUT_PREV', 'SP_CNT_AUT_CONF', 'SP_CNT_RPT_MAY_ANA', 'SP_CNT_CAT_DESC_GET',
             'CNT_PKG_CONTABILIDAD']
    for name in names:
        kind = 'PACKAGE BODY' if name == 'CNT_PKG_CONTABILIDAD' else 'PROCEDURE'
        lines = []
        start = 1
        for page in range(30):
            data = read('Fuente_' + name + '_' + str(page + 1), 'fuente',
                        {'conexion': 'CNT', 'nombre': name, 'tipo': kind, 'desdeLinea': start, 'cantidadLineas': 500})
            segment = data['lineas']
            if segment['truncado']:
                raise RuntimeError(name + ': texto truncado')
            rows = segment['filas']
            if rows and int(rows[0]['LINE']) != start:
                raise RuntimeError(name + ': fuente no contigua')
            lines.extend(rows)
            if not segment['hasMore']:
                break
            if not rows:
                raise RuntimeError(name + ': paginacion sin avance')
            start = int(rows[-1]['LINE']) + 1
        else:
            raise RuntimeError(name + ': limite de paginas')
        text = ''.join(r['TEXT'] or '' for r in lines)
        (folder / (name + '.txt')).write_text(text)
        receipt = {'Name': name, 'Type': kind, 'Lines': len(lines), 'Sha256': hashlib.sha256(text.encode()).hexdigest(), 'Complete': True}
        manifests.append(receipt)
        print(json.dumps(receipt), flush=True)
    (folder / 'Manifest.json').write_text(json.dumps(manifests, indent=2) + '\n')
    tables = {
        'CatalogosAutorizadosQA': {'objeto': 'CNT_DESCRIPTIVAS', 'columnas': ['DESCRIPCION_ID', 'TITULO_ID', 'CODIGO', 'DESCRIPCION', 'CODIGO_EMPRESA'],
            'filtros': [{'columna': 'TITULO_ID', 'operador': 'in', 'valor': [1, 5]}], 'orden': [{'columna': 'DESCRIPCION_ID', 'direccion': 'asc'}]},
        'MayoresQA': {'objeto': 'CNT_MAYORES', 'columnas': ['CODIGO_MAYOR', 'NUMERO_MAYOR', 'DENOMINACION', 'COLUMNA_BALANCE', 'CODIGO_EMPRESA'],
            'orden': [{'columna': 'CODIGO_MAYOR', 'direccion': 'asc'}]},
        'PeriodosQA': {'objeto': 'CNT_PERIODOS', 'columnas': ['CODIGO_PERIODO', 'NOMBRE_PERIODO', 'FECHA_DESDE', 'FECHA_HASTA', 'FECHA_CIERRE', 'FECHA_PRECIERRE', 'CODIGO_EMPRESA'],
            'filtros': [{'columna': 'FECHA_DESDE', 'operador': 'gte', 'valor': '2022-01-01'}], 'orden': [{'columna': 'CODIGO_PERIODO', 'direccion': 'asc'}]},
        'ComprobantesMuestraQA': {'objeto': 'CNT_COMPROBANTES', 'columnas': ['CODIGO_COMPROBANTE', 'CODIGO_PERIODO', 'TIPO_COMPROBANTE_ID', 'ORIGEN_ID', 'NUMERO_COMPROBANTE', 'FECHA_COMPROBANTE', 'ESTADO_CMP', 'AJUSTE_MANUAL', 'VERSION_CMP', 'CODIGO_EMPRESA'],
            'filtros': [{'columna': 'NUMERO_COMPROBANTE', 'operador': 'in', 'valor': ['22-07-02-001', '22-07-02-002', '22-07-02-003']}],
            'orden': [{'columna': 'CODIGO_COMPROBANTE', 'direccion': 'asc'}]}}
    for name, body in tables.items():
        data = read(name, 'consultarDatos', {'conexion': 'CNT', 'pageSize': 500, **body})
        print(json.dumps({'Dataset': name, 'Rows': len(data['datos']['filas']), 'HasMore': data['datos']['hasMore'], 'Truncated': data['datos']['truncado']}), flush=True)


if __name__ == '__main__':
    main()
