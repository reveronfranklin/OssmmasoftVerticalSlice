using Oracle.ManagedDataAccess.Client;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Helpers;

namespace OssmmasoftVerticalSlice.Features.FacturacionElectronica;

// Catalogo de alicuotas de IVA. Decision D-22.
//
// DE DONDE SALE. Del ERP, no de una tabla propia: ADM.ADM_DESCRIPTIVAS con
// TITULO_ID = 18, y el porcentaje en la columna EXTRA1. Es donde el ERP ya lo
// tiene y desde donde lo lee el modulo de ordenes de pago, asi que una factura y
// una orden de pago no pueden discrepar. Lo permite D-3: los datos maestros del
// ERP se leen en proceso, con los getters Oracle de ConnectionDB.
//
// POR QUE SE VALIDA IGUAL. El porcentaje vive en una columna generica y sin tipo,
// que guarda texto. En una orden de pago un dedazo en EXTRA1 es un bug que se
// corrige; en un documento fiscal es un IVA mal calculado en un papel con valor
// legal. Asi que lo que viene de ahi se comprueba antes de usarse, y lo que no
// parsea no se usa: se ignora esa fila y se sigue con las que si.
//
// Y POR QUE NO SE REFERENCIA. La alicuota aplicada se COPIA al documento
// (FED_DOC_IMPUESTO y FED_DOCUMENTO_DETALLE). Es el mismo principio de
// instantanea que ya rige para los datos del emisor: si manana el gobierno cambia
// la tasa, las facturas viejas conservan la que se les aplico. Un documento fiscal
// no puede cambiar porque cambio un catalogo.
public record AlicuotaResponse(decimal Valor, string Descripcion);

public static class FacturaAlicuotas
{
    // Titulo del catalogo de tipos de impuesto en las descriptivas del ERP. El 18
    // no es arbitrario: es el que usa AdmDetalleSolicitudService para resolver el
    // tipo de impuesto de un renglon.
    private const int TituloTipoImpuesto = 18;

    private const string SqlAlicuotas = @"
        SELECT D.DESCRIPCION_ID, D.DESCRIPCION, D.EXTRA1
          FROM ADM.ADM_DESCRIPTIVAS D
         WHERE D.TITULO_ID = :p_TITULO
           AND D.CODIGO_EMPRESA = :p_EMPRESA
         ORDER BY D.DESCRIPCION";

    public static async Task<ResultDto<List<AlicuotaResponse>>> ObtenerAsync(
        ConnectionDB connectionDB, IConfiguration config)
    {
        if (!int.TryParse(config["settings:EmpresaConfig"], out int empresa))
        {
            return Falla("No se pudo determinar la empresa desde settings:EmpresaConfig.");
        }

        using var cn = connectionDB.GetAdmConnection();

        try
        {
            await cn.OpenAsync();
        }
        catch (Exception ex)
        {
            return Falla($"Error técnico al abrir conexión ADM: {ex.Message}");
        }

        try
        {
            using var cmd = new OracleCommand(SqlAlicuotas, cn) { BindByName = true };
            cmd.Parameters.Add("p_TITULO", OracleDbType.Int32).Value = TituloTipoImpuesto;
            cmd.Parameters.Add("p_EMPRESA", OracleDbType.Int32).Value = empresa;

            var lista = new List<AlicuotaResponse>();
            var descartadas = new List<string>();

            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                string descripcion = reader.SafeGetString("DESCRIPCION");
                string crudo = reader.SafeGetString("EXTRA1");

                if (TryParsearPorcentaje(crudo, out decimal valor))
                {
                    lista.Add(new AlicuotaResponse(valor, descripcion));
                }
                else
                {
                    // No se lanza ni se inventa un valor: se descarta la fila y se
                    // deja constancia. Una alicuota ilegible es un dato de
                    // configuracion del ERP que alguien tiene que arreglar, no una
                    // razon para que el modulo entero deje de emitir.
                    descartadas.Add($"{descripcion} = '{crudo}'");
                }
            }

            string mensaje = descartadas.Count == 0
                ? FacturacionElectronicaDb.MensajeExito
                : "Hay alícuotas del catálogo con porcentaje ilegible y fueron descartadas: "
                  + string.Join("; ", descartadas);

            return new ResultDto<List<AlicuotaResponse>>(lista)
            {
                IsValid = true,
                Message = mensaje,
                CantidadRegistros = lista.Count,
                Total1 = descartadas.Count
            };
        }
        catch (Exception ex)
        {
            return Falla($"Error técnico: {ex.Message}");
        }
    }

    // El porcentaje llega como texto de una columna generica, asi que puede venir
    // con coma decimal, con punto, con el signo de porcentaje o con espacios. Se
    // aceptan las dos separaciones decimales porque la columna es libre y en la
    // practica conviven; lo que no se acepta es un valor fuera de rango.
    public static bool TryParsearPorcentaje(string crudo, out decimal valor)
    {
        valor = 0;

        if (string.IsNullOrWhiteSpace(crudo))
        {
            return false;
        }

        string limpio = crudo.Trim().Replace("%", string.Empty).Replace(",", ".").Trim();

        if (!decimal.TryParse(limpio, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out decimal parseado))
        {
            return false;
        }

        // Una alicuota negativa o mayor a 100 no es una tasa: es un dato mal
        // cargado. La tabla tiene el mismo CHECK, y aca se atrapa antes de llegar.
        if (parseado < 0 || parseado > 100)
        {
            return false;
        }

        valor = parseado;

        return true;
    }

    private static ResultDto<List<AlicuotaResponse>> Falla(string mensaje) =>
        new(null!) { Data = null, IsValid = false, Message = mensaje };
}
