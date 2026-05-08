using GoogleAdk.Core.Abstractions.Models;
using Spectre.Console;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace GoogleAdk.Core.Runner;

public static partial class ConsoleRunner
{
    internal static async Task HandleAttachCommandAsync(
        string input,
        List<Part> stagedFiles,
        CancellationToken cancellationToken)
    {
        var filePaths = input.Substring("/attach ".Length).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        foreach (var path in filePaths)
        {
            try
            {
                var bytes = await File.ReadAllBytesAsync(path, cancellationToken);
                var extension = Path.GetExtension(path);
                string mimeType = "application/octet-stream";
                try
                {
                    mimeType = MimeTypes.MimeTypeMap.GetMimeType(extension);
                }
                catch
                {
                    // fallback to default
                }

                stagedFiles.Add(new Part
                {
                    InlineData = new InlineData
                    {
                        Data = Convert.ToBase64String(bytes),
                        MimeType = mimeType,
                        DisplayName = Path.GetFileName(path)
                    }
                });
                AnsiConsole.MarkupLine($"[grey]📎 Attached {Markup.Escape(Path.GetFileName(path))}[/]");
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]Failed to attach '{Markup.Escape(path)}': {Markup.Escape(ex.Message)}[/]");
            }
        }
    }
}
