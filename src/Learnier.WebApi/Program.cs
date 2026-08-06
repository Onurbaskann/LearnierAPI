using System.Globalization;
using System.Text.Json.Serialization;
using Learnier.Application;
using Learnier.Application.Common.Abstractions;
using Learnier.Infrastructure;
using Learnier.Infrastructure.Persistence.Seeding;
using Learnier.WebApi.Authorization;
using Learnier.WebApi.Common;
using Learnier.WebApi.Filters;
using Learnier.WebApi.Localization;
using Learnier.WebApi.Middleware;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Localization;
using Scalar.AspNetCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Serilog: yapilandirilmis loglama ve istek basina ozet kayit.
// Bicimlendirme InvariantCulture ile sabitlenir: istek basina kultur degistigi icin
// aksi halde ayni log satiri dile gore farkli tarih/sayi bicimiyle yazilirdi.
builder.Host.UseSerilog((context, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console(formatProvider: CultureInfo.InvariantCulture));

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();

// CurrentTenant hem somut tipiyle (middleware degeri set edebilsin diye)
// hem de arayuzuyle kaydedilir; ikisi de ayni ornege isaret eder.
builder.Services.AddScoped<CurrentTenant>();
builder.Services.AddScoped<ICurrentTenant>(sp => sp.GetRequiredService<CurrentTenant>());

builder.Services.AddScoped<ErrorMessageResolver>();

builder.Services.AddControllers(options =>
{
    // Validation her action'da otomatik calisir; tek tek eklemek gerekmez.
    options.Filters.Add<ValidationFilter>();
})
.AddJsonOptions(options =>
{
    // Enum'lar metin olarak tasinir: istemci "Provider" gonderir, sayi degil.
    // Bu olmadan enum alan iceren her istek baglama hatasiyla 400 doner.
    // Gerekce enum'lari veritabaninda metin saklamakla ayni: sayi kullanilirsa
    // enum uyelerinin sirasi degistiginde mevcut istemcilerin anlami sessizce kayar.
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");

builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    var supportedCultures = new[] { new CultureInfo("tr"), new CultureInfo("en") };

    options.DefaultRequestCulture = new RequestCulture("tr");
    options.SupportedCultures = supportedCultures;
    options.SupportedUICultures = supportedCultures;
});

// Sadece L1 (in-memory). Redis eklendiginde AddStackExchangeRedisCache yeterli olur;
// GetOrCreateAsync cagrilari degismez.
builder.Services.AddHybridCache();

// Policy adi dogrudan izin kodu olarak yorumlanir; her izin icin ayri kayit gerekmez.
builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
builder.Services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
builder.Services.AddAuthorization();

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();

var app = builder.Build();

// "seed" argumaniyla calistirildiginda uygulama sunucuyu ayaga kaldirmaz, yalnizca
// veriyi yazip cikar. Migration'da oldugu gibi tohumlama da acik bir adimdir:
// baslangicta kendiliginden calissaydi hangi verinin ne zaman yazildigi
// ongorulemez olurdu. Ornek hesaplar yalnizca gelistirme ortaminda olusur.
if (args.Contains("seed", StringComparer.OrdinalIgnoreCase))
{
    await DatabaseSeeder.RunAsync(app.Services, app.Environment.IsDevelopment());
    return;
}

app.UseExceptionHandler();
app.UseSerilogRequestLogging();
app.UseRequestLocalization();

if (app.Environment.IsDevelopment())
{
    // OpenAPI ve Scalar yalnizca gelistirmede: uretimde API yuzeyini ifsa etmemek icin.
    app.MapOpenApi();
    app.MapScalarApiReference();
}
else
{
    app.UseHttpsRedirection();
}

app.UseAuthentication();

// Sira onemli: tenant cozumlemesi kimlik dogrulamadan sonra (kullaniciyi bilmesi gerekir),
// yetkilendirmeden once (izinler uyelige bagli) calismali.
app.UseMiddleware<TenantResolutionMiddleware>();

app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health");

await app.RunAsync();

/// <summary>
/// Entegrasyon testlerinin <c>WebApplicationFactory&lt;Program&gt;</c> ile
/// uygulamayi ayaga kaldirabilmesi icin gerekli.
/// </summary>
public partial class Program;
