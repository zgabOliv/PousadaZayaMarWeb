using System.Text;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using PousadaMareMansa.Web.Controllers;
using PousadaMareMansa.Web.Data;
using PousadaMareMansa.Web.Services;

var builder = WebApplication.CreateBuilder(args);
var cfg = builder.Configuration;

builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    o.KnownNetworks.Clear(); o.KnownProxies.Clear();   // App Service/containers: confia no proxy da plataforma
});
builder.Services.AddControllersWithViews(o => o.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()));
builder.Services.AddSingleton<Db>();
builder.Services.AddSingleton<JwtService>();
builder.Services.AddScoped<PousadaRepository>();
builder.Services.AddScoped<AdminRepository>();
builder.Services.AddScoped<ContaRepository>();
builder.Services.AddScoped<IEmailSender, EmailService>();
builder.Services.AddScoped<ContaService>();
builder.Services.AddHealthChecks().AddCheck<BancoHealthCheck>("banco", tags: new[] { "ready" });

// Dois esquemas: Cookie (site MVC) e JWT Bearer (API REST).
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o =>
    {
        o.LoginPath = "/Admin/Login";
        o.AccessDeniedPath = "/Admin/Negado";
        o.Cookie.HttpOnly = true;
        o.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        o.ExpireTimeSpan = TimeSpan.FromHours(2);
    })
    // Cookie próprio do hóspede: não vale para o painel (que usa o esquema padrão acima).
    .AddCookie(ContaController.Esquema, o =>
    {
        o.Cookie.Name = "zm.hospede";
        o.LoginPath = "/Conta/Entrar";
        o.AccessDeniedPath = "/Conta/Entrar";
        o.Cookie.HttpOnly = true;
        o.Cookie.SameSite = SameSiteMode.Lax;
        o.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        o.ExpireTimeSpan = TimeSpan.FromHours(8);
        o.SlidingExpiration = true;
    })
    .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, o =>
    {
        o.MapInboundClaims = false;
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true, ValidIssuer = cfg["Jwt:Issuer"],
            ValidateAudience = true, ValidAudience = cfg["Jwt:Audience"],
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(cfg["Jwt:Key"]!)),
            ValidateLifetime = true, ClockSkew = TimeSpan.FromMinutes(1),
            NameClaimType = "name", RoleClaimType = "role"
        };
    });

// Limite de tentativas no login da API (proteção contra força bruta).
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.AddFixedWindowLimiter("login", l => { l.PermitLimit = 5; l.Window = TimeSpan.FromMinutes(1); l.QueueLimit = 0; });
});

// Documentação interativa (Swagger) só das rotas /api.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(o =>
{
    o.SwaggerDoc("v1", new OpenApiInfo { Title = "API Zaya Mar Pousada", Version = "v1" });
    o.DocInclusionPredicate((_, api) => api.RelativePath?.StartsWith("api/") == true);
    o.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization", Type = SecuritySchemeType.Http, Scheme = "bearer",
        BearerFormat = "JWT", In = ParameterLocation.Header, Description = "Informe apenas o token JWT."
    });
    o.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        { new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } }, Array.Empty<string>() }
    });
});

var app = builder.Build();

// Uso: dotnet run -- --criar-admin <login> <senha> <Gerente|Recepcao>
if (args.Length == 4 && args[0] == "--criar-admin")
{
    using var escopo = app.Services.CreateScope();
    var (hash, salt) = PasswordHasher.Gerar(args[2]);
    await escopo.ServiceProvider.GetRequiredService<AdminRepository>().CriarAdminAsync(args[1], hash, salt, args[3]);
    Console.WriteLine("Administrador criado.");
    return;
}

app.UseForwardedHeaders();
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Erro");
    app.UseHsts(); // o redirecionamento para HTTPS é feito pelo App Service ("Somente HTTPS")
}
// Swagger: sempre em Development; em produção só se Swagger__Enabled=true (útil na apresentação).
if (app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("Swagger:Enabled"))
{
    app.UseSwagger();
    app.UseSwaggerUI(o => o.SwaggerEndpoint("/swagger/v1/swagger.json", "API v1"));
}
app.UseDefaultFiles();   // serve /app/ (app mobile) via index.html
app.UseStaticFiles();
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
// Guarda o nome do hóspede logado (cookie "Hospede") para o menu do layout.
app.Use(async (ctx, next) =>
{
    if (!ctx.Request.Path.StartsWithSegments("/api"))
    {
        var r = await ctx.AuthenticateAsync(ContaController.Esquema);
        if (r.Succeeded) ctx.Items["HospedeNome"] = r.Principal!.Identity!.Name;
    }
    await next();
});
app.UseAuthorization();
app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");
app.MapControllers();
// Monitoramento: /health (processo no ar) e /health/ready (processo + banco)
app.MapHealthChecks("/health", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = c => c.Tags.Contains("ready") });
app.Run();
