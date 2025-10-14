using System.Diagnostics;
using System.Globalization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using ResultsService.Contracts;
using ResultsService.Data;
using ResultsService.HealthChecks;
using ResultsService.Models;
using Shared.Security;

var builder = WebApplication.CreateBuilder(args);

var securityProfile = SecurityProfileDefaults.ResolveCurrentProfile();
var requiresHttps = securityProfile.RequiresHttps();
var requiresJwt = securityProfile.RequiresJwt();
var dashboardOrigins = builder.Configuration.GetValue<string>("Dashboard:Origin") ?? "http://localhost:5173";
var allowAnonymousReads = builder.Configuration.GetValue("Results:AllowAnonymousReads", true);
var parsedDashboardOrigins = dashboardOrigins
    .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

builder.Services.AddDbContext<ResultsDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("Results")
        ?? throw new InvalidOperationException("Connection string 'Results' not found.");

    options.UseNpgsql(connectionString, npgsql =>
    {
        npgsql.MigrationsAssembly(typeof(ResultsDbContext).Assembly.FullName);
    });
});

builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        if (parsedDashboardOrigins.Length > 0)
        {
            policy.WithOrigins(parsedDashboardOrigins);
        }
        else
        {
            policy.AllowAnyOrigin();
        }

        policy.AllowAnyMethod()
              .AllowAnyHeader();
    });
});

builder.Services.AddHealthChecks()
    .AddCheck<ResultsDatabaseHealthCheck>("results-db");

var authority = builder.Configuration["AuthServer:Authority"];

if (requiresJwt)
{
    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.Authority = authority;
            options.RequireHttpsMetadata = requiresHttps;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = false
            };
        });

    builder.Services.AddAuthorization();
}

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ResultsDbContext>();
    dbContext.Database.EnsureCreated();
}

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

app.UseSwagger();
app.UseSwaggerUI();

if (requiresHttps)
{
    app.UseHttpsRedirection();
}

app.UseCors();

if (requiresJwt)
{
    app.UseAuthentication();
    app.UseAuthorization();
}

app.MapHealthChecks("/healthz");

var createRunEndpoint = app.MapPost("/api/runs", async Task<Results<Created<RunResponse>, ValidationProblem>> (
    RunRequest request,
    ResultsDbContext dbContext,
    CancellationToken cancellationToken) =>
{
    var validationErrors = RunRequestValidator.Validate(request);
    if (validationErrors.Count > 0)
    {
        return TypedResults.ValidationProblem(validationErrors);
    }

    var run = new Run
    {
        Id = Guid.NewGuid(),
        Name = request.Name,
        Environment = request.Environment,
        StartedAt = request.StartedAt,
        CompletedAt = request.CompletedAt,
        ConfigurationJson = request.Configuration
    };

    foreach (var metric in request.Metrics)
    {
        run.Metrics.Add(new Metric
        {
            Id = Guid.NewGuid(),
            Name = metric.Name,
            Unit = metric.Unit,
            Value = metric.Value
        });
    }

    dbContext.Runs.Add(run);
    await dbContext.SaveChangesAsync(cancellationToken);

    return TypedResults.Created($"/api/runs/{run.Id}", RunResponse.FromEntity(run));
});

var listRunsEndpoint = app.MapGet("/api/runs", async Task<Ok<List<RunResponse>>> (ResultsDbContext dbContext, CancellationToken cancellationToken) =>
{
    var runs = await dbContext.Runs
        .AsNoTracking()
        .Include(run => run.Metrics)
        .OrderByDescending(run => run.StartedAt)
        .ToListAsync(cancellationToken);

    var response = runs.Select(RunResponse.FromEntity).ToList();
    return TypedResults.Ok(response);
});

var getRunEndpoint = app.MapGet("/api/runs/{id:guid}", async Task<Results<Ok<RunResponse>, NotFound>> (Guid id, ResultsDbContext dbContext, CancellationToken cancellationToken) =>
{
    var run = await dbContext.Runs
        .AsNoTracking()
        .Include(entity => entity.Metrics)
        .FirstOrDefaultAsync(entity => entity.Id == id, cancellationToken);

    if (run is null)
    {
        return TypedResults.NotFound();
    }

    return TypedResults.Ok(RunResponse.FromEntity(run));
});

if (requiresJwt)
{
    createRunEndpoint.RequireAuthorization();
    if (!allowAnonymousReads)
    {
        listRunsEndpoint.RequireAuthorization();
        getRunEndpoint.RequireAuthorization();
    }
}

app.MapPost("/api/benchrunner/run", async Task<Results<Ok<object>, BadRequest<string>>> (
    BenchRunnerRequest request,
    ILogger<Program> logger) =>
{
    try
    {
        var benchRunnerDirectory = Path.Combine(AppContext.BaseDirectory, "benchrunner");
        var benchRunnerDllPath = Path.Combine(benchRunnerDirectory, "BenchRunner.dll");

        if (!File.Exists(benchRunnerDllPath))
        {
            logger.LogError("BenchRunner binary not found at {Path}", benchRunnerDllPath);
            return TypedResults.BadRequest("BenchRunner tooling is not available on the server.");
        }

        var processStartInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            WorkingDirectory = benchRunnerDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        processStartInfo.ArgumentList.Add(benchRunnerDllPath);
        processStartInfo.ArgumentList.Add("--protocol");
        processStartInfo.ArgumentList.Add(request.Protocol);
        processStartInfo.ArgumentList.Add("--security");
        processStartInfo.ArgumentList.Add(request.Security);
        processStartInfo.ArgumentList.Add("--workload");
        processStartInfo.ArgumentList.Add(request.Workload);
        processStartInfo.ArgumentList.Add("--rps");
        processStartInfo.ArgumentList.Add(request.Rps.ToString(CultureInfo.InvariantCulture));
        processStartInfo.ArgumentList.Add("--duration");
        processStartInfo.ArgumentList.Add($"{request.Duration}s");
        processStartInfo.ArgumentList.Add("--warmup");
        processStartInfo.ArgumentList.Add($"{request.Warmup}s");
        processStartInfo.ArgumentList.Add("--connections");
        processStartInfo.ArgumentList.Add(request.Connections.ToString(CultureInfo.InvariantCulture));

        logger.LogInformation("Launching BenchRunner: {FileName} {Arguments}", processStartInfo.FileName, string.Join(' ', processStartInfo.ArgumentList));

        using var process = Process.Start(processStartInfo);
        if (process is null)
        {
            logger.LogError("Failed to start BenchRunner process.");
            return TypedResults.BadRequest("Failed to start BenchRunner process.");
        }

        var stdOutTask = process.StandardOutput.ReadToEndAsync();
        var stdErrTask = process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync();

        var stdOut = await stdOutTask;
        var stdErr = await stdErrTask;

        if (process.ExitCode != 0)
        {
            logger.LogError("BenchRunner exited with code {ExitCode}. stderr: {StdErr}", process.ExitCode, stdErr);
            return TypedResults.BadRequest($"BenchRunner failed with exit code {process.ExitCode}: {stdErr}");
        }

        logger.LogInformation("BenchRunner completed successfully. Output: {StdOut}", stdOut);
        return TypedResults.Ok<object>(new { message = "Benchmark completed successfully", stdout = stdOut });
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Failed to execute BenchRunner");
        return TypedResults.BadRequest($"Failed to execute BenchRunner: {ex.Message}");
    }
});

app.MapGet("/", () => TypedResults.Ok("ResultsService ready."));

app.Run();

record BenchRunnerRequest(string Protocol, string Security, string Workload, int Rps, int Duration, int Warmup, int Connections);
