using System.Text.Json.Serialization;
using FtpTester.Models;
using FtpTester.Services;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Http.Json;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddEnvironmentVariables(prefix: "FTPTESTER_");
builder.Services.Configure<JsonOptions>(options => options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = builder.Configuration.GetValue<long>("Limits:MaxUploadBytes", 104_857_600);
});

builder.Services.AddLogging(logging =>
{
    logging.ClearProviders();
    logging.AddConsole();
});

builder.Services.AddSingleton<TestHistoryStore>();
builder.Services.AddSingleton<FluentFtpService>();
builder.Services.AddSingleton<IFtpTestService>(sp => sp.GetRequiredService<FluentFtpService>());
builder.Services.AddSingleton<IFtpTestService>(sp => FluentFtpService.CreateFtps(sp.GetRequiredService<ILogger<FluentFtpService>>()));
builder.Services.AddSingleton<IFtpTestService, SftpService>();
builder.Services.AddSingleton<ServiceSelector>();
builder.Services.AddSingleton<BenchmarkService>();
builder.Services.AddHealthChecks();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler(errorApp =>
    {
        errorApp.Run(async context =>
        {
            var feature = context.Features.Get<IExceptionHandlerFeature>();
            app.Logger.LogError(feature?.Error, "Unhandled request error");
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            await context.Response.WriteAsJsonAsync(new { error = "An unexpected server error occurred." });
        });
    });
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseDefaultFiles();
app.UseStaticFiles();
app.MapHealthChecks("/health");

app.MapPost("/api/test-connection", async (ConnectionRequest request, ServiceSelector selector, TestHistoryStore history, CancellationToken cancellationToken) =>
{
    var validation = ValidateConnection(request);
    if (validation.Count > 0)
    {
        return Results.ValidationProblem(validation.ToDictionary(error => error, error => new[] { error }));
    }

    var result = await selector.Resolve(request.Protocol).TestConnectionAsync(request, cancellationToken);
    history.Add(result);
    return result.Success ? Results.Ok(result) : Results.Problem(result.Error, statusCode: StatusCodes.Status502BadGateway, title: result.Message);
});

app.MapPost("/api/upload", async (HttpRequest httpRequest, ServiceSelector selector, TestHistoryStore history, CancellationToken cancellationToken) =>
{
    if (!httpRequest.HasFormContentType)
    {
        return Results.BadRequest(new { error = "Multipart form data is required." });
    }

    var form = await httpRequest.ReadFormAsync(cancellationToken);
    var request = ReadConnectionForm(form);
    var remotePath = form["remotePath"].ToString();
    var file = form.Files.GetFile("file");
    var validation = ValidateConnection(request).ToList();
    if (!ValidationService.IsSafeRemotePath(remotePath)) validation.Add("A valid remote path is required.");
    if (file is null || file.Length == 0) validation.Add("A non-empty upload file is required.");
    if (validation.Count > 0) return Results.ValidationProblem(validation.ToDictionary(error => error, error => new[] { error }));

    await using var stream = file!.OpenReadStream();
    var result = await selector.Resolve(request.Protocol).UploadAsync(request, stream, remotePath, file.Length, cancellationToken);
    history.Add(result);
    return result.Success ? Results.Ok(result) : Results.Problem(result.Error, statusCode: StatusCodes.Status502BadGateway, title: result.Message);
});

app.MapPost("/api/download", async (OperationRequest request, ServiceSelector selector, TestHistoryStore history, CancellationToken cancellationToken) =>
{
    var validation = ValidateOperation(request);
    if (validation.Count > 0) return Results.ValidationProblem(validation.ToDictionary(error => error, error => new[] { error }));

    var (result, content) = await selector.Resolve(request.Protocol).DownloadAsync(request, cancellationToken);
    history.Add(result);
    return result.Success
        ? Results.File(content, "application/octet-stream", Path.GetFileName(request.RemotePath), enableRangeProcessing: false)
        : Results.Problem(result.Error, statusCode: StatusCodes.Status502BadGateway, title: result.Message);
});

app.MapPost("/api/list-directory", async (OperationRequest request, ServiceSelector selector, TestHistoryStore history, CancellationToken cancellationToken) =>
{
    var validation = ValidateOperation(request);
    if (validation.Count > 0) return Results.ValidationProblem(validation.ToDictionary(error => error, error => new[] { error }));

    var result = await selector.Resolve(request.Protocol).ListDirectoryAsync(request, cancellationToken);
    history.Add(result);
    return result.Success ? Results.Ok(result) : Results.Problem(result.Error, statusCode: StatusCodes.Status502BadGateway, title: result.Message);
});

app.MapPost("/api/delete-file", async (OperationRequest request, ServiceSelector selector, TestHistoryStore history, CancellationToken cancellationToken) =>
{
    var validation = ValidateOperation(request);
    if (validation.Count > 0) return Results.ValidationProblem(validation.ToDictionary(error => error, error => new[] { error }));

    var result = await selector.Resolve(request.Protocol).DeleteFileAsync(request, cancellationToken);
    history.Add(result);
    return result.Success ? Results.Ok(result) : Results.Problem(result.Error, statusCode: StatusCodes.Status502BadGateway, title: result.Message);
});

app.MapPost("/api/benchmark", async (BenchmarkRequest request, BenchmarkService benchmark, CancellationToken cancellationToken) =>
{
    var validation = ValidateConnection(request).Concat(ValidationService.Validate(request)).Distinct().ToList();
    if (!ValidationService.IsSafeRemotePath(request.RemoteDirectory)) validation.Add("A valid remote directory is required.");
    if (validation.Count > 0) return Results.ValidationProblem(validation.ToDictionary(error => error, error => new[] { error }));

    var result = await benchmark.RunAsync(request, cancellationToken);
    return result.Success ? Results.Ok(result) : Results.Problem("Benchmark failed. Review operation details.", statusCode: StatusCodes.Status502BadGateway);
});

app.MapGet("/api/history", (TestHistoryStore history) => Results.Ok(history.GetAll()));

app.Run();

static IReadOnlyCollection<string> ValidateConnection(ConnectionRequest request)
{
    var validation = ValidationService.Validate(request).ToList();
    if (!Enum.IsDefined(request.Protocol)) validation.Add("Protocol must be FTP, FTPS, or SFTP.");
    if (request.Protocol == TransferProtocol.Sftp && request.PassiveMode) { }
    return validation.Distinct().ToArray();
}

static IReadOnlyCollection<string> ValidateOperation(OperationRequest request)
{
    var validation = ValidateConnection(request).Concat(ValidationService.Validate(request)).Distinct().ToList();
    if (!ValidationService.IsSafeRemotePath(request.RemotePath)) validation.Add("A valid remote path is required.");
    return validation;
}

static ConnectionRequest ReadConnectionForm(IFormCollection form)
{
    _ = Enum.TryParse<TransferProtocol>(form["protocol"].ToString(), ignoreCase: true, out var protocol);
    _ = int.TryParse(form["port"].ToString(), out var port);
    _ = bool.TryParse(form["passiveMode"].ToString(), out var passiveMode);
    return new ConnectionRequest
    {
        Protocol = protocol,
        Host = form["host"].ToString(),
        Port = port,
        Username = form["username"].ToString(),
        Password = form["password"].ToString(),
        PassiveMode = passiveMode
    };
}
