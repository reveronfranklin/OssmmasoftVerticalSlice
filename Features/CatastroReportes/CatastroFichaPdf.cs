using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Features.CatastroContribuyentes;
using OssmmasoftVerticalSlice.Features.CatastroInmuebles;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Globalization;

namespace OssmmasoftVerticalSlice.Features.CatastroReportes;

public record CatastroFichaPdfQuery(long CodigoInmueble, long CodigoContribuyente, string? Usuario = null);

public static class CatastroFichaPdfGenerator
{
    public static byte[] Generate(CatastroInmuebleDetalleResponse inmueble, CatastroContribuyenteResponse contribuyente, string? usuario)
    {
        QuestPDF.Settings.License = LicenseType.Evaluation;
        var culture = CultureInfo.GetCultureInfo("es-VE");
        return Document.Create(document => document.Page(page =>
        {
            page.Size(PageSizes.Letter);
            page.Margin(32);
            page.DefaultTextStyle(style => style.FontSize(9));
            page.Header().Column(column =>
            {
                column.Item().AlignCenter().Text("FICHA CATASTRAL").Bold().FontSize(16).FontColor(Colors.Blue.Darken3);
                column.Item().AlignCenter().Text("Consulta preliminar - pendiente de homologacion legal").FontSize(8).FontColor(Colors.Grey.Darken1);
                column.Item().PaddingTop(8).LineHorizontal(1).LineColor(Colors.Blue.Darken2);
            });
            page.Content().PaddingVertical(14).Column(column =>
            {
                column.Spacing(12);
                column.Item().Element(c => Section(c, "Identificacion", new (string, string?)[]
                {
                    ("Codigo catastral", inmueble.CodigoCatastro), ("Codigo inmueble", inmueble.CodigoInmueble.ToString()),
                    ("Ficha", inmueble.CodigoFicha?.ToString()), ("Nombre", inmueble.NombreInmueble),
                    ("Numero", inmueble.NumeroInmueble), ("Contribuyente", contribuyente.CodigoContribuyente.ToString()),
                    ("Identificacion", contribuyente.NumeroIdentificacion), ("Nombre / razon social", $"{contribuyente.NombreRazonSocial} {contribuyente.ApellidoAcronimo}".Trim())
                }));
                column.Item().Element(c => Section(c, "Ubicacion", new (string, string?)[]
                {
                    ("Estado", inmueble.EstadoId?.ToString()), ("Municipio", inmueble.MunicipioId?.ToString()),
                    ("Parroquia", inmueble.ParroquiaId?.ToString()), ("Sector", inmueble.SectorId?.ToString()),
                    ("Vialidad", inmueble.Vialidad), ("Vivienda", inmueble.Vivienda),
                    ("Unidad", inmueble.NumeroUnidad), ("Complemento", inmueble.ComplementoDireccion)
                }));
                column.Item().Element(c => Section(c, "Avaluo", new (string, string?)[]
                {
                    ("Area", Amount(inmueble.Area, culture)), ("Valor terreno", Amount(inmueble.ValorTerreno, culture)),
                    ("Valor construccion", Amount(inmueble.ValorConstruccion, culture)), ("Valor inmueble", Amount(inmueble.ValorInmueble, culture))
                }));
                column.Item().Element(c => Section(c, "Observaciones", new (string, string?)[] { ("Observacion", inmueble.Observacion) }));
            });
            page.Footer().Row(row =>
            {
                row.RelativeItem().Text($"Generado: {DateTime.Now:dd/MM/yyyy HH:mm} · Usuario: {usuario ?? "no informado"}").FontSize(7).FontColor(Colors.Grey.Darken1);
                row.ConstantItem(80).AlignRight().Text(text => { text.Span("Pagina "); text.CurrentPageNumber(); text.Span(" de "); text.TotalPages(); });
            });
        })).GeneratePdf();
    }

    private static void Section(IContainer container, string title, IEnumerable<(string Label, string? Value)> values)
    {
        container.Border(1).BorderColor(Colors.Grey.Lighten1).Column(column =>
        {
            column.Item().Background(Colors.Blue.Lighten4).Padding(6).Text(title).Bold().FontColor(Colors.Blue.Darken3);
            column.Item().Padding(8).Table(table =>
            {
                table.ColumnsDefinition(columns => { columns.ConstantColumn(115); columns.RelativeColumn(); });
                foreach (var value in values)
                {
                    table.Cell().PaddingVertical(3).Text(value.Label).SemiBold().FontSize(8);
                    table.Cell().PaddingVertical(3).Text(string.IsNullOrWhiteSpace(value.Value) ? "Sin dato" : value.Value).FontSize(8);
                }
            });
        });
    }

    private static string Amount(decimal? value, CultureInfo culture) => value.HasValue ? value.Value.ToString("N2", culture) : "Sin dato";
}

[ApiController]
[Authorize]
[Route("api/CatastroReportes")]
public class CatastroFichaPdfController(ConnectionDB connections, IConfiguration config) : ControllerBase
{
    [HttpPost("fichaPdf")]
    public async Task<IActionResult> FichaPdf(CatastroFichaPdfQuery query)
    {
        if (query.CodigoInmueble <= 0 || query.CodigoContribuyente <= 0) return BadRequest(new { message = "Inmueble y contribuyente son obligatorios." });
        var inmueble = await new CatastroInmueblesGetByIdHandler(connections, config).HandleAsync(new(query.CodigoInmueble));
        if (!inmueble.IsValid || inmueble.Data is null) return BadRequest(new { message = inmueble.Message });
        var contribuyente = await new CatastroContribuyentesGetByIdHandler(connections, config).HandleAsync(new(query.CodigoContribuyente));
        if (!contribuyente.IsValid || contribuyente.Data is null) return BadRequest(new { message = contribuyente.Message });
        try
        {
            var pdf = CatastroFichaPdfGenerator.Generate(inmueble.Data, contribuyente.Data.Contribuyente, query.Usuario);
            return File(pdf, "application/pdf", $"ficha-catastro-{query.CodigoInmueble}.pdf");
        }
        catch (Exception ex) { return BadRequest(new { message = $"Error técnico al generar la ficha: {ex.Message}" }); }
    }
}
