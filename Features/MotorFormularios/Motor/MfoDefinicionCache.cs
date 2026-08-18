using Oracle.ManagedDataAccess.Client;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Helpers;
using System.Collections.Concurrent;
using System.Data;

namespace OssmmasoftVerticalSlice.Features.MotorFormularios;

/// <summary>
/// Cache en memoria de la definicion compuesta, por VERSION_ID.
///
/// Es segura sin estrategia de invalidacion porque una version PUBLICADA es
/// inmutable: los triggers de la base lo garantizan, no una convencion del
/// backend. Esa es la consecuencia elegante del versionado inmutable, y la razon
/// por la que esta cache solo crece y nunca se invalida por VERSION_ID.
///
/// Lo que si cambia es **cual** version corresponde a un alias: al publicar o
/// archivar, el alias apunta a otra. Por eso la resolucion alias -> version se
/// cachea aparte y esa si se invalida.
///
/// Las versiones en BORRADOR no se cachean nunca: son justamente las que se
/// estan editando.
/// </summary>
public class MfoDefinicionCache
{
    private readonly ConcurrentDictionary<int, MfoDefinicionResponse> _porVersion = new();
    private readonly ConcurrentDictionary<string, int> _aliasVigente = new(StringComparer.OrdinalIgnoreCase);

    public void InvalidarAlias()
    {
        _aliasVigente.Clear();
    }

    /// <summary>
    /// Solo para pruebas y para el caso extremo de una version publicada que se
    /// haya tocado por fuera de la aplicacion.
    /// </summary>
    public void InvalidarTodo()
    {
        _porVersion.Clear();
        _aliasVigente.Clear();
    }

    public async Task<ResultDto<MfoDefinicionResponse>> ObtenerAsync(
        ConnectionDB connectionDB, int? versionId, string? alias)
    {
        if (versionId is null && string.IsNullOrWhiteSpace(alias))
        {
            return MfoDb.Invalid<MfoDefinicionResponse>("Indique la version por id o el formulario por alias.");
        }

        // Camino rapido: alias ya resuelto y su definicion ya cacheada.
        if (versionId is null && _aliasVigente.TryGetValue(alias!, out var cachedVersionId)
            && _porVersion.TryGetValue(cachedVersionId, out var cachedPorAlias))
        {
            return Ok(cachedPorAlias);
        }

        if (versionId is not null && _porVersion.TryGetValue(versionId.Value, out var cached))
        {
            return Ok(cached);
        }

        var cargada = await CargarAsync(connectionDB, versionId, alias);
        if (!cargada.IsValid || cargada.Data is null)
        {
            return cargada;
        }

        var definicion = cargada.Data;

        // Solo se cachea lo inmutable.
        if (string.Equals(definicion.Estado, "PUBLICADA", StringComparison.OrdinalIgnoreCase))
        {
            _porVersion[definicion.VersionId] = definicion;

            if (!string.IsNullOrWhiteSpace(definicion.Alias))
            {
                _aliasVigente[definicion.Alias] = definicion.VersionId;
            }
        }

        return cargada;
    }

    private static ResultDto<MfoDefinicionResponse> Ok(MfoDefinicionResponse data)
    {
        return new ResultDto<MfoDefinicionResponse>(data) { Data = data, IsValid = true, Message = string.Empty };
    }

    /// <summary>
    /// Lee los seis ref cursors de SP_MFO_VER_GET_FULL y arma el objeto anidado.
    /// El ensamblado se hace aqui, con diccionarios por id, y no con LINQ sobre
    /// listas: un formulario grande tiene cientos de opciones y reglas, y un
    /// Where por cada campo seria cuadratico.
    /// </summary>
    private static async Task<ResultDto<MfoDefinicionResponse>> CargarAsync(
        ConnectionDB connectionDB, int? versionId, string? alias)
    {
        using var cn = connectionDB.GetMfoConnection();
        var openError = await MfoDb.TryOpenAsync(cn);
        if (openError is not null)
        {
            return MfoDb.Invalid<MfoDefinicionResponse>(openError);
        }

        using var cmd = MfoDb.StoredProcedure("SP_MFO_VER_GET_FULL", cn);
        cmd.Parameters.Add("p_VersionId", OracleDbType.Int32).Value = MfoDb.DbValue(versionId);
        cmd.Parameters.Add("p_Alias", OracleDbType.Varchar2).Value = MfoDb.DbValue(alias);
        cmd.Parameters.Add("p_Version", OracleDbType.RefCursor, ParameterDirection.Output);
        cmd.Parameters.Add("p_Secciones", OracleDbType.RefCursor, ParameterDirection.Output);
        cmd.Parameters.Add("p_Campos", OracleDbType.RefCursor, ParameterDirection.Output);
        cmd.Parameters.Add("p_Opciones", OracleDbType.RefCursor, ParameterDirection.Output);
        cmd.Parameters.Add("p_Reglas", OracleDbType.RefCursor, ParameterDirection.Output);
        cmd.Parameters.Add("p_Condiciones", OracleDbType.RefCursor, ParameterDirection.Output);
        var pMessage = cmd.Parameters.Add("p_Message", OracleDbType.Varchar2, 4000, null, ParameterDirection.Output);

        int vVersionId = 0, vFormularioId = 0, vNumero = 0;
        string vEstado = string.Empty, vHash = string.Empty, vAlias = string.Empty;
        string vNombre = string.Empty, vDescripcion = string.Empty, vCategoria = string.Empty;
        string vModoUso = string.Empty, vEntidadDestino = string.Empty;
        bool vRegistraEjec = false, vPermiteBorrador = false;
        var encontrada = false;

        var secciones = new List<MfoSeccionResponse>();
        var campos = new List<MfoCampoResponse>();
        var opciones = new List<MfoOpcionResponse>();
        var reglas = new List<MfoReglaResponse>();
        var condiciones = new List<MfoCondicionResponse>();

        using (var reader = await cmd.ExecuteReaderAsync())
        {
            if (await reader.ReadAsync())
            {
                encontrada = true;
                vVersionId = reader.SafeGetInt32("VERSION_ID");
                vFormularioId = reader.SafeGetInt32("FORMULARIO_ID");
                vNumero = reader.SafeGetInt32("NUMERO");
                vEstado = reader.SafeGetString("ESTADO");
                vHash = reader.SafeGetString("HASH_DEF");
                vAlias = reader.SafeGetString("ALIAS");
                vNombre = reader.SafeGetString("NOMBRE");
                vDescripcion = reader.SafeGetString("DESCRIPCION");
                vCategoria = reader.SafeGetString("CATEGORIA");
                vModoUso = reader.SafeGetString("MODO_USO");
                vEntidadDestino = reader.SafeGetString("ENTIDAD_DESTINO");
                vRegistraEjec = MfoDb.ToBool(reader, "REGISTRA_EJEC");
                vPermiteBorrador = MfoDb.ToBool(reader, "PERMITE_BORRADOR");
            }

            if (await reader.NextResultAsync())
            {
                while (await reader.ReadAsync()) secciones.Add(MfoDb.MapSeccion(reader));
            }

            if (await reader.NextResultAsync())
            {
                while (await reader.ReadAsync()) campos.Add(MfoDb.MapCampo(reader));
            }

            if (await reader.NextResultAsync())
            {
                while (await reader.ReadAsync()) opciones.Add(MfoDb.MapOpcion(reader));
            }

            if (await reader.NextResultAsync())
            {
                while (await reader.ReadAsync()) reglas.Add(MfoDb.MapRegla(reader));
            }

            if (await reader.NextResultAsync())
            {
                while (await reader.ReadAsync()) condiciones.Add(MfoDb.MapCondicion(reader));
            }
        }

        var message = MfoDb.GetMessage(pMessage);
        if (!MfoDb.IsSuccessMessage(message) || !encontrada)
        {
            return MfoDb.Invalid<MfoDefinicionResponse>(
                MfoDb.IsSuccessMessage(message) ? "La version indicada no existe." : message);
        }

        // Indices por id para que el ensamblado sea lineal.
        var opcionesPorCampo = new Dictionary<int, List<MfoOpcionResponse>>();
        foreach (var o in opciones)
        {
            if (!opcionesPorCampo.TryGetValue(o.CampoId, out var lista))
            {
                lista = [];
                opcionesPorCampo[o.CampoId] = lista;
            }
            lista.Add(o);
        }

        var reglasPorCampo = new Dictionary<int, List<MfoReglaResponse>>();
        foreach (var g in reglas)
        {
            if (!reglasPorCampo.TryGetValue(g.CampoId, out var lista))
            {
                lista = [];
                reglasPorCampo[g.CampoId] = lista;
            }
            lista.Add(g);
        }

        var camposPorSeccion = new Dictionary<int, List<MfoCampoDefinicion>>();
        foreach (var c in campos)
        {
            if (!camposPorSeccion.TryGetValue(c.SeccionId, out var lista))
            {
                lista = [];
                camposPorSeccion[c.SeccionId] = lista;
            }

            lista.Add(new MfoCampoDefinicion(
                c,
                opcionesPorCampo.TryGetValue(c.CampoId, out var ops) ? ops : [],
                reglasPorCampo.TryGetValue(c.CampoId, out var rgs) ? rgs : []));
        }

        var seccionesDef = secciones
            .Select(s => new MfoSeccionDefinicion(
                s,
                camposPorSeccion.TryGetValue(s.SeccionId, out var cps) ? cps : []))
            .ToList();

        var definicion = new MfoDefinicionResponse(
            vVersionId, vFormularioId, vNumero, vEstado, vHash, vAlias, vNombre,
            vDescripcion, vCategoria, vModoUso, vRegistraEjec, vPermiteBorrador,
            vEntidadDestino, seccionesDef, condiciones);

        return Ok(definicion);
    }
}
