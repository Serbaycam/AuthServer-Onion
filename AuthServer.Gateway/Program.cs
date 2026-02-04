var builder = WebApplication.CreateBuilder(args);

// YARP Servisini ekle
// Ayarlarý "ReverseProxy" bölümünden (appsettings.json) okuyacak
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

var app = builder.Build();

// YARP Middleware'ini devreye al
app.MapReverseProxy();

app.Run();