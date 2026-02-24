using OssmmasoftVerticalSlice.ContextDB;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Registrar nuestra clase de conexión
builder.Services.AddScoped<ConnectionDB>();
builder.Services.AddCors(options =>
{
    options.AddPolicy("MyPolicy", 
        policy =>
        {
           
                 policy.WithOrigins(
                        "https://ossmmasoft.com.ve:3001",
                        "http://localhost:3001",
                        "http://localhost:3000", 
                        "https://localhost:3000",
                        "https://localhost:3001",
                        "http://dev.ossmmasoft.com.ve:3001",
                        "https://dev.ossmmasoft.com.ve:3443",
                        "https://dev.ossmmasoft.com.ve:3001")
                  .AllowAnyMethod()
                  .AllowAnyHeader();



        });
});

builder.Services.AddControllers();
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

app.UseAuthorization();

app.MapControllers();

app.Run();
