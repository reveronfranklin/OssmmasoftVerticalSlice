namespace OssmmasoftVerticalSlice.Features.FacturacionElectronica;

// T6B.5 - validador del comprobante de retencion, contra el Art. 11 de la
// Providencia SNAT/2024/000102.
//
// Es la misma obligacion del Art. 29.4 que sostiene al validador de la factura:
// validar la estructura de los documentos es deber de la imprenta digital.
//
// POR QUE NO REUSA FacturaNumerales. El mapa de D-35 traduce un mismo concepto
// entre los Arts. 7, 13 y 15, que piden LO MISMO con otra numeracion. El Art. 11
// no es una tercera forma de pedir lo mismo: pide otras cosas -agente de
// retencion, proveedor, periodo de imposicion, documentos retenidos- que no
// tienen equivalente en ninguno de los otros tres. Forzarlo al mapa habria
// agregado once conceptos que solo un articulo usa.
//
// Lo que SI se reusa es la forma del resultado: FacturaFalta separa el numeral
// del texto, y FacturaValidacion lleva el articulo. Por eso el mensaje sale
// diciendo "Articulo 11" sin que haya que tocar nada.
//
// QUE NO VALIDA. El numeral 11.9 -datos de la imprenta digital- no bloquea la
// emision, igual que el 7.14 en la factura: la providencia no existe hasta que
// el SENIAT autorice. Marca el comprobante como de prueba, y el CHECK de la
// tabla impide que uno DEFINITIVO exista sin esos datos.
public static class RetencionValidador
{
    public static FacturaValidacion Validar(RetencionEmitirCommand comando)
    {
        var faltantes = new List<FacturaFalta>();

        // 11.7 - periodo de imposicion. Va primero porque de el sale la
        // numeracion del 11.1: sin periodo valido no hay documento posible.
        string periodo = (comando.Periodo ?? string.Empty).Trim();

        if (periodo.Length != 6 || !periodo.All(char.IsDigit))
        {
            faltantes.Add(new FacturaFalta("11.7",
                "el período de imposición debe venir en formato AAAAMM, por ejemplo 202609."));
        }
        else
        {
            int mes = int.Parse(periodo[4..]);

            if (mes < 1 || mes > 12)
            {
                faltantes.Add(new FacturaFalta("11.7", $"el período indica el mes {mes}, que no existe."));
            }
        }

        // 11.2 - datos del agente de retencion. El RIF no se pide en el request:
        // sale del emisor, que ES el agente. Lo que se valida es que el emisor
        // venga identificado.
        if (comando.EmisorId <= 0)
        {
            faltantes.Add(new FacturaFalta("11.2",
                "falta el agente de retención: el comprobante se emite desde un emisor registrado."));
        }

        // 11.4 - datos del proveedor. El numeral pide nombre, RIF y CORREO, y el
        // correo se nombra expreso, asi que no es opcional.
        if (string.IsNullOrWhiteSpace(comando.ProveedorRif))
        {
            faltantes.Add(new FacturaFalta("11.4", "falta el RIF del proveedor."));
        }

        if (string.IsNullOrWhiteSpace(comando.ProveedorRazonSocial))
        {
            faltantes.Add(new FacturaFalta("11.4", "falta el nombre o razón social del proveedor."));
        }

        if (string.IsNullOrWhiteSpace(comando.ProveedorCorreo))
        {
            faltantes.Add(new FacturaFalta("11.4",
                "falta el correo electrónico del proveedor, que el numeral nombra expresamente."));
        }

        // 11.5, 11.6 y 11.8 - los documentos retenidos. Un comprobante sin
        // documentos no retiene nada.
        if (comando.Documentos is null || comando.Documentos.Count == 0)
        {
            faltantes.Add(new FacturaFalta("11.6",
                "el comprobante no indica ninguna factura o nota de débito retenida."));

            return new FacturaValidacion(false, faltantes, "11");
        }

        for (int i = 0; i < comando.Documentos.Count; i++)
        {
            var doc = comando.Documentos[i];
            int numero = i + 1;

            if (string.IsNullOrWhiteSpace(doc.DocumentoNumero))
            {
                faltantes.Add(new FacturaFalta("11.6", $"al documento {numero} le falta su número."));
            }

            if (string.IsNullOrWhiteSpace(doc.DocumentoControl))
            {
                faltantes.Add(new FacturaFalta("11.5",
                    $"al documento {numero} le falta el número de control de la factura retenida."));
            }

            if (doc.DocumentoFecha == default)
            {
                faltantes.Add(new FacturaFalta("11.7",
                    $"al documento {numero} le falta la fecha, sin la cual el período de imposición no se sostiene."));
            }

            if (doc.MontoTotal < 0 || doc.BaseImponible < 0 || doc.ImpuestoCausado < 0 || doc.MontoRetenido < 0)
            {
                faltantes.Add(new FacturaFalta("11.8", $"el documento {numero} tiene montos negativos."));
            }

            // No se puede retener mas impuesto del que la operacion causo. Es la
            // misma regla que sostiene el CHECK de la tabla; se valida antes para
            // devolver el numeral en vez de un error del motor.
            if (doc.MontoRetenido > doc.ImpuestoCausado)
            {
                faltantes.Add(new FacturaFalta("11.8",
                    $"el documento {numero} retiene {doc.MontoRetenido:N2} sobre un impuesto causado de "
                    + $"{doc.ImpuestoCausado:N2}: no se puede retener más de lo causado."));
            }

            // Coherencia interna del documento retenido. Sin ella, el comprobante
            // afirmaria que una factura tiene un impuesto que su propia base no
            // produce.
            if (doc.BaseImponible > doc.MontoTotal)
            {
                faltantes.Add(new FacturaFalta("11.8",
                    $"el documento {numero} tiene una base imponible mayor que su monto total."));
            }
        }

        return new FacturaValidacion(faltantes.Count == 0, faltantes, "11");
    }
}
