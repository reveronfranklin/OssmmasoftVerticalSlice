namespace OssmmasoftVerticalSlice.Features.FacturacionElectronica;

// T4.5 - calculo de totales, DISCRIMINADOS POR ALICUOTA.
//
//   7.11  base imponible discriminada por alicuota, indicando el porcentaje
//         aplicable, mas el monto total exento o exonerado
//   7.12  monto total del IVA, discriminado por alicuota
//   7.13  valor total de las operaciones
//
// Puro a proposito: entra la lista de renglones, salen los totales. Sin base, sin
// configuracion y sin reloj. Un calculo fiscal que necesita infraestructura para
// comprobarse no se comprueba nunca.
//
// EL REDONDEO ES PARTE DEL RESULTADO, no un detalle de implementacion. Se redondea
// a dos decimales POR ALICUOTA y no al final, y el orden importa: sumar sin
// redondear y redondear el total da un numero distinto que redondear cada grupo y
// sumarlos. El documento impreso muestra los subtotales por alicuota, asi que
// tienen que ser esos los que sumen al total. Si no, el papel no cuadra consigo
// mismo, y eso en un documento fiscal no es un centavo: es una inconsistencia
// visible.
//
// MidpointRounding.AwayFromZero y no el banquero: 0,125 va a 0,13. Es lo que hace
// una calculadora y lo que espera quien revisa una factura a mano.
public static class FacturaCalculo
{
    private const int Decimales = 2;

    public static FacturaTotales Calcular(List<FacturaRenglonCommand> renglones)
    {
        var porAlicuota = new Dictionary<decimal, (decimal Base, decimal Iva)>();
        var totalesRenglon = new List<decimal>();

        decimal totalExento = 0;

        foreach (var renglon in renglones ?? [])
        {
            // 7.10 - el ajuste al precio entra antes de calcular impuestos: un
            // descuento reduce la base imponible, no el IVA ya calculado.
            decimal bruto = Redondear(renglon.Cantidad * renglon.Precio);
            decimal neto = Redondear(bruto + renglon.AjusteValor);

            // Un ajuste no puede dejar el renglon en negativo: eso no es un
            // descuento, es una nota de credito, y va en otro documento.
            if (neto < 0)
            {
                neto = 0;
            }

            totalesRenglon.Add(neto);

            // 7.11 - el monto exento o exonerado se informa aparte, no como una
            // alicuota de cero. Son dos cosas distintas en el documento: una es
            // base gravada al 0 % y la otra es operacion no gravada.
            if (renglon.Exento || renglon.Alicuota == 0)
            {
                totalExento += neto;

                continue;
            }

            decimal tasa = renglon.Alicuota;

            if (!porAlicuota.TryGetValue(tasa, out var acumulado))
            {
                acumulado = (0, 0);
            }

            porAlicuota[tasa] = (acumulado.Base + neto, 0);
        }

        // El IVA se calcula sobre la base YA acumulada de cada alicuota, no
        // renglon por renglon. Calcularlo por renglon y sumar arrastra un error de
        // redondeo por cada linea; sobre la base agrupada hay un solo redondeo, y
        // es el que el documento muestra.
        var detalle = new List<FacturaTotalPorAlicuota>();

        decimal totalBase = 0;
        decimal totalIva = 0;

        foreach (var grupo in porAlicuota.OrderByDescending(g => g.Key))
        {
            decimal baseImponible = Redondear(grupo.Value.Base);
            decimal iva = Redondear(baseImponible * grupo.Key / 100m);

            detalle.Add(new FacturaTotalPorAlicuota(grupo.Key, baseImponible, iva));

            totalBase += baseImponible;
            totalIva += iva;
        }

        totalExento = Redondear(totalExento);

        // 7.13 - valor total de las operaciones. Es la suma de lo que el documento
        // muestra: base gravada mas IVA mas lo exento.
        decimal totalGeneral = Redondear(totalBase + totalIva + totalExento);

        return new FacturaTotales(totalExento, totalBase, totalIva, totalGeneral, detalle, totalesRenglon);
    }

    private static decimal Redondear(decimal valor) =>
        Math.Round(valor, Decimales, MidpointRounding.AwayFromZero);
}
