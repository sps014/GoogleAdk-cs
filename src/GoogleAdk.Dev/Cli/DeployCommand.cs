// Copyright 2025 Google LLC
// SPDX-License-Identifier: Apache-2.0

using System.CommandLine;
using System.Diagnostics;

namespace GoogleAdk.Dev.Cli;

/// <summary>
/// The "deploy" command — deploys an agent to Google Cloud Run.
/// Usage: adk deploy [agents_dir] --project PROJECT [--region REGION] [--service-name NAME] [--port 8080] [--with-ui]
/// </summary>
public static class DeployCommand
{
    public static Command Create()
    {
        var agentsDirArg = new Argument<string>("agents_dir")
        {
            Description = "Directory containing agent assemblies or projects.",
            DefaultValueFactory = _ => ".",
        };

        var projectOption = new Option<string>("--project")
        {
            Description = "Google Cloud project ID.",
            Required = true,
        };

        var regionOption = new Option<string?>("--region")
        {
            Description = "Google Cloud region (e.g. us-central1).",
        };

        var serviceNameOption = new Option<string>("--service-name")
        {
            Description = "Cloud Run service name.",
            DefaultValueFactory = _ => "adk-agent",
        };

        var portOption = new Option<int>("--port")
        {
            Description = "Port the service listens on.",
            DefaultValueFactory = _ => 8080,
        };

        var withUiOption = new Option<bool>("--with-ui")
        {
            Description = "Include the dev UI in the deployment.",
            DefaultValueFactory = _ => false,
        };

        var logLevelOption = new Option<string>("--log-level")
        {
            Description = "Logging level.",
            DefaultValueFactory = _ => "info",
        };

        var command = new Command("deploy", "Deploy an agent to Google Cloud Run.");
        command.Arguments.Add(agentsDirArg);
        command.Options.Add(projectOption);
        command.Options.Add(regionOption);
        command.Options.Add(serviceNameOption);
        command.Options.Add(portOption);
        command.Options.Add(withUiOption);
        command.Options.Add(logLevelOption);

        command.SetAction(async parseResult =>
        {
            var agentsDir = parseResult.GetValue(agentsDirArg);
            var project = parseResult.GetValue(projectOption);
            var region = parseResult.GetValue(regionOption);
            var serviceName = parseResult.GetValue(serviceNameOption);
            var port = parseResult.GetValue(portOption);
            var withUi = parseResult.GetValue(withUiOption);
            var logLevel = parseResult.GetValue(logLevelOption);

            await DeployToCloudRunAsync(agentsDir!, project!, region, serviceName!, port, withUi, logLevel!);
        });

        return command;
    }

    private static async Task DeployToCloudRunAsync(
        string agentsDir, string project, string? region, string serviceName,
        int port, bool withUi, string logLevel)
    {
        Console.WriteLine($"Deploying agent from '{agentsDir}' to Cloud Run...");
        Console.WriteLine($"  Project: {project}");
        Console.WriteLine($"  Service: {serviceName}");
        Console.WriteLine($"  Port: {port}");
        if (region != null)
            Console.WriteLine($"  Region: {region}");

        // Create a temporary directory for deployment artifacts
        var tempDir = Path.Combine(Path.GetTempPath(), $"adk-deploy-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            // Generate Dockerfile
            var dockerfileContent = CreateDockerfileContent(port, withUi, logLevel);
            await File.WriteAllTextAsync(Path.Combine(tempDir, "Dockerfile"), dockerfileContent);

            // Copy agent files
            CopyDirectory(agentsDir, tempDir);

            Console.WriteLine("Deploying to Cloud Run via gcloud...");

            // Build gcloud arguments
            var gcloudArgs = new List<string>
            {
                "run", "deploy", serviceName,
                "--source", tempDir,
                "--project", project,
                "--port", port.ToString(),
                "--labels", "created-by=adk",
            };

            if (region != null)
            {
                gcloudArgs.Add("--region");
                gcloudArgs.Add(region);
            }

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "gcloud",
                    Arguments = string.Join(" ", gcloudArgs),
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                }
            };

            process.Start();
            var stdout = await process.StandardOutput.ReadToEndAsync();
            var stderr = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                Console.Error.WriteLine($"Deployment failed with exit code {process.ExitCode}:");
                Console.Error.WriteLine(stderr);
                return;
            }

            Console.WriteLine("Deployment successful!");
            Console.WriteLine(stdout);
        }
        finally
        {
            // Cleanup temp directory
            try { Directory.Delete(tempDir, recursive: true); } catch { /* best effort */ }
        }
    }

    internal static string CreateDockerfileContent(int port, bool withUi, string logLevel)
    {
        var adkCommand = withUi ? "web" : "api_server";
        return $"""
            FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
            WORKDIR /app
            COPY . .
            RUN dotnet restore
            RUN dotnet publish -c Release -o /out

            FROM mcr.microsoft.com/dotnet/aspnet:10.0
            WORKDIR /app
            COPY --from=build /out .
            EXPOSE {port}
            ENTRYPOINT ["dotnet", "GoogleAdk.Dev.dll", "{adkCommand}", "--port", "{port}", "--bind", "0.0.0.0"]
            """;
    }

    private static void CopyDirectory(string source, string destination)
    {
        var dir = new DirectoryInfo(source);
        if (!dir.Exists)
            throw new DirectoryNotFoundException($"Source directory not found: {source}");

        foreach (var file in dir.GetFiles())
        {
            file.CopyTo(Path.Combine(destination, file.Name), overwrite: true);
        }

        foreach (var subDir in dir.GetDirectories())
        {
            if (subDir.Name is "bin" or "obj" or ".git" or "node_modules")
                continue;
            var destSubDir = Path.Combine(destination, subDir.Name);
            Directory.CreateDirectory(destSubDir);
            CopyDirectory(subDir.FullName, destSubDir);
        }
    }
}
