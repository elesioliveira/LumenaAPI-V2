using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

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
builder.Services.AddControllers();

// CORS para Dev + Cookies
builder.Services.AddCors(options =>
{
    options.AddPolicy("DevPolicy", cors =>
    {
        cors
            .WithOrigins("http://localhost:5173")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials(); // cookies permitidos
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
app.UseCors("DevPolicy");
app.UseStaticFiles();
// 🔐 Auth
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
