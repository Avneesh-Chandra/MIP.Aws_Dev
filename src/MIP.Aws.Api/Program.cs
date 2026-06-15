using System.Text;

using System.Text.Json.Serialization;

using MIP.Aws.API.Account;

using MIP.Aws.API.Security;

using MIP.Aws.Application;

using MIP.Aws.Application.Configuration;

using MIP.Aws.Application.Scheduling;

using MIP.Aws.Blazor.Components;

using MIP.Aws.Blazor.Services;

using MIP.Aws.Domain.Security;

using MIP.Aws.Infrastructure;

using MIP.Aws.Persistence;

using MIP.Aws.Shared.Responses;

using Hangfire;

using Microsoft.AspNetCore.Authentication.JwtBearer;

using Microsoft.AspNetCore.Identity;

using Microsoft.AspNetCore.Mvc;

using Microsoft.IdentityModel.Tokens;

using Microsoft.OpenApi.Models;

using MudBlazor.Services;



var builder = WebApplication.CreateBuilder(args);



var jwtKey = builder.Configuration[$"{JwtOptions.SectionName}:SigningKey"];

if (string.IsNullOrWhiteSpace(jwtKey) || jwtKey.Length < 32)

{

    throw new InvalidOperationException("Configuration 'Jwt:SigningKey' is required and must be at least 32 characters.");

}



builder.Services.AddJwtServices(builder.Configuration);

builder.Services.AddApplication();

builder.Services.AddPersistence(builder.Configuration);

builder.Services.AddMipAwsInfrastructure(builder.Configuration, builder.Environment, enableHangfireProcessing: true);



builder.Services.AddRazorComponents()

    .AddInteractiveServerComponents()

    .AddCircuitOptions(options =>

    {

        options.DetailedErrors = builder.Environment.IsDevelopment();

    });

builder.Services.AddCascadingAuthenticationState();

builder.Services.AddScoped<IUserTimeZone, UserTimeZoneService>();

builder.Services.AddMudServices(config =>

{

    config.SnackbarConfiguration.PositionClass = MudBlazor.Defaults.Classes.Position.BottomRight;

    config.SnackbarConfiguration.PreventDuplicates = true;

    config.SnackbarConfiguration.NewestOnTop = true;

    config.SnackbarConfiguration.ShowCloseIcon = true;

    config.SnackbarConfiguration.VisibleStateDuration = 4000;

});



builder.Services.AddControllers().AddJsonOptions(o =>

{

    o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());

});



builder.Services.Configure<ApiBehaviorOptions>(options =>

{

    options.InvalidModelStateResponseFactory = context =>

    {

        var errors = context.ModelState.Values

            .SelectMany(v => v.Errors)

            .Select(e => e.ErrorMessage)

            .Where(s => !string.IsNullOrWhiteSpace(s))

            .ToList();

        return new BadRequestObjectResult(ApiResponse.Fail("Validation failed", errors));

    };

});



builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(options =>

{

    options.SwaggerDoc("v1", new OpenApiInfo { Title = "MIP AWS API", Version = "v1" });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme

    {

        Description = "JWT Authorization header. Example: Bearer {token}",

        Name = "Authorization",

        In = ParameterLocation.Header,

        Type = SecuritySchemeType.Http,

        Scheme = "bearer",

        BearerFormat = "JWT"

    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement

    {

        {

            new OpenApiSecurityScheme

            {

                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }

            },

            Array.Empty<string>()

        }

    });

});



var jwt = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()

    ?? throw new InvalidOperationException($"Missing configuration section '{JwtOptions.SectionName}'.");



builder.Services.AddAuthentication(options =>

{

    options.DefaultScheme = AuthenticationSchemes.SmartAuth;

    options.DefaultAuthenticateScheme = AuthenticationSchemes.SmartAuth;

    options.DefaultChallengeScheme = AuthenticationSchemes.SmartAuth;

    options.DefaultForbidScheme = AuthenticationSchemes.SmartAuth;

    options.DefaultSignOutScheme = IdentityConstants.ApplicationScheme;

})

.AddPolicyScheme(AuthenticationSchemes.SmartAuth, "SmartAuth (cookie or bearer)", options =>

{

    options.ForwardDefaultSelector = context =>

    {

        var authorization = context.Request.Headers.Authorization.ToString();

        if (!string.IsNullOrEmpty(authorization)

            && authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))

        {

            return JwtBearerDefaults.AuthenticationScheme;

        }



        return IdentityConstants.ApplicationScheme;

    };

})

.AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>

{

    options.MapInboundClaims = true;

    options.TokenValidationParameters = new TokenValidationParameters

    {

        ValidateIssuer = true,

        ValidateAudience = true,

        ValidateIssuerSigningKey = true,

        ValidateLifetime = true,

        ValidIssuer = jwt.Issuer,

        ValidAudience = jwt.Audience,

        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),

        ClockSkew = TimeSpan.FromMinutes(1),

        RoleClaimType = System.Security.Claims.ClaimTypes.Role

    };

});



var useHttpsCookies = builder.Configuration.GetValue<bool?>("Auth:UseHttpsCookies")

    ?? !builder.Environment.IsDevelopment();

var cookieSecurePolicy = useHttpsCookies

    ? Microsoft.AspNetCore.Http.CookieSecurePolicy.Always

    : Microsoft.AspNetCore.Http.CookieSecurePolicy.SameAsRequest;



builder.Services.ConfigureApplicationCookie(o =>

{

    o.Cookie.SecurePolicy = cookieSecurePolicy;

    o.Cookie.SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Lax;

    o.Cookie.HttpOnly = true;

    o.Cookie.Name = "MIP.Aws.Auth";

    o.ExpireTimeSpan = TimeSpan.FromHours(8);

    o.SlidingExpiration = true;

    o.LoginPath = "/Account/Login";

    o.AccessDeniedPath = "/Account/AccessDenied";

    o.Events.OnRedirectToLogin = ctx =>

    {

        if (ctx.Request.Path.StartsWithSegments("/api"))

        {

            ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;

            return Task.CompletedTask;

        }



        if (ctx.Request.Path.Value is { } path && BlazorPublicAccess.IsAnonymousPath(path))

        {

            ctx.Response.StatusCode = StatusCodes.Status404NotFound;

            return Task.CompletedTask;

        }



        ctx.Response.Redirect(ctx.RedirectUri);

        return Task.CompletedTask;

    };

    o.Events.OnRedirectToAccessDenied = ctx =>

    {

        if (ctx.Request.Path.StartsWithSegments("/api"))

        {

            ctx.Response.StatusCode = StatusCodes.Status403Forbidden;

            return Task.CompletedTask;

        }



        ctx.Response.Redirect(ctx.RedirectUri);

        return Task.CompletedTask;

    };

});



builder.Services.AddAntiforgery(o =>

{

    o.Cookie.SecurePolicy = cookieSecurePolicy;

});



builder.Services.AddAuthorization(options => AuthorizationPolicyRegistry.Register(options));

builder.Services.AddHttpContextAccessor();

builder.Services.AddCors(options =>

{

    options.AddDefaultPolicy(policy => policy

        .AllowAnyHeader()

        .AllowAnyMethod()

        .SetIsOriginAllowed(_ => true)

        .AllowCredentials());

});



var app = builder.Build();



await DatabaseBootstrap.ApplyDevelopmentDatabaseAsync(app.Environment, app.Services, app.Configuration).ConfigureAwait(false);
await DatabaseBootstrap.ApplyProductionMigrationIfRequestedAsync(app.Environment, app.Services, app.Configuration).ConfigureAwait(false);



if (app.Environment.IsDevelopment())

{

    app.UseSwagger();

    app.UseSwaggerUI();

}



app.UseCors();

app.UseAuthentication();

app.UseAuthorization();

app.UseAntiforgery();



app.MapStaticAssets().AllowAnonymous();



app.MapRazorComponents<App>()

    .AddInteractiveServerRenderMode()

    .Add(endpoint =>

    {

        if (endpoint is RouteEndpointBuilder routeEndpoint

            && routeEndpoint.RoutePattern.RawText is { } rawText

            && BlazorPublicAccess.IsAnonymousPath(rawText))

        {

            routeEndpoint.Metadata.Add(new Microsoft.AspNetCore.Authorization.AllowAnonymousAttribute());

        }

    });



app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "MIP.Aws.Api" }))

    .AllowAnonymous();

app.MapGet("/health/live", () => Results.Ok(new { status = "alive", service = "MIP.Aws.Api" }))

    .AllowAnonymous();



app.UseHangfireDashboard("/hangfire", new DashboardOptions

{

    Authorization = [new HangfireDashboardAuthorizationFilter()]

});



// Hangfire recurring jobs require a reachable SQL catalog (shared with EF on RDS Express).
await DatabaseBootstrap.EnsureAuxiliarySqlCatalogAsync(app.Configuration).ConfigureAwait(false);

using (var scope = app.Services.CreateScope())
{
    scope.ServiceProvider.GetRequiredService<IScheduledJobRegistry>().RegisterRecurringJobs();
}



app.MapControllers();

app.MapAccountEndpoints();



app.Run();

