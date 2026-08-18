using Oracle.ManagedDataAccess.Client;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Helpers;
using System.Data;

namespace OssmmasoftVerticalSlice.Features.MotorFormularios;

/// <summary>
/// Lista blanca de catalogos. Es el unico punto del sistema autorizado a decidir
/// que consulta se ejecuta para resolver un <c>CATALOGO_CLAVE</c>.
///
/// **La clave que viene de la base de datos NUNCA se concatena en SQL.** Se usa
/// como clave de busqueda en este diccionario; si no esta registrada, la
/// resolucion falla con <c>IsValid = false</c> y no se ejecuta nada. Es el mismo
/// principio que rige la lista blanca de reportes de la Fase 9, y por la misma
/// razon: un motor que ejecuta lo que dice una fila de base de datos es
/// ejecucion arbitraria configurable.
///
/// Agregar un catalogo nuevo **requiere despliegue**. Es deliberado y hay que
/// decirlo: lo que el motor elimina es el trabajo repetido de la pantalla, no el
/// control sobre que datos se exponen.
/// </summary>
public static class MfoCatalogoRegistro
{
    private delegate Task<List<MfoCatalogoOpcionResponse>> Resolutor(ConnectionDB conexiones, int empresa);

    private static readonly Dictionary<string, Resolutor> Registro =
        new(StringComparer.OrdinalIgnoreCase)
        {
            // Unidades de trabajo (ICP) de Bienes Municipales. Es el catalogo del
            // campo CODIGOS_ICP del formulario de referencia REP_BM1.
            ["BM_ICP"] = ResolverBmIcp
        };

    public static bool EstaRegistrado(string? clave)
    {
        return !string.IsNullOrWhiteSpace(clave) && Registro.ContainsKey(clave);
    }

    public static IEnumerable<string> ClavesRegistradas() => Registro.Keys.OrderBy(k => k);

    public static async Task<ResultDto<List<MfoCatalogoOpcionResponse>>> ResolverAsync(
        ConnectionDB conexiones, string? clave, int empresa)
    {
        if (!EstaRegistrado(clave))
        {
            return MfoDb.InvalidList<MfoCatalogoOpcionResponse>(
                $"El catalogo '{clave}' no esta registrado en el backend.");
        }

        try
        {
            var opciones = await Registro[clave!](conexiones, empresa);

            return new ResultDto<List<MfoCatalogoOpcionResponse>>(opciones)
            {
                Data = opciones,
                IsValid = true,
                Message = string.Empty,
                CantidadRegistros = opciones.Count
            };
        }
        catch (Exception ex)
        {
            return MfoDb.InvalidList<MfoCatalogoOpcionResponse>($"Error tecnico: {ex.Message}");
        }
    }

    /// <summary>
    /// Se conecta al schema BM, no al de MFO: el catalogo son datos vivos de
    /// Bienes Municipales y el motor no los duplica.
    /// </summary>
    private static async Task<List<MfoCatalogoOpcionResponse>> ResolverBmIcp(
        ConnectionDB conexiones, int empresa)
    {
        using var cn = conexiones.GetBmConnection();
        await cn.OpenAsync();

        using var cmd = new OracleCommand("BM.SP_BM1_GET_LIST_ICP", cn)
        {
            CommandType = CommandType.StoredProcedure,
            BindByName = true
        };

        cmd.Parameters.Add("p_CodigoEmpresa", OracleDbType.Int32).Value = empresa;
        cmd.Parameters.Add("p_ResultSet", OracleDbType.RefCursor, ParameterDirection.Output);
        cmd.Parameters.Add("p_Message", OracleDbType.Varchar2, 4000, null, ParameterDirection.Output);
        cmd.Parameters.Add("p_TotalRecords", OracleDbType.Int32, ParameterDirection.Output);

        var lista = new List<MfoCatalogoOpcionResponse>();

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var codigo = reader.SafeGetInt32("CODIGO_ICP").ToString();
            lista.Add(new MfoCatalogoOpcionResponse(codigo, reader.SafeGetString("UNIDAD_TRABAJO")));
        }

        return lista;
    }
}
