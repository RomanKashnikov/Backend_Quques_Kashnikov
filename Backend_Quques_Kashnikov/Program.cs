using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Threading.RateLimiting;
using Backend_Quques_Kashnikov.Data;
using Backend_Quques_Kashnikov.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);
// Локальному учебному серверу достаточно консоли и окна Output в Visual Studio.
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();
var dataDirectory = Path.GetFullPath(builder.Configuration["DataDirectory"] ?? Path.Combine(builder.Environment.ContentRootPath, "App_Data"));
Directory.CreateDirectory(dataDirectory);
var keyPath = Path.Combine(dataDirectory, "jwt.key");
if (!File.Exists(keyPath)) File.WriteAllBytes(keyPath, RandomNumberGenerator.GetBytes(64));
var tokens = new TokenService(File.ReadAllBytes(keyPath));
builder.Services.AddSingleton(tokens);
builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlite(
    new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder { DataSource = Path.Combine(dataDirectory, "eduqueue.db"), ForeignKeys = true }.ToString()));
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "Учебные электронные очереди — ЛР 1", Version = "v1" });
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "JWT",
        Description = "Получите accessToken через POST /api/auth/login и вставьте его сюда."
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        [new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } }] = Array.Empty<string>()
    });
});
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true, ValidIssuer = TokenService.Issuer,
        ValidateAudience = true, ValidAudience = TokenService.Audience,
        ValidateLifetime = true, ValidateIssuerSigningKey = true,
        IssuerSigningKey = tokens.SigningKey, ClockSkew = TimeSpan.Zero
    };
    options.Events = new JwtBearerEvents
    {
        OnTokenValidated = async context =>
        {
            var db = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
            if (!int.TryParse(context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier), out var id))
            { context.Fail("Некорректный пользователь."); return; }
            var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == id);
            if (user == null || user.SecurityStamp != context.Principal?.FindFirstValue("stamp"))
                context.Fail("Учётная запись изменена. Войдите заново.");
        }
    };
});
builder.Services.AddAuthorization();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("login", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "local",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 20, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});
var app = builder.Build();
using (var scope = app.Services.CreateScope())
    await SeedData.Initialize(scope.ServiceProvider.GetRequiredService<AppDbContext>());
// Для лабораторной запускается один процесс. Проверка и сохранение изменений сериализованы.
var writeGate = new SemaphoreSlim(1, 1);
app.Use(async (context, next) =>
{
    var write = HttpMethods.IsPost(context.Request.Method) || HttpMethods.IsPut(context.Request.Method)
        || HttpMethods.IsPatch(context.Request.Method) || HttpMethods.IsDelete(context.Request.Method);
    if (write) await writeGate.WaitAsync(context.RequestAborted);
    try { await next(context); }
    catch (Exception ex) when (ex is ValidationException or InvalidOperationException or DbUpdateException)
    {
        var status = ex is ValidationException ? 400 : 409;
        var message = ex is DbUpdateException ? "Операция нарушает уникальность данных или связи между записями." : ex.Message;
        await Results.Problem(statusCode: status, title: message).ExecuteAsync(context);
    }
    finally { if (write) writeGate.Release(); }
});
app.UseSwagger();
app.UseSwaggerUI();
app.UseHttpsRedirection();
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();
