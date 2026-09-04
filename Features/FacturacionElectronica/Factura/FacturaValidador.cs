namespace OssmmasoftVerticalSlice.Features.FacturacionElectronica;

// T4.4 - validador de estructura, previo a la asignacion del numero de control.
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
public static class FacturaValidador
{
    public static FacturaValidacion Validar(FacturaEmitirCommand comando, FacturaImprentaDatos imprenta)
    {
        var faltantes = new List<string>();

        // 7.1 - denominacion del documento. Se deriva del tipo, asi que lo que se
        // valida es que el tipo este dentro del alcance documental DOC-1 a DOC-4.
        string tipo = (comando.TipoDocumento ?? string.Empty).Trim().ToLowerInvariant();

        if (!FacturacionElectronicaDb.TiposDocumento.Contains(tipo))
        {
            faltantes.Add("7.1: el tipo de documento debe ser factura, débito, crédito o entrega.");
        }

        // 7.2 - la numeracion consecutiva y unica la genera el sistema (D-21), asi
        // que aca no se exige. Lo que si se valida es la coherencia del modo: un
        // emisor en modo 'sistema' no puede traer numeracion externa, porque
        // mezclarlas sobre la misma serie es como se choca INV-3. El modo vive en
        // el emisor y lo comprueba el handler, que es quien lo tiene a mano.

        // 7.7 - datos del adquiriente. Se puede prescindir del RIF para personas
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
                faltantes.Add("7.7: falta el RIF del adquiriente o, en su defecto, su cédula o pasaporte.");
            }

            if (string.IsNullOrWhiteSpace(comando.AdqNombre))
            {
                faltantes.Add("7.7: falta el nombre o razón social del adquiriente.");
            }
        }

        // 7.8 - descripcion y precio de cada renglon. Un documento sin renglones no
        // describe ninguna operacion, y el numeral pide describirla.
        if (comando.Renglones is null || comando.Renglones.Count == 0)
        {
            faltantes.Add("7.8: el documento no tiene renglones que describan la operación.");
        }
        else
        {
            for (int i = 0; i < comando.Renglones.Count; i++)
            {
                var renglon = comando.Renglones[i];
                int numero = i + 1;

                if (string.IsNullOrWhiteSpace(renglon.Descripcion))
                {
                    faltantes.Add($"7.8: el renglón {numero} no tiene descripción.");
                }

                if (renglon.Precio < 0)
                {
                    faltantes.Add($"7.8: el renglón {numero} tiene un precio negativo.");
                }

                // 7.8 - la cantidad se exige cuando el precio refiere a varios
                // bienes o servicios iguales. Cero o negativo no describe nada.
                if (renglon.Cantidad <= 0)
                {
                    faltantes.Add($"7.8: el renglón {numero} tiene una cantidad inválida.");
                }

                // Coherencia entre el hecho y la tasa. La misma regla que sostiene
                // el CHECK de la tabla: si un renglon fuera exento Y gravado, la
                // discriminacion por alicuota del 7.11 no cerraria contra el total
                // del 7.13.
                if (renglon.Exento && renglon.Alicuota > 0)
                {
                    faltantes.Add($"7.11: el renglón {numero} está marcado exento pero tiene alícuota.");
                }

                if (renglon.Alicuota < 0 || renglon.Alicuota > 100)
                {
                    faltantes.Add($"7.11: el renglón {numero} tiene una alícuota fuera de rango.");
                }

                // 7.10 - los ajustes al precio se informan "con descripcion y
                // valor". Un ajuste sin decir por que es un descuento anonimo en
                // un documento fiscal.
                if (renglon.AjusteValor != 0 && string.IsNullOrWhiteSpace(renglon.AjusteDescripcion))
                {
                    faltantes.Add($"7.10: el renglón {numero} tiene un ajuste al precio sin descripción.");
                }
            }
        }

        // 7.14 - razon social y RIF de la imprenta digital, MAS la nomenclatura y
        // fecha de su Providencia de autorizacion.
        //
        // Este es el unico numeral que hoy NO se puede cumplir, y no por un
        // descuido: la providencia no existe hasta que el SENIAT autorice a
        // Ossmmasoft. Por eso no bloquea la emision, marca el documento como de
        // prueba. Emitir en modo prueba es correcto; emitir un documento
        // DEFINITIVO sin ese dato no lo es, y eso lo impide el CHECK de la tabla.
        if (!imprenta.EsDefinitivo)
        {
            // No entra en faltantes: no es culpa del documento ni del emisor.
        }

        return new FacturaValidacion(faltantes.Count == 0, faltantes);
    }

    // El numeral 7.14 decide si el documento puede ser definitivo. Se expone
    // aparte porque la respuesta no es "valido o invalido" sino "de prueba o
    // definitivo", y confundir las dos cosas llevaria a rechazar emisiones que hoy
    // son perfectamente correctas.
    public static bool EsDocumentoDePrueba(FacturaImprentaDatos imprenta) => !imprenta.EsDefinitivo;
}
