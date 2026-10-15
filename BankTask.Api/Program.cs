
using System.Text;
using BankTask.Api.Filters;
using BankTask.Api.Hubs;
using BankTask.Api.Middleware;
using BankTask.Api.Services;
using BankTask.Api.WebSockets;
using BankTask.Application.Interfaces.Repositories;
using BankTask.Application.Interfaces.Security;
using BankTask.Application.Interfaces.Services;
using BankTask.Application.Services;
using BankTask.Application.Validators;
using BankTask.Authentication;
using BankTask.DBManager;
using BankTask.Infrastructure.Jobs;
using BankTask.Infrastructure.Repositories;
using BankTask.Infrastructure.Services;
using FluentValidation;
using Hangfire;
using Hangfire.SqlServer;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using Serilog.Sinks.Network;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, configuration) =>
{
    configuration
        .MinimumLevel.Information()
        .WriteTo.Seq("http://localhost:5341")
        .WriteTo.TCPSink("tcp://localhost:5044");
});

// Connection Strings

var sqlServerConnectionString =
    builder.Configuration.GetConnectionString("SqlServer")
    ?? throw new InvalidOperationException(
        "SqlServer connection string not found.");

var postgreSqlConnectionString =
    builder.Configuration.GetConnectionString("PostgreSQL")
    ?? throw new InvalidOperationException(
        "PostgreSQL connection string not found.");

// Hangfire

builder.Services.AddHangfire(configuration =>
{
    configuration.UseSqlServerStorage(
        sqlServerConnectionString);
});

builder.Services.AddHangfireServer();

var connectionFactory = new ConnectionFactory(
    sqlServerConnectionString,
    postgreSqlConnectionString);

builder.Services.AddSingleton<IConnectionFactory>(
    connectionFactory);

// Repositories

builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IAccountRepository, AccountRepository>();
builder.Services.AddScoped<ITransactionRepository, TransactionRepository>();
builder.Services.AddScoped<IAuditLogRepository, AuditLogRepository>();
builder.Services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<IBackgroundJobService, HangfireBackgroundJobService>();

// Security

builder.Services.AddScoped<IPasswordHasher, PasswordHasher>();

// JWT Options

builder.Services
    .AddOptions<JwtOptions>()
    .BindConfiguration("Jwt")
    .Validate(
        options => !string.IsNullOrWhiteSpace(options.SecretKey),
        "JWT secret key not found.")
    .ValidateOnStart();

builder.Services.AddScoped<IJwtService, JwtService>();

// JWT Authentication

var jwtSecretKey =
    builder.Configuration["Jwt:SecretKey"]
    ?? throw new InvalidOperationException(
        "JWT secret key not found.");

var jwtIssuer =
    builder.Configuration["Jwt:Issuer"]
    ?? throw new InvalidOperationException(
        "JWT issuer not found.");

var jwtAudience =
    builder.Configuration["Jwt:Audience"]
    ?? throw new InvalidOperationException(
        "JWT audience not found.");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters =
            new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,

                IssuerSigningKey =
                    new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(jwtSecretKey)),

                ValidateIssuer = true,
                ValidIssuer = jwtIssuer,

                ValidateAudience = true,
                ValidAudience = jwtAudience,

                ValidateLifetime = true,

                ClockSkew = TimeSpan.Zero
            };

        // Allow SignalR to receive the JWT through access_token
        // for the transactions Hub endpoint only.
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken =
                    context.Request.Query["access_token"];

                var path = context.HttpContext.Request.Path;

                if (!string.IsNullOrEmpty(accessToken) &&
                    path.StartsWithSegments(
                        "/hubs/transactions"))
                {
                    context.Token = accessToken;
                }

                return Task.CompletedTask;
            }
        };
    });

// Application Services

builder.Services.AddScoped<IUserService, UserService>();

builder.Services.AddScoped<
    IAuthenticationService,
    AuthenticationService>();

builder.Services.AddHttpClient<
    IExchangeRateService,
    ExchangeRateService>();

builder.Services.AddScoped<IAccountService, AccountService>();

builder.Services.AddScoped<
    ITransactionService,
    TransactionService>();

builder.Services.AddScoped<
    IAuditLogService,
    AuditLogService>();

builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration =
        builder.Configuration["Redis:ConnectionString"];

    options.InstanceName = "BankTask:";
});

builder.Services.AddScoped<ICacheService, RedisCacheService>();

builder.Services.AddSingleton<
    IStockPriceService,
    StockPriceService>();

builder.Services.AddScoped<
    ITransactionNotificationService,
    TransactionNotificationService>();

// Validation

builder.Services.AddValidatorsFromAssemblyContaining<
    SignupRequestValidator>();

builder.Services.AddScoped<ValidationFilter>();

builder.Services.AddHttpContextAccessor();

builder.Services.AddScoped<UserCountJob>();

// SignalR

builder.Services.AddSignalR();

// Controllers

builder.Services.AddControllers(options =>
{
    options.Filters.Add<ValidationFilter>();
});

builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.SuppressModelStateInvalidFilter = true;
});

builder.Services.AddEndpointsApiExplorer();

// Swagger

builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition(
        "Bearer",
        new Microsoft.OpenApi.Models.OpenApiSecurityScheme
        {
            Name = "Authorization",
            Type =
                Microsoft.OpenApi.Models.SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In =
                Microsoft.OpenApi.Models.ParameterLocation.Header,
            Description = "Enter your JWT token."
        });

    options.AddSecurityRequirement(
        new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
        {
            {
                new Microsoft.OpenApi.Models.OpenApiSecurityScheme
                {
                    Reference =
                        new Microsoft.OpenApi.Models.OpenApiReference
                        {
                            Type =
                                Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                            Id = "Bearer"
                        }
                },
                Array.Empty<string>()
            }
        });
});

builder.Services.AddTransient<StockWebSocketHandler>();

// Build Application

var app = builder.Build();

// WebSockets

app.UseWebSockets();

// Swagger

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHangfireDashboard();

var recurringJobManager =
    app.Services.GetRequiredService<IRecurringJobManager>();

recurringJobManager.AddOrUpdate<UserCountJob>(
    "user-count-job",
    job => job.ExecuteAsync(),
    "*/30 * * * *");

// HTTPS

app.UseHttpsRedirection();

// Request / Response Logging

app.UseMiddleware<RequestResponseLoggingMiddleware>();

// Exception Handling Middleware

app.UseMiddleware<ExceptionHandlingMiddleware>();

// Authentication & Authorization

app.UseAuthentication();
app.UseAuthorization();

// Raw WebSocket endpoint

app.Map("/ws/stocks", async context =>
{
    var handler = context.RequestServices
        .GetRequiredService<StockWebSocketHandler>();

    await handler.HandleAsync(context);
});

// SignalR endpoint

// Serve the static test page from wwwroot (signalr test helper)
app.UseStaticFiles();

app.MapHub<TransactionsHub>("/hubs/transactions");

// Controllers

app.MapControllers();

app.Run();
