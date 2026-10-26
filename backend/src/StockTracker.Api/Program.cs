using System.Text.Json.Serialization;
using Microsoft.OpenApi;
using Serilog;
using Serilog.Formatting.Compact;
using StockTracker.Api.Authentication;
using StockTracker.Api.ExceptionHandling;
using StockTracker.Api.OpenApi;
using StockTracker.Api.Options;
using StockTracker.Api.Services;
using StockTracker.Application;
using StockTracker.Application.Common.Interfaces;
using StockTracker.Infrastructure;
using StockTracker.Infrastructure.Identity;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSerilog((services, logger) => logger
    .ReadFrom.Configuration(builder.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .WriteTo.Console(new RenderedCompactJsonFormatter()));

builder.Services.AddOptions<CorsOptions>().BindConfiguration(CorsOptions.SectionName);

builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration);

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HttpContextCurrentUser>();
builder.Services.AddApiAuthentication();

builder.Services.AddApiProblemDetails();
builder.Services.AddControllers()
    // Enums travel as their names ("Serialized", "Piece"), matching the generated frontend types.
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    // Generated frontend types follow C# nullability: non-nullable members become required.
    options.SupportNonNullableReferenceTypes();
    options.NonNullableReferenceTypesAsRequired();
    options.SchemaFilter<RequireNonNullablePropertiesSchemaFilter>();

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "Access token from POST /api/auth/login.",
    });
    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer", document)] = [],
    });
});

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseSerilogRequestLogging();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health").AllowAnonymous();

await app.Services.SeedIdentityAsync();

await app.RunAsync();
