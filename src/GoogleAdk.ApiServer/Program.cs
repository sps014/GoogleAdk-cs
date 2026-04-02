using System.CommandLine;
using GoogleAdk.ApiServer.Cli;

var rootCommand = new RootCommand("Google ADK for .NET — Developer Tools");
rootCommand.Subcommands.Add(WebCommand.Create());
rootCommand.Subcommands.Add(ApiServerCommand.Create());
rootCommand.Subcommands.Add(RunCommand.Create());
rootCommand.Subcommands.Add(CreateCommand.Create());
rootCommand.Subcommands.Add(DeployCommand.Create());
rootCommand.Subcommands.Add(EvalCommand.Create());
rootCommand.Subcommands.Add(OptimizeCommand.Create());
rootCommand.Subcommands.Add(EvalSetCommand.Create());
rootCommand.Subcommands.Add(ConformanceCommand.Create());

return rootCommand.Parse(args).Invoke();
