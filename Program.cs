using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Features.Contabilidad;
using OssmmasoftVerticalSlice.Features.Email;
using OssmmasoftVerticalSlice.Features.Support;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Registrar nuestra clase de conexión
builder.Services.AddScoped<ConnectionDB>();
builder.Services.AddScoped<SupportDashboardSummaryHandler>();
builder.Services.AddScoped<GetSupportPermissionsHandler>();
builder.Services.AddScoped<GetSupportNotificationsByUserHandler>();
builder.Services.AddScoped<MarkSupportNotificationReadHandler>();
builder.Services.AddScoped<NotifyExpiredSupportSlaHandler>();
builder.Services.AddScoped<GetSupportCatalogsHandler>();
builder.Services.AddScoped<SupportTicketHandlerSupport>();
builder.Services.AddScoped<CreateSupportTicketHandler>();
builder.Services.AddScoped<GetSupportTicketsHandler>();
builder.Services.AddScoped<GetSupportTicketByIdHandler>();
builder.Services.AddScoped<AssignSupportTicketHandler>();
builder.Services.AddScoped<ChangeSupportTicketStatusHandler>();
builder.Services.AddScoped<CloseSupportTicketHandler>();
builder.Services.AddScoped<SupportChildRowsReader>();
builder.Services.AddScoped<CreateSupportCommentHandler>();
builder.Services.AddScoped<GetSupportCommentsByTicketHandler>();
builder.Services.AddScoped<GetSupportHistoryByTicketHandler>();
builder.Services.AddScoped<SupportAttachmentHandlerSupport>();
builder.Services.AddScoped<CreateSupportAttachmentHandler>();
builder.Services.AddScoped<UploadSupportAttachmentHandler>();
builder.Services.AddScoped<GetSupportAttachmentsByTicketHandler>();
builder.Services.AddScoped<GetCntCatalogosHandler>();
builder.Services.AddScoped<GetCntPeriodosHandler>();
builder.Services.AddScoped<SearchCntMayoresHandler>();
builder.Services.AddScoped<SearchCntAuxiliaresHandler>();
builder.Services.AddScoped<GetCntTitulosHandler>();
builder.Services.AddScoped<SaveCntTituloHandler>();
builder.Services.AddScoped<DeleteCntTituloHandler>();
builder.Services.AddScoped<GetCntDescriptivasHandler>();
builder.Services.AddScoped<SaveCntDescriptivaHandler>();
builder.Services.AddScoped<DeleteCntDescriptivaHandler>();
builder.Services.AddScoped<GetCntDescriptivaUsedByHandler>();
builder.Services.AddScoped<GetCntRubrosHandler>();
builder.Services.AddScoped<SaveCntRubroHandler>();
builder.Services.AddScoped<DeleteCntRubroHandler>();
builder.Services.AddScoped<GetCntBalancesHandler>();
builder.Services.AddScoped<SaveCntBalanceHandler>();
builder.Services.AddScoped<DeleteCntBalanceHandler>();
builder.Services.AddScoped<GetCntMayoresHandler>();
builder.Services.AddScoped<SaveCntMayorHandler>();
builder.Services.AddScoped<DeleteCntMayorHandler>();
builder.Services.AddScoped<GetCntMayorUsedByHandler>();
builder.Services.AddScoped<GetCntAuxiliaresHandler>();
builder.Services.AddScoped<SaveCntAuxiliarHandler>();
builder.Services.AddScoped<DeleteCntAuxiliarHandler>();
builder.Services.AddScoped<GetCntAuxiliarUsedByHandler>();
builder.Services.AddScoped<GetCntAuxiliaresPucHandler>();
builder.Services.AddScoped<SaveCntAuxiliarPucHandler>();
builder.Services.AddScoped<DeleteCntAuxiliarPucHandler>();
builder.Services.AddScoped<GetCntPeriodosAdminHandler>();
builder.Services.AddScoped<SaveCntPeriodoHandler>();
builder.Services.AddScoped<DeleteCntPeriodoHandler>();
builder.Services.AddScoped<GenerateCntPeriodoYearHandler>();
builder.Services.AddScoped<GetCntRelacionDocumentosHandler>();
builder.Services.AddScoped<SaveCntRelacionDocumentoHandler>();
builder.Services.AddScoped<DeleteCntRelacionDocumentoHandler>();
builder.Services.AddScoped<GetCntSaldosHandler>();
builder.Services.AddScoped<SaveCntSaldoHandler>();
builder.Services.AddScoped<DeleteCntSaldoHandler>();
builder.Services.AddScoped<GetCntBancosHandler>();
builder.Services.AddScoped<GetCntCuentasBancoHandler>();
builder.Services.AddScoped<GetCntConciliacionesHandler>();
builder.Services.AddScoped<GetCntConciliacionByIdHandler>();
builder.Services.AddScoped<CreateCntConciliacionHandler>();
builder.Services.AddScoped<PrecloseCntConciliacionHandler>();
builder.Services.AddScoped<CloseCntConciliacionHandler>();
builder.Services.AddScoped<ReverseCntConciliacionHandler>();
builder.Services.AddScoped<GetCntConciliacionBancoMovimientosHandler>();
builder.Services.AddScoped<GetCntConciliacionLibroMovimientosHandler>();
builder.Services.AddScoped<GetCntConciliacionTemporalesHandler>();
builder.Services.AddScoped<MatchCntConciliacionHandler>();
builder.Services.AddScoped<MatchMultiCntConciliacionHandler>();
builder.Services.AddScoped<GetCntConciliacionSuggestionsHandler>();
builder.Services.AddScoped<UnmatchCntConciliacionHandler>();
builder.Services.AddScoped<GetCntBancoArchivosHandler>();
builder.Services.AddScoped<CreateCntBancoArchivoControlHandler>();
builder.Services.AddScoped<CreateCntBancoArchivoDetalleHandler>();
builder.Services.AddScoped<GetCntBancoArchivoDetallesHandler>();
builder.Services.AddScoped<GetCntBancoArchivoPreviewHandler>();
builder.Services.AddScoped<GetCntBancoArchivoTraceHandler>();
builder.Services.AddScoped<ExtractCntBancoArchivoHandler>();
builder.Services.AddScoped<CreateCntBancoArchivoBatchHandler>();
builder.Services.AddScoped<ConfirmCntBancoArchivoHandler>();
builder.Services.AddScoped<GetCntBancoFormatosHandler>();
builder.Services.AddScoped<SaveCntBancoFormatoHandler>();
builder.Services.AddScoped<DeleteCntBancoFormatoHandler>();
builder.Services.AddScoped<GetCntEstadosCuentaHandler>();
builder.Services.AddScoped<GetCntEstadoCuentaDetallesHandler>();
builder.Services.AddScoped<GetCntLibrosBancoHandler>();
builder.Services.AddScoped<GetCntLibroBancoDetallesHandler>();
builder.Services.AddScoped<GenerateCntLibroBancoHandler>();
builder.Services.AddScoped<GetCntCierrePeriodosHandler>();
builder.Services.AddScoped<GetCntCierreModificacionesHandler>();
builder.Services.AddScoped<PrecierreCntContableHandler>();
builder.Services.AddScoped<CierreCntContableHandler>();
builder.Services.AddScoped<ReversoCntContableHandler>();
builder.Services.AddScoped<CloneCntDescriptivasHandler>();
builder.Services.AddScoped<CloneCntPlanCuentasHandler>();
builder.Services.AddScoped<CheckCntPermissionHandler>();
builder.Services.AddScoped<GetCntComprobantesHandler>();
builder.Services.AddScoped<GetCntComprobanteByIdHandler>();
builder.Services.AddScoped<GetCntComprobantePrintHandler>();
builder.Services.AddScoped<GenerateCntComprobanteNumberHandler>();
builder.Services.AddScoped<GetCntDetallesByComprobanteHandler>();
builder.Services.AddScoped<CreateCntComprobanteHandler>();
builder.Services.AddScoped<UpdateCntComprobanteHandler>();
builder.Services.AddScoped<DeleteCntComprobanteHandler>();
builder.Services.AddScoped<ReorderCntComprobantesHandler>();
builder.Services.AddScoped<AddCntDetalleHandler>();
builder.Services.AddScoped<UpdateCntDetalleHandler>();
builder.Services.AddScoped<DeleteCntDetalleHandler>();
builder.Services.AddScoped<PreviewCntAutomaticoHandler>();
builder.Services.AddScoped<ConfirmCntAutomaticoHandler>();
builder.Services.AddScoped<GetCntMayorAnaliticoHandler>();
builder.Services.AddScoped<GetCntMovimientoAuxiliarHandler>();
builder.Services.AddHostedService<EmailQueueWorker>();
builder.Services.AddCors(options =>
{
    options.AddPolicy("MyPolicy", 
        policy =>
        {
                 policy.AllowAnyOrigin()
                  .AllowAnyMethod()
                  .AllowAnyHeader();



        });
});

builder.Services.AddControllers();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration.GetSection("AppSettings:Token").Value ?? string.Empty)),
            ValidateIssuer = false,
            ValidateAudience = false
        };

        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                if (string.IsNullOrWhiteSpace(context.Token))
                {
                    context.Token = context.Request.Cookies["X-Auth-Token"];
                }

                return Task.CompletedTask;
            }
        };
    });
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseCors("MyPolicy");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
