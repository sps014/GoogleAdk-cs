// Copyright 2025 Google LLC
// SPDX-License-Identifier: Apache-2.0

using System.CommandLine;
using GoogleAdk.Dev.Cli;

var rootCommand = new RootCommand("Google ADK for .NET — Developer Tools");
rootCommand.Subcommands.Add(WebCommand.Create());
rootCommand.Subcommands.Add(ApiServerCommand.Create());
rootCommand.Subcommands.Add(RunCommand.Create());
rootCommand.Subcommands.Add(CreateCommand.Create());
rootCommand.Subcommands.Add(DeployCommand.Create());

return rootCommand.Parse(args).Invoke();
