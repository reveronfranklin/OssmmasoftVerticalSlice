namespace OssmmasoftVerticalSlice.Features.FacturacionElectronica;

using Concepto = FacturaNumerales.Concepto;

// T4.4 y T5.9 - validador de estructura, previo a la asignacion del numero de
// control.
//
// No es una validacion de formulario: es la obligacion del Articulo 29.4, que
// manda a la imprenta digital "validar la estructura de los documentos
// transmitidos por el emisor". Nosotros somos la imprenta, asi que este paso es
// nuestro deber y no una cortesia con el cliente.
//
// DEVUELVE QUE NUMERAL FALTA, y devuelve TODOS los que faltan, no el primero.
// Quien esta corrigiendo un documento necesita ver la lista completa; descubrirla
// de a un error por intento convierte una correccion en diez viajes.
//
// Nunca lanza. Un documento incompleto es una falla esperada del negocio, no una
// excepcion, y el estandar del proyecto es explicito: eso viaja en IsValid.
//
// QUE NO VALIDA, y por que. Los numerales 4, 5 y 15 -numero de control, total de
// numeros asignados y fecha de asignacion- son responsabilidad del Rol A y se
// completan DESPUES de esta validacion: el Art. 29.4 pone la validacion antes de
// la asignacion, no despues. Exigirlos aca seria pedir que el documento traiga lo
// que todavia no se le asigno.
//
// UN SOLO CONJUNTO DE CHEQUEOS PARA TRES ARTICULOS (T5.9, D-35). El contenido
// que exige el Art. 7 de la 102 para la factura es el mismo que exigen los Arts.
// 13 y 15 de la 00071 para las notas, con otra numeracion. Por eso los chequeos
// se escriben una vez y el numeral lo resuelve FacturaNumerales segun el articulo
// aplicable. Antes el codigo del numeral estaba soldado dentro del texto del
// mensaje, y eso obligaba a duplicar cada condicion para poder citar otro
// articulo.
public static class FacturaValidador
{
    // Validacion de la emision directa: factura y nota de entrega, contra el
    // Art. 7 de la 102.
    public static FacturaValidacion Validar(FacturaEmitirCommand comando, FacturaImprentaDatos imprenta)
    {
        string tipo = (comando.TipoDocumento ?? string.Empty).Trim().ToLowerInvariant();

        // El 7.1 se valida distinto de los demas porque el tipo decide si el
        // resto tiene sentido: no vale la pena decirle a alguien que le falta el
        // adquiriente de un documento cuyo tipo no existe.
        if (!FacturacionElectronicaDb.TiposDocumento.Contains(tipo))
        {
            return Invalida(FacturaNumerales.Art7, new FacturaFalta(
                FacturaNumerales.Numeral(Concepto.TipoDocumento, FacturaNumerales.Art7),
                "el tipo de documento debe ser factura, débito, crédito o entrega."));
        }

        // El tipo es valido, pero no por esta via (T5.3). Una nota emitida aca
        // quedaria sin la referencia a la factura que soporto la operacion que
        // exige el Art. 23 de la 00071, y en este modulo eso no se puede corregir
        // despues: no hay UPDATE sobre FED_DOCUMENTO.
        //
        // El mensaje cita el Art. 23 y no el 7, que es el articulo que realmente
        // se estaria incumpliendo, y dice a donde ir: un rechazo que no indica la
        // alternativa obliga a quien integra a adivinarla.
        if (FacturacionElectronicaDb.TiposNota.Contains(tipo))
        {
            return Invalida("23", new FacturaFalta(string.Empty,
                "una nota de débito o de crédito se emite por notaCreate, que exige la referencia a la "
                + "fecha, número y monto de la factura que soportó la operación (Art. 23 de la Providencia "
                + "SNAT/2011/00071)."));
        }

        // Lo mismo para la guia de despacho (T6.3). El tipo es valido y esta via
        // no lo es: el Art. 10 le exige el motivo del traslado, el receptor con
        // RIF y la medida de los bienes, que este comando no sabe pedir, y le
        // prohibe el precio y el IVA, que si sabe mandar.
        //
        // El mensaje cita el Art. 10 y dice a donde ir, igual que el de la nota.
        if (FacturacionElectronicaDb.TiposGuia.Contains(tipo))
        {
            return Invalida("10", new FacturaFalta(string.Empty,
                "una guía de despacho se emite por guiaCreate, que exige el motivo del traslado, el RIF del "
                + "receptor (Art. 10.5) y la capacidad, peso o volumen de cada bien (Art. 10.4), y que no "
                + "admite precio ni IVA porque el Art. 10.2 no remite a los numerales 8, 11, 12 ni 13 del "
                + "Artículo 7."));
        }

        var faltantes = ValidarContenido(comando, tipo, FacturaNumerales.Art7);

        return new FacturaValidacion(faltantes.Count == 0, faltantes, FacturaNumerales.Art7);
    }

    // Validacion de una nota, contra el Art. 13 o el 15 de la 00071 segun el tipo
    // de contribuyente del emisor (Art. 23, "segun sea el caso", D-31).
    //
    // Los chequeos de contenido son LOS MISMOS: lo que cambia es el numeral que
    // se cita. Lo propio de la nota -origen, motivo, coherencia de moneda- lo
    // valida el handler, que es quien tiene el documento original a mano.
    public static FacturaValidacion ValidarNota(
        FacturaEmitirCommand comando, FacturaImprentaDatos imprenta, string tipoContribuyente)
    {
        string tipo = (comando.TipoDocumento ?? string.Empty).Trim().ToLowerInvariant();
        string articulo = FacturaNumerales.ArticuloParaNota(tipoContribuyente);

        if (!FacturacionElectronicaDb.TiposNota.Contains(tipo))
        {
            return Invalida(articulo, new FacturaFalta(
                FacturaNumerales.Numeral(Concepto.TipoDocumento, articulo),
                "esta vía solo emite notas de débito o de crédito (Art. 22 de la Providencia SNAT/2011/00071)."));
        }

        var faltantes = ValidarContenido(comando, tipo, articulo);

        // Art. 13.14 / 15.11 - moneda extranjera con ambos montos y el tipo de
        // cambio. NO se valida en la factura: el Art. 7 de la 102 no lo exige, y
        // exigirlo ahi seria inventar una obligacion. La asimetria es de la norma.
        string moneda = (comando.Moneda ?? string.Empty).Trim().ToUpperInvariant();

        if (moneda.Length > 0 && moneda != "VES" && comando.TasaCambio <= 0)
        {
            faltantes.Add(new FacturaFalta(
                FacturaNumerales.Numeral(Concepto.MonedaSinTasa, articulo),
                $"la operación está en {moneda} y no trae el tipo de cambio."));
        }

        return new FacturaValidacion(faltantes.Count == 0, faltantes, articulo);
    }

    // ------------------------------------------------------------------
    // El contenido comun, escrito una sola vez
    // ------------------------------------------------------------------

    private static List<FacturaFalta> ValidarContenido(
        FacturaEmitirCommand comando, string tipo, string articulo)
    {
        var faltantes = new List<FacturaFalta>();

        // Datos del adquiriente. Se puede prescindir del RIF para personas
        // naturales que no requieran la factura a efectos tributarios, PERO en ese
        // caso se exige como minimo cedula de identidad o pasaporte. No es que el
        // dato sea opcional: es que hay dos formas de cumplirlo.
        //
        // La nota de entrega queda fuera: el Art. 10.2 no remite al 7.7. Lleva
        // datos del receptor, que son otra cosa y son de la Fase 6.
        if (tipo != "entrega")
        {
            bool sinIdentificacion =
                string.IsNullOrWhiteSpace(comando.AdqRif)
                && string.IsNullOrWhiteSpace(comando.AdqDocumentoId);

            if (sinIdentificacion)
            {
                faltantes.Add(new FacturaFalta(
                    FacturaNumerales.Numeral(Concepto.AdquirienteIdentificado, articulo),
                    "falta el RIF del adquiriente o, en su defecto, su cédula o pasaporte."));
            }

            if (string.IsNullOrWhiteSpace(comando.AdqNombre))
            {
                faltantes.Add(new FacturaFalta(
                    FacturaNumerales.Numeral(Concepto.AdquirienteNombrado, articulo),
                    "falta el nombre o razón social del adquiriente."));
            }
        }

        // Descripcion y precio de cada renglon. Un documento sin renglones no
        // describe ninguna operacion, y el numeral pide describirla.
        if (comando.Renglones is null || comando.Renglones.Count == 0)
        {
            faltantes.Add(new FacturaFalta(
                FacturaNumerales.Numeral(Concepto.RenglonesPresentes, articulo),
                "el documento no tiene renglones que describan la operación."));

            return faltantes;
        }

        for (int i = 0; i < comando.Renglones.Count; i++)
        {
            var renglon = comando.Renglones[i];
            int numero = i + 1;

            if (string.IsNullOrWhiteSpace(renglon.Descripcion))
            {
                faltantes.Add(new FacturaFalta(
                    FacturaNumerales.Numeral(Concepto.RenglonDescripcion, articulo),
                    $"el renglón {numero} no tiene descripción."));
            }

            if (renglon.Precio < 0)
            {
                faltantes.Add(new FacturaFalta(
                    FacturaNumerales.Numeral(Concepto.RenglonPrecio, articulo),
                    $"el renglón {numero} tiene un precio negativo."));
            }

            // La cantidad se exige cuando el precio refiere a varios bienes o
            // servicios iguales. Cero o negativo no describe nada.
            if (renglon.Cantidad <= 0)
            {
                faltantes.Add(new FacturaFalta(
                    FacturaNumerales.Numeral(Concepto.RenglonCantidad, articulo),
                    $"el renglón {numero} tiene una cantidad inválida."));
            }

            // Coherencia entre el hecho y la tasa. La misma regla que sostiene el
            // CHECK de la tabla: si un renglon fuera exento Y gravado, la
            // discriminacion por alicuota no cerraria contra el total.
            if (renglon.Exento && renglon.Alicuota > 0)
            {
                faltantes.Add(new FacturaFalta(
                    FacturaNumerales.Numeral(Concepto.ExentoConAlicuota, articulo),
                    $"el renglón {numero} está marcado exento pero tiene alícuota."));
            }

            if (renglon.Alicuota < 0 || renglon.Alicuota > 100)
            {
                faltantes.Add(new FacturaFalta(
                    FacturaNumerales.Numeral(Concepto.AlicuotaFueraDeRango, articulo),
                    $"el renglón {numero} tiene una alícuota fuera de rango."));
            }

            // Los ajustes al precio se informan "con descripcion y valor". Un
            // ajuste sin decir por que es un descuento anonimo en un documento
            // fiscal.
            if (renglon.AjusteValor != 0 && string.IsNullOrWhiteSpace(renglon.AjusteDescripcion))
            {
                faltantes.Add(new FacturaFalta(
                    FacturaNumerales.Numeral(Concepto.AjusteSinDescripcion, articulo),
                    $"el renglón {numero} tiene un ajuste al precio sin descripción."));
            }
        }

        return faltantes;
    }

    private static FacturaValidacion Invalida(string articulo, FacturaFalta falta) =>
        new(false, [falta], articulo);

    // El numeral 7.14 decide si el documento puede ser definitivo. Se expone
    // aparte porque la respuesta no es "valido o invalido" sino "de prueba o
    // definitivo", y confundir las dos cosas llevaria a rechazar emisiones que hoy
    // son perfectamente correctas.
    //
    // Este es el unico numeral que hoy NO se puede cumplir, y no por un descuido:
    // la providencia no existe hasta que el SENIAT autorice a Ossmmasoft. Por eso
    // no bloquea la emision, marca el documento como de prueba. Emitir en modo
    // prueba es correcto; emitir un documento DEFINITIVO sin ese dato no lo es, y
    // eso lo impide el CHECK de la tabla.
    public static bool EsDocumentoDePrueba(FacturaImprentaDatos imprenta) => !imprenta.EsDefinitivo;
}
