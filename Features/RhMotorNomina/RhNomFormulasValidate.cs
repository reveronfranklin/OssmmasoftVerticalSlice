using Microsoft.AspNetCore.Mvc;
using OssmmasoftVerticalSlice.Helpers;

namespace OssmmasoftVerticalSlice.Features.RhMotorNomina;

public record RhNomFormulasValidateCommand(string VariableSalida, string Expresion, List<RhNomValorEntrada>? Variables = null);
public sealed class RhNomFormulasValidateHandler
{
    public ResultDto<RhNomResultadoValidacion> Handle(RhNomFormulasValidateCommand value, CancellationToken ct = default)
    {
        try
        {
            ct.ThrowIfCancellationRequested();
            var name = RhNomNumeros.Codigo(value.VariableSalida);
            var formula = RhNomExpresion.Compilar(value.Expresion);
            if (formula.Dependencias.Contains(name, StringComparer.Ordinal))
                throw new RhNomError("AUTORREFERENCIA", $"La formula [{name}] no puede referenciarse a si misma.", variable: name);
            var evaluation = value.Variables is null ? null : RhNomModelos.Evaluar(name, formula, RhNomModelos.Entradas(value.Variables), ct);
            var response = new RhNomResultadoValidacion(evaluation is null ? "SINTAXIS_VALIDA" : "VALIDADA_CON_DATOS_DE_PRUEBA",
                formula.Normalizada, formula.Dependencias.ToList(), evaluation, []);
            return new(response) { IsValid = true, Message = evaluation is null ? "Sintaxis valida; faltan datos de prueba para evaluar." : "Formula validada con datos de prueba." };
        }
        catch (RhNomError ex)
        {
            return new(new("INVALIDO", null, [], null, [ex.Diagnostico])) { IsValid = false, Message = ex.Message };
        }
    }
}
public sealed partial class RhNominaController
{
    [HttpPost("formulasValidate")]
    [RequestSizeLimit(131072)]
    public Task<IActionResult> Validate(RhNomFormulasValidateCommand value) =>
        Run("rh.nomina.formulas.validar", ct => new RhNomFormulasValidateHandler().Handle(value, ct));
}
