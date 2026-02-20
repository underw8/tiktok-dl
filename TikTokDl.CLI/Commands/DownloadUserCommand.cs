using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;
using TikTokDl.Core.Application.UseCases;
using TikTokDl.Core.Domain.Models;

namespace TikTokDl.CLI.Commands;

/// <summary>
/// tiktok-dl download-user &lt;username&gt; [options]
/// Scrapes a TikTok profile via Playwright and bulk-downloads all posts.
/// </summary>
public static class DownloadUserCommand
{
    public static Command Build(IServiceProvider services)
    {
        var usernameArg = new Argument<string>("username",
            "TikTok username (with or without @) or full profile URL");

        var hdOption = new Option<bool>("--hd", "Download in HD quality");
        var outputOption = new Option<string?>("--output", "Output directory");
        outputOption.AddAlias("-o");
        var browserOption = new Option<string?>("--browser",
            "Path to browser executable for Playwright (default: bundled Chromium)");

        var cmd = new Command("download-user",
            "Download all posts from a TikTok profile (requires browser)")
        {
            usernameArg, hdOption, outputOption, browserOption
        };

        cmd.SetHandler(async (string username, bool hd, string? output, string? browser) =>
        {
            var useCase = services.GetRequiredService<DownloadByUsernameUseCase>();
            var options = DownloadOptions.Default with
            {
                Hd = hd,
                OutputDirectory = output ?? DownloadOptions.Default.OutputDirectory,
                CustomBrowserPath = browser
            };

            AnsiConsole.MarkupLine($"[yellow]Note:[/] A browser window will open. " +
                "Scroll through the profile page until it finishes loading automatically.");

            var result = await useCase.ExecuteAsync(username, options);

            if (result.IsSuccess)
            {
                var r = result.Value!;
                AnsiConsole.MarkupLine(
                    $"[green]Done:[/] {r.Succeeded} downloaded, " +
                    $"{r.Skipped} skipped, {r.Failed} failed of {r.Total} total");
            }
            else
            {
                AnsiConsole.MarkupLine($"[red]Error:[/] {Markup.Escape(result.ErrorMessage!)}");
            }
        },
        usernameArg, hdOption, outputOption, browserOption);

        return cmd;
    }
}
