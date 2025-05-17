using Microsoft.Extensions.Hosting.WindowsServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Application.Data;
using Application.Services;
using Serilog;
using Microsoft.AspNetCore.StaticFiles;
var builder = WebApplication.CreateBuilder(args);


// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddScoped<AuthService>(); // Регистрация AuthService
builder.Services.AddScoped<DiskService>(); // Регистрация DiskService
builder.Services.AddScoped<FileService>(); // Регистрация FileService
builder.Services.AddScoped<DiskSpaceService>(); // Регистрация DiskSpaceService
builder.Services.AddScoped<FolderService>(); // Регистрация FolderService
builder.Services.AddScoped<IconService>(); // Регистрация FileIconService

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<DatabaseInitializer>();

builder.Services.AddSingleton<IIconGenerationQueue, IconGenerationQueue>();
builder.Services.AddHostedService<IconGenerationHostedService>();

builder.Services.AddSingleton<FileExtensionContentTypeProvider>();

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "Media Storage API",
        Version = "v1"
    });

    // Добавляем поддержку JWT-аутентификации
    options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "Введите JWT токен в формате: Bearer {токен}"
    });

    options.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            new string[] {}
        }
    });
});

//для докер-контейнера
/*builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(8080); // HTTP
    options.ListenAnyIP(443, listenOptions =>
    {
        listenOptions.UseHttps("/https/fullchain.pem", "/https/privkey.pem");
    });
});*/



// Добавляем поддержку Windows-службы
builder.Host.UseWindowsService();
// Настройка Serilog
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console() // Логи в консоль
    .WriteTo.File("logs/log-.txt", rollingInterval: RollingInterval.Day) // Логи в файлы
    .CreateLogger();

builder.Host.UseSerilog(); // Использовать Serilog вместо встроенного логгера

builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
builder.Services.AddMvc();         // Для поддержки MVC
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

// Добавление CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy
            .AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader()
            .WithExposedHeaders("Content-Range", "Accept-Ranges");
    });
});

// Настройка подключения к базе данных
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(connectionString));

builder.Services.AddHostedService<FileCleanupService>();

// Добавляем аутентификацию
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidAudience = builder.Configuration["Jwt:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]))
    };
});

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
    options.AddPolicy("UserOnly", policy => policy.RequireRole("User"));
});

var app = builder.Build();
// CORS сразу после Build()
app.UseCors("AllowAll");

// Swagger — по желанию
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// HTTPS
app.UseHttpsRedirection();

// Аутентификация и авторизация
app.UseAuthentication();
app.UseAuthorization();

app.UseStaticFiles();

// Razor и контроллеры
app.MapRazorPages();
app.MapControllers();

// Покажи ошибки (404 и т.д.)
app.UseStatusCodePages();
// Вызов инициализации базы данных
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var initializer = services.GetRequiredService<DatabaseInitializer>();
    await initializer.InitializeAsync();
    await initializer.LoadDefaultIconsAsync();
}
app.Run();
