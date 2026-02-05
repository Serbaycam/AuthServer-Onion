using AuthServer.Laboratory.API.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// --- SERVÝSLER ---
builder.Services.AddOpenApi();

// Veritabaný Baðlantýsý
builder.Services.AddDbContext<LabDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Controller'larý ekle
builder.Services.AddControllers();

var app = builder.Build();

// --- PIPELINE (Ýstek Ýþleme Hattý) ---
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

// EKSÝK OLAN KISIMLAR BURADA:
app.MapControllers(); // Controller rotalarýný (api/testmodules) tanýtýr
app.Run();            // Uygulamayý canlý tutar ve istek dinler (Olmazsa kapanýr)