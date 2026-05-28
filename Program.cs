using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.UseUrls(builder.Configuration["ASPNETCORE_URLS"] ?? "http://localhost:5037");

//  JWT Config
var jwtKey = builder.Configuration["Jwt:Key"];
var jwtIssuer = builder.Configuration["Jwt:Issuer"];
var jwtAudience = builder.Configuration["Jwt:Audience"];

var keyBytes = Encoding.UTF8.GetBytes(jwtKey!);

//  DI Services
builder.Services.AddSingleton<JwtService>();
builder.Services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.Configure<GtinApiOptions>(
    builder.Configuration.GetSection("GtinApi"));
builder.Services.AddSingleton<CacheHelper>();
builder.Services.AddMemoryCache();

builder.Services.AddHttpClient<IGtinTokenService, GtinTokenService>();
builder.Services.AddHttpClient<IGtinProdutoService, GtinProdutoService>();

builder.Services.AddSingleton<ICertificateService, CertificateService>();
builder.Services.AddSingleton<IXmlBuilderService, XmlBuilderService>();
builder.Services.AddSingleton<IXmlSignerService, XmlSignerService>();
builder.Services.AddSingleton<ISefazClientService, SefazClientService>();
builder.Services.AddSingleton<IDanfeService, DanfeService>();
builder.Services.AddSingleton<IDanfceService, DanfceService>();
builder.Services.AddSingleton<IQrCodeService, QrCodeService>();

builder.Services.AddScoped<ValidarLicencaFilter>();

builder.Services.AddControllers(options =>
{
    options.Filters.Add<ValidarLicencaFilter>();
});

var corsOrigins = builder.Configuration["CORS_ORIGINS"]?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
    ?? ["http://localhost:5173"];

builder.Services.AddCors(options =>
{
    options.AddPolicy("AppPolicy", cors =>
    {
        cors
            .WithOrigins(corsOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});


//  Authentication / JWT
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false; // DEV
    options.SaveToken = true;

    // Leia token automaticamente do cookie
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            if (context.Request.Cookies.ContainsKey("access_token"))
            {
                context.Token = context.Request.Cookies["access_token"];
            }
            return Task.CompletedTask;
        }
    };

    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidIssuer = jwtIssuer,

        ValidateAudience = true,
        ValidAudience = jwtAudience,

        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(keyBytes),

        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero
    };
});

builder.Services.AddAuthorization();

//  Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

// Swagger ativado apenas em DEV (mas você decide)
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// ⚠ Ambiente de produção exige HTTPS
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

//  CORS sempre antes de Auth
app.UseCors("AppPolicy");
app.UseStaticFiles();
// 🔐 Auth
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
