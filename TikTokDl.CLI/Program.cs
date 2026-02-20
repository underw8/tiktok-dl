using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Parsing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Spectre.Console;
using TikTokDl.CLI;
using TikTokDl.CLI.Commands;
using TikTokDl.Core.Application.Interfaces;
using TikTokDl.Core.Application.UseCases;
using TikTokDl.Core.Infrastructure.Services;

// ── Dependency Injection ──────────────────────────────────────────────────────

var services = new ServiceCollection();

services.AddLogging(b =>
{
    b.SetMinimumLevel(LogLevel.Warning); // quiet by default; use --verbose to lower
    b.AddConsole();
});

// Infrastructure services (each service creates its own HttpClient as per the original WinForms app)
services.AddSingleton<TikTokApiService>();
services.AddSingleton<IMediaApiService>(sp => sp.GetRequiredService<TikTokApiService>());
services.AddSingleton<IHdApiService, TikWmApiService>();
services.AddSingleton<IFileDownloadService, FileDownloadService>();
services.AddSingleton<IProgressReporter, CliProgress>();

// Browser service — CustomBrowserPath resolved from --browser option per-command,
// so we register a factory that reads from CliSettings at resolution time.
services.AddTransient<IBrowserService>(sp =>
    new PlaywrightBrowserService(
        sp.GetRequiredService<ILogger<PlaywrightBrowserService>>(),
        customBrowserPath: null   // overridden per-command via DownloadByUsernameUseCase
    ));

// Use cases
services.AddTransient<DownloadSingleMediaUseCase>();
services.AddTransient<DownloadFromFileUseCase>();
services.AddTransient<DownloadByUsernameUseCase>();

var sp = services.BuildServiceProvider();

// ── CLI Root Command ──────────────────────────────────────────────────────────

var rootCommand = new RootCommand("tiktok-dl — cross-platform TikTok downloader");

rootCommand.AddCommand(DownloadCommand.Build(sp));
rootCommand.AddCommand(DownloadUserCommand.Build(sp));
rootCommand.AddCommand(DownloadFileCommand.Build(sp));

AnsiConsole.MarkupLine("[dim]tiktok-dl v0.1 — powered by TikTokDl.Core[/]");

// Build parser without response-file handling so that @username args are treated as plain strings.
var parser = new CommandLineBuilder(rootCommand)
    .UseEnvironmentVariableDirective()
    .UseParseDirective()
    .UseSuggestDirective()
    .RegisterWithDotnetSuggest()
    .UseTypoCorrections()
    .UseParseErrorReporting()
    .UseExceptionHandler()
    .CancelOnProcessTermination()
    .Build();

// System.CommandLine beta4 bakes response-file handling into the tokenizer (not middleware),
// so @username args are treated as "read from file". Strip the leading @ here so that
// TikTok @username convention passes through as a plain string.
var processedArgs = args.Select(a => a.StartsWith('@') ? a[1..] : a).ToArray();
return await parser.InvokeAsync(processedArgs);
