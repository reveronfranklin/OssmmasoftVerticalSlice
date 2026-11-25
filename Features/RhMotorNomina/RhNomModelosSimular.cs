using Microsoft.AspNetCore.Mvc;
using OssmmasoftVerticalSlice.Helpers;

namespace OssmmasoftVerticalSlice.Features.RhMotorNomina;

public record RhNomModelosSimularCommand(List<RhNomFormula> Formulas, List<RhNomValorEntrada> Entradas);
public sealed class RhNomModelosSimularHandler
{
    public ResultDto<RhNomResultadoSimulacion> Handle(RhNomModelosSimularCommand value, CancellationToken ct = default)
    {
        var response = RhNomModelos.Simular(value.Formulas, value.Entradas, ct);
        return new(response)
        {
            IsValid = response.Errores.Count == 0,
            Message = response.Errores.Count == 0 ? "Expresiones simuladas. No se aplicaron movimientos de nomina." : response.Errores[0].Mensaje
        };
    }
}
public sealed partial class RhNominaController
{
    [HttpPost("modelosSimular")]
    [RequestSizeLimit(262144)]
    public Task<IActionResult> Simulate(RhNomModelosSimularCommand value) =>
        Run("rh.nomina.simular", ct => new RhNomModelosSimularHandler().Handle(value, ct));
}
