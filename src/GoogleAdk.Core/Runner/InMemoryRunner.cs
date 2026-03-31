// Copyright 2025 Google LLC
// SPDX-License-Identifier: Apache-2.0

using GoogleAdk.Core.Agents;
using GoogleAdk.Core.Artifacts;
using GoogleAdk.Core.Memory;
using GoogleAdk.Core.Sessions;

namespace GoogleAdk.Core.Runner;

/// <summary>
/// A convenience runner that uses all in-memory service implementations.
/// Ideal for development, testing, and simple applications.
/// </summary>
public class InMemoryRunner : Runner
{
    public InMemoryRunner(string appName, BaseAgent agent)
        : base(new RunnerConfig
        {
            AppName = appName,
            Agent = agent,
            SessionService = new InMemorySessionService(),
            ArtifactService = new InMemoryArtifactService(),
            MemoryService = new InMemoryMemoryService(),
        })
    {
    }
}
