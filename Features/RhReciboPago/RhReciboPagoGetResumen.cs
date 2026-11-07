using Microsoft.AspNetCore.Authorization;
using Oracle.ManagedDataAccess.Client;
using System.Data;
using OssmmasoftVerticalSlice.Features.Support;
using Microsoft.AspNetCore.Mvc;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Helpers;

namespace OssmmasoftVerticalSlice.Features.RhReciboPago;

public class RhReciboPagoGetResumenHandler(ConnectionDB db, IConfiguration config)
{
    public async Task<ResultDto<List<RhReciboResumen>>> HandleAsync(RhReciboPagoFilter filter, CancellationToken ct = default)
    {
        var data = await RhReciboPagoDb.ReadAsync(db, config, filter, true, ct);
        if (!data.IsValid || data.Data is null) return RhReciboPagoDb.Fail<List<RhReciboResumen>>(data.Message);
        var rows = RhReciboPagoDb.Summary(data.Data.Lineas);
        return new(rows) { IsValid = true, Message = "success", CantidadRegistros = rows.Count };
    }
}

[Authorize]
[ApiController]
[Route("api/RhReciboPago")]
public class RhReciboPagoGetResumenController(ConnectionDB db, IConfiguration config) : ControllerBase
{
    [HttpPost("GetResumen")]
    public async Task<IActionResult> GetResumen(RhReciboPagoFilter filter, CancellationToken ct)
    {
        var result = await new RhReciboPagoGetResumenHandler(db,config).HandleAsync(filter,ct);
        if (!result.IsValid || result.Data is null || !config.GetValue("settings:RhReciboEmail:Enabled",false)) return Ok(result);
        if (!SupportDb.TryGetEmpresa(config,out var company,out _)) return Ok(result);
        try
        {
            using var sis=db.GetSisConnection();await sis.OpenAsync(ct);
            var actor=await SupportSecurity.GetAuthenticatedUserIdAsync(sis,User,company,null);
            if (actor<=0) return Unauthorized();
            var permissions=await SupportSecurity.GetPermissionsAsync(sis,actor,company);
            if (!SupportSecurity.HasAny(permissions,RhReciboEmailRules.Permission)) return Ok(result);
            using var rh=db.GetRhConnection();await rh.OpenAsync(ct);
            using var cmd=RrmDb.Cmd(rh,"CONTACTOS_TODOS",null,RrmDb.N("p_EMPRESA",company));
            cmd.Parameters.Add("p_ROWS",OracleDbType.RefCursor,ParameterDirection.Output);
            var contacts=new Dictionary<int,List<ReciboEmailContacto>>();
            using (var reader=await cmd.ExecuteReaderAsync(ct)) while(await reader.ReadAsync(ct))
            {
                var person=reader.SafeGetInt32("CODIGO_PERSONA");
                if(!contacts.TryGetValue(person,out var list)) contacts[person]=list=[];
                list.Add(new(reader.SafeGetInt32("CODIGO_COMUNICACION"),reader.SafeGetString("CORREO"),reader.SafeGetInt32("PRINCIPAL")==1));
            }
            result.Data=result.Data.Select(row=> {
                var contact=RhReciboEmailRules.ResolveContact(contacts.GetValueOrDefault(row.CodigoPersona) ?? []);
                return row with { Correo=contact.Correo,EstadoCorreo=contact.Error.Length==0?"Valido":contact.Error };
            }).ToList();
            return Ok(result);
        }
        catch(OperationCanceledException) when(ct.IsCancellationRequested) { throw; }
        catch { return Ok(RhReciboPagoDb.Fail<List<RhReciboResumen>>("No se pudo consultar Comunicaciones para los recibos. Revise la instalacion R53.")); }
    }
}
