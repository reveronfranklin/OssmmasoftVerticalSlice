namespace OssmmasoftVerticalSlice.Features.FacturacionElectronica;

// T6.3 y T6.4 - validador de la guia de despacho, contra el Art. 10 de la
// Providencia SNAT/2024/000102.
//
// Es la misma obligacion del Art. 29.4 que sostiene a los otros dos validadores:
// validar la estructura de los documentos es deber de la imprenta digital.
//
// POR QUE NO REUSA FacturaValidador.ValidarContenido. Porque no queda nada que
// reusar. El Art. 10.2 remite a los numerales 2, 3, 4, 5, 6 y 14 del Art. 7, y
// los seis son datos que pone el SISTEMA: numeracion, datos del emisor, numero de
// control, rango asignado, fecha de emision y datos de la imprenta. Ninguno viene
// del request. Todo lo que el emisor aporta a una guia lo piden el 10.3, el 10.4
// y el 10.5, que son numerales propios del Art. 10.
//
// Lo que si se reusa es la forma del resultado -FacturaFalta separa el numeral
// del texto, FacturaValidacion lleva el articulo-, igual que en el comprobante de
// retencion.
//
// LO QUE ESTE VALIDADOR RECHAZA Y LOS OTROS NO: montos. Un precio, una alicuota o
// un ajuste en una guia no es un dato de mas, es un dato que la norma no admite
// que el documento diga (D-40). Se rechaza en vez de ponerlo en cero en silencio,
// porque poner cero escondería que quien lo emitio creia estar facturando.
public static class GuiaValidador
{
    public static FacturaValidacion Validar(GuiaEmitirCommand comando)
    {
        var faltantes = new List<FacturaFalta>();

        // Encabezado del Art. 10 - estos documentos amparan "unicamente" el
        // traslado de bienes muebles que NO representen ventas. El motivo es con
        // lo que el emisor sostiene que su traslado cae dentro de esa condicion
        // (D-43). El numeral va vacio a proposito: no lo pide un numeral, lo pide
        // el encabezado, y FacturaFalta omite el prefijo cuando esta vacio.
        if (string.IsNullOrWhiteSpace(comando.MotivoTraslado))
        {
            faltantes.Add(new FacturaFalta(string.Empty,
                "falta el motivo del traslado. El encabezado del artículo solo admite este documento para "
                + "amparar traslados que no representen ventas, y sin motivo no hay con qué sostenerlo."));
        }

        // 10.5 - nombre y RIF del receptor. SIN la alternativa de cedula o
        // pasaporte que el 7.7 admite para el adquiriente: el 10.5 no la tiene.
        if (string.IsNullOrWhiteSpace(comando.ReceptorNombre))
        {
            faltantes.Add(new FacturaFalta("10.5", "falta el nombre o razón social del receptor."));
        }

        if (string.IsNullOrWhiteSpace(comando.ReceptorRif))
        {
            faltantes.Add(new FacturaFalta("10.5",
                "falta el RIF del receptor. El numeral no admite cédula ni pasaporte en su lugar, "
                + "a diferencia del 7.7 para el adquiriente de una factura."));
        }

        // 10.4 - la descripcion de los bienes. Un documento sin renglones no
        // describe ningun traslado.
        if (comando.Renglones is null || comando.Renglones.Count == 0)
        {
            faltantes.Add(new FacturaFalta("10.4",
                "la guía no describe ningún bien: no hay nada que amparar."));

            return new FacturaValidacion(false, faltantes, "10");
        }

        for (int i = 0; i < comando.Renglones.Count; i++)
        {
            var renglon = comando.Renglones[i];
            int numero = i + 1;

            if (string.IsNullOrWhiteSpace(renglon.Descripcion))
            {
                faltantes.Add(new FacturaFalta("10.4", $"el renglón {numero} no describe el bien que se traslada."));
            }

            // La medida, estructurada y comprobable (D-42). El numeral dice
            // "capacidad, peso o volumen": basta una de las tres, pero tiene que
            // ser una de las tres.
            string medida = (renglon.MedidaTipo ?? string.Empty).Trim().ToLowerInvariant();

            if (medida.Length == 0)
            {
                faltantes.Add(new FacturaFalta("10.4",
                    $"al renglón {numero} le falta la medida: el numeral exige señalar capacidad, peso o volumen."));
            }
            else if (!GuiaDb.MedidasValidas.Contains(medida))
            {
                faltantes.Add(new FacturaFalta("10.4",
                    $"el renglón {numero} declara la medida «{renglon.MedidaTipo}», y el numeral solo nombra "
                    + "capacidad, peso o volumen."));
            }
            else
            {
                if (renglon.MedidaValor <= 0)
                {
                    faltantes.Add(new FacturaFalta("10.4",
                        $"el renglón {numero} señala {medida} sin un valor mayor que cero."));
                }

                if (string.IsNullOrWhiteSpace(renglon.MedidaUnidad))
                {
                    faltantes.Add(new FacturaFalta("10.4",
                        $"el renglón {numero} señala {medida} sin decir en qué unidad."));
                }
            }

            if (renglon.Cantidad <= 0)
            {
                faltantes.Add(new FacturaFalta("10.4",
                    $"el renglón {numero} no dice cuántos bienes se trasladan."));
            }
        }

        return new FacturaValidacion(faltantes.Count == 0, faltantes, "10");
    }
}
