using System.Data;
using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using Oracle.ManagedDataAccess.Client;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Helpers;

namespace OssmmasoftVerticalSlice.Features.RhFamiliares;

public record RhFamiliarRequest(int CodigoPersona, int CodigoFamiliar, int CedulaFamiliar,
    string Nacionalidad, string Nombre, string Apellido, string FechaNacimientoString,
    int ParienteId, string Sexo, int NivelEducativo, int Grado, JsonElement EstadoSaludId = default);
public record RhFamiliarFilter(int CodigoPersona);
public record RhFamiliarDelete(int CodigoFamiliar);
public record RhFecha(string Year, string Month, string Day);
public record RhFamiliarResponse(int CodigoPersona, int CodigoFamiliar, int CedulaFamiliar,
    string Nacionalidad, string Nombre, string Apellido, DateTime FechaNacimiento,
    string FechaNacimientoString, RhFecha FechaNacimientoObj, string Edad, int ParienteId,
    string ParienteDescripcion, string Sexo, int NivelEducativo, int Grado,
    int? EstadoSaludId, string EstadoSaludDescripcion);

internal static class RhFamiliaresDb
{
    internal static object DbValue(object? value) => value ?? DBNull.Value;
    internal static bool Success(string message) => message.Equals("success", StringComparison.OrdinalIgnoreCase)
        || message.Equals("suscces", StringComparison.OrdinalIgnoreCase);
    internal static RhFamiliarResponse Map(IDataReader r)
    {
        int Number(string n) => r.IsDBNull(r.GetOrdinal(n)) ? 0 : Convert.ToInt32(r[n]);
        string Text(string n) => r[n]?.ToString() ?? "";
        var date = Convert.ToDateTime(r["FECHA_NACIMIENTO"]);
        var today = DateTime.Today;
        var years = today.Year - date.Year;
        if (date.Date > today.AddYears(-years)) years--;
        var months = (today.Year - date.Year) * 12 + today.Month - date.Month;
        if (today.Day < date.Day) months--;
        return new(Number("CODIGO_PERSONA"), Number("CODIGO_FAMILIAR"), Number("CEDULA_FAMILIAR"),
            Text("NACIONALIDAD"), Text("NOMBRE"), Text("APELLIDO"), date,
            date.ToString("dd/MM/yyyy"), new(date.ToString("yyyy"), date.ToString("MM"), date.ToString("dd")),
            years > 0 ? $"{years} Años" : $"{Math.Max(0, months)} Meses", Number("PARIENTE_ID"),
            Text("PARIENTE_DESCRIPCION"), Text("SEXO"), Number("NIVEL_EDUCATIVO_ID"), Number("GRADO"),
            r.IsDBNull(r.GetOrdinal("ESTADO_SALUD_ID")) ? null : Number("ESTADO_SALUD_ID"), Text("ESTADO_SALUD_DESCRIPCION"));
    }

    internal static async Task<ResultDto<T>> Execute<T>(ConnectionDB db, IConfiguration config,
        string procedure, Action<OracleCommand> bind, Func<OracleCommand, Task<T>> read)
    {
        ResultDto<T> Fail(string message) => new(default!) { IsValid = false, Message = message };
        if (!int.TryParse(config["settings:EmpresaConfig"], out var empresa) || empresa <= 0)
            return Fail("EmpresaConfig debe ser un numero valido.");
        using var cn = db.GetRhConnection();
        try { await cn.OpenAsync(); }
        catch (Exception ex) { return Fail($"Error tecnico al abrir conexion RH: {ex.Message}"); }
        using var cmd = new OracleCommand(procedure, cn) { CommandType = CommandType.StoredProcedure, BindByName = true };
        cmd.Parameters.Add("p_EMPRESA", OracleDbType.Int32).Value = empresa;
        var message = cmd.Parameters.Add("p_MESSAGE", OracleDbType.Varchar2, 4000, null, ParameterDirection.Output);
        try
        {
            using var transaction = cn.BeginTransaction();
            cmd.Transaction = transaction;
            bind(cmd);
            var data = await read(cmd);
            var text = message.Value?.ToString() ?? "Sin respuesta de BD";
            if (Success(text)) transaction.Commit();
            else transaction.Rollback();
            return new(Success(text) ? data : default!) { IsValid = Success(text), Message = text };
        }
        catch (Exception ex) { return Fail($"Error tecnico: {ex.Message}"); }
    }

    internal static Task<ResultDto<RhFamiliarResponse>> Save(ConnectionDB db, IConfiguration config,
        RhFamiliarRequest request, bool create, ClaimsPrincipal user)
    {
        ResultDto<RhFamiliarResponse> Fail(string message) => new(default!) { IsValid = false, Message = message };
        if (request.CodigoPersona <= 0 || (!create && request.CodigoFamiliar <= 0))
            return Task.FromResult(Fail("Persona o familiar invalido."));
        if (request.CedulaFamiliar <= 0 || string.IsNullOrWhiteSpace(request.Nombre) || string.IsNullOrWhiteSpace(request.Apellido))
            return Task.FromResult(Fail("Cedula, nombre y apellido son obligatorios."));
        if (request.Nacionalidad is not ("V" or "E") || request.Sexo is not ("M" or "F"))
            return Task.FromResult(Fail("Nacionalidad o sexo invalido."));
        DateTime date;
        var formats = new[] { "dd/MM/yyyy", "yyyy-MM-dd", "yyyy-MM-ddTHH:mm:ss.FFFFFFFK" };
        if (!DateTime.TryParseExact(request.FechaNacimientoString, formats, CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind, out date))
            return Task.FromResult(Fail("Fecha de nacimiento invalida."));
        int? estado = null;
        var specified = request.EstadoSaludId.ValueKind != JsonValueKind.Undefined;
        if (specified && request.EstadoSaludId.ValueKind != JsonValueKind.Null)
        {
            if (request.EstadoSaludId.ValueKind != JsonValueKind.Number || !request.EstadoSaludId.TryGetInt32(out var id) || id <= 0 || id > 99999)
                return Task.FromResult(Fail("Estado de salud invalido."));
            estado = id;
        }
        return Execute(db, config, "RH.SP_RH_FAM_SAVE", cmd =>
        {
            cmd.Parameters.Add("p_CREAR", OracleDbType.Int32).Value = create ? 1 : 0;
            cmd.Parameters.Add("p_PERSONA", OracleDbType.Int32).Value = request.CodigoPersona;
            cmd.Parameters.Add("p_FAMILIAR", OracleDbType.Int32).Value = request.CodigoFamiliar;
            cmd.Parameters.Add("p_CEDULA", OracleDbType.Int32).Value = request.CedulaFamiliar;
            cmd.Parameters.Add("p_NACIONALIDAD", OracleDbType.Varchar2).Value = request.Nacionalidad;
            cmd.Parameters.Add("p_NOMBRE", OracleDbType.Varchar2).Value = request.Nombre.Trim();
            cmd.Parameters.Add("p_APELLIDO", OracleDbType.Varchar2).Value = request.Apellido.Trim();
            cmd.Parameters.Add("p_FECHA", OracleDbType.Date).Value = date;
            cmd.Parameters.Add("p_PARIENTE", OracleDbType.Int32).Value = request.ParienteId;
            cmd.Parameters.Add("p_SEXO", OracleDbType.Varchar2).Value = request.Sexo;
            cmd.Parameters.Add("p_NIVEL", OracleDbType.Int32).Value = request.NivelEducativo;
            cmd.Parameters.Add("p_GRADO", OracleDbType.Int32).Value = request.Grado;
            cmd.Parameters.Add("p_ESTADO", OracleDbType.Int32).Value = DbValue(estado);
            cmd.Parameters.Add("p_ESTADO_PRESENTE", OracleDbType.Int32).Value = specified ? 1 : 0;
            cmd.Parameters.Add("p_LOGIN", OracleDbType.Varchar2).Value = DbValue(user.FindFirstValue(ClaimTypes.Name) ?? user.FindFirstValue("unique_name"));
            cmd.Parameters.Add("p_RESULT", OracleDbType.RefCursor, ParameterDirection.Output);
        }, async cmd =>
        {
            using var reader = await cmd.ExecuteReaderAsync();
            return await reader.ReadAsync() ? Map(reader) : default!;
        });
    }
}
