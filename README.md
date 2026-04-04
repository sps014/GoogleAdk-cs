# Agent Development Kit (ADK) for .NET (C#)

[![License](https://img.shields.io/badge/License-Apache_2.0-blue.svg)](LICENSE.md)
[![NuGet](https://img.shields.io/badge/NuGet-Preview-informational)](#-installation)

<html>
    <h2 align="center">
      <img src="https://raw.githubusercontent.com/google/adk-python/main/assets/agent-development-kit.png" width="256"/>
    </h2>
    <h3 align="center">
      An open-source, code-first .NET toolkit for building, evaluating, and deploying sophisticated AI agents with flexibility and control.
    </h3>
  
</html>

Agent Development Kit (ADK) is designed for developers seeking fine-grained
control and flexibility when building advanced AI agents that are tightly
integrated with services in Google Cloud. It allows you to define agent
behavior, orchestration, and tool use directly in code, enabling robust
debugging, versioning, and deployment anywhere – from your laptop to the cloud.

---

> **⚠️ EXPERIMENTAL** - This project is in active development and the API will change. Builds are nightly and super frequent and may contain breaking changes. Use in production at your own risk.

## ✨ Key Features

- **ADK Web (development server & UI)**: Run agents with the embedded dev UI,
  REST/WebSocket APIs, Swagger, and optional static file hosting—via
  `AdkServer.RunAsync`, the CLI (`GoogleAdk.ApiServer`), or your own host. See
  [Running agents](docs/running-agents.md).
- **A2A (client + server)**: **Client** types in `src/GoogleAdk.Core/A2a`
  (`A2aClient`, `A2aRemoteAgent`, streaming/event helpers) for calling remote
  agents. **Server** endpoints via `MapA2aApi()` on the API host so your agent
  speaks the [A2A protocol](https://github.com/google/A2A/). Enable A2A on the
  dev server with `enableA2a: true` in `AdkServer.RunAsync` or the CLI `--a2a`
  flag. Details: [docs/a2a.md](docs/a2a.md).
- **Planning**: Attach an `IPlanner` to `LlmAgent` (e.g. built-in and ReAct-style
  planners) with natural-language planning processors in the LLM pipeline. See
  [docs/planning.md](docs/planning.md).
- **Prompt / context caching**: `ContextCacheConfig` on agents and apps, with
  Gemini-backed implicit caching via `GoogleAdk.Models.Gemini` and
  `ContextCacheRequestProcessor`. See [docs/caching.md](docs/caching.md).
- **C# tools & source generation**: Mark static/instance methods with
  `[FunctionTool]`; `GoogleAdk.SourceGenerators` emits partial classes with
  schema-backed `FunctionTool` instances (XML docs required for descriptions).
  See [docs/tools.md](docs/tools.md).
- **MCP (Model Context Protocol)**: `GoogleAdk.Tools.Mcp` provides `McpToolset`
  for stdio and HTTP MCP servers, dynamic tool discovery, and integration with
  `LlmAgent` toolsets. See [docs/mcp.md](docs/mcp.md).
- **OpenAPI-backed tools**: Generate tools from OpenAPI documents with
  `GoogleAdk.Tools.OpenApi`. Sample: `samples/GoogleAdk.Samples.OpenApi`.
- **Model backends**: **Gemini** (`GoogleAdk.Models.Gemini`) and **MEAI**
  (`GoogleAdk.Models.Meai`) for providers such as Ollama and other MEAI-compatible
  models. See [docs/models.md](docs/models.md).
- **Sessions & persistence**: In-memory sessions in core; optional **EF Core**
  session storage in `GoogleAdk.Sessions.EfCore`.
- **State & memory**: Session **state** (scoped keys, `{placeholder}` injection in
  instructions) via `State` and `RunnerConfig.InitialState` / dev server defaults;
  **memory** via `IBaseMemoryService` with `InMemoryMemoryService` as the runner default
  and `AgentContext` helpers (`AddSessionToMemoryAsync`, `SearchMemoryAsync`, etc.).
  See [docs/state.md](docs/state.md) and [docs/memory.md](docs/memory.md).
- **Evaluation & optimization**: `GoogleAdk.Evaluation` (datasets, inference,
  LLM-as-judge scoring) and `GoogleAdk.Optimization` for systematic prompt/agent
  improvement. See [docs/evaluation-optimization.md](docs/evaluation-optimization.md).
- **Rich built-in tools & Google integrations**: Search, maps, Vertex AI Search,
  Discovery Engine, URL context, code execution, artifacts, sub-agents, human-in-the-loop
  confirmations, and more—see [docs/tools.md](docs/tools.md) and
  [docs/features.md](docs/features.md).
- **Orchestration & processors**: `SequentialAgent`, `ParallelAgent`, `LoopAgent`,
  transfer-to-agent, and a configurable LLM request/response processor pipeline
  (instructions, code execution, output schema, context compaction, etc.). See
  [docs/orchestration.md](docs/orchestration.md).
- **Plugins, telemetry, streaming**: Hook lifecycle with plugins, OpenTelemetry-style
  tracing in the API server, and streaming event flows. See [docs/plugins.md](docs/plugins.md),
  [docs/streaming.md](docs/streaming.md).
- **Code-First Development**: Define agent logic, tools, and orchestration in C#
  for flexibility, testability, and versioning.

## 🚀 Installation

Install the preview package via NuGet:

```bash
dotnet add package GoogleAdk --prerelease
```

## 📚 Documentation

For building, evaluating, and deploying agents, follow the docs and samples:

- **[In-repo docs](docs/index.md)** (feature guides for this .NET port)
- **[Samples](https://github.com/sps014/GoogleAdk-cs/tree/main/samples)**

## 🏁 Feature Highlight

### Same Features & Familiar Interface As Other ADKs:

```csharp
//Load env variables like GOOGLE_API_KEY
AdkEnv.Load();

var rootAgent = new LlmAgent(new LlmAgentConfig
{
    Name = "search_assistant",
    Description = "An assistant that can search the web.",
    Model = "gemini-2.5-flash",
    Instruction = "You are a helpful assistant. Answer user questions using Google Search when needed. If User asks about weather use the GetWeather tool",
    Tools = [ToolRegistry.GOOGLE_SEARCH, GetWeatherDataTool],  //GetWeatherDataTool is generated from GetWeatherData in format ADK expect
});

await AdkServer.RunAsync(rootAgent); //creates a webserver that can launch the adk web ui and other endpoints 



/// <summary>
/// Fetches the current weather data for a given location.
/// </summary>
/// <param name="location">The location to get the weather for (e.g., 'New York')</param>
/// <returns>A WeatherData object containing the location and forecast</returns>
[FunctionTool]
static WeatherData? GetWeatherData(string location)
{
    // trigger recompilation
    return new WeatherData(location, "Sunny with a chance of rainbows");
}
public record WeatherData(string Location, string Forecast);


```

### Development UI

Same as the Python Development UI. A built-in development UI to help you test,
evaluate, debug, and showcase your agent(s).

<img  alt="Image" src="https://github.com/user-attachments/assets/1f8db230-b8f2-4b3c-85c8-54c32ea9e379" />

### Evaluate Agents

Use `GoogleAdk.Evaluation` and `GoogleAdk.Optimization` for eval sets, inference
runs, scoring, and prompt tuning. See
[docs/evaluation-optimization.md](docs/evaluation-optimization.md) and sample
`GoogleAdk.Samples.EvalOptimize`.

## 🤖 A2A and ADK integration

Remote agent-to-agent communication uses the
[A2A protocol](https://github.com/google/A2A/). **Client** code lives under
`src/GoogleAdk.Core/A2a`; **server** wiring is in `GoogleAdk.ApiServer` (`MapA2aApi`).
End-to-end coverage is in `tests/GoogleAdk.E2e.Tests`. See [docs/a2a.md](docs/a2a.md).


## 🏗️ Building the Project

To set up the project and build it from source, follow these steps:

1. **Install dependencies**:

   ```bash
   dotnet restore "GoogleAdk/GoogleAdk.slnx"
   ```

2. **Build the project**:

   ```bash
   dotnet build "GoogleAdk/GoogleAdk.slnx"
   ```

## 🤝 Contributing

We welcome contributions from the community! Whether it's bug reports, feature
requests, documentation improvements, or code contributions, please see our
guidelines:

- [General contribution guideline and flow](https://google.github.io/adk-docs/contributing-guide/).
- Then if you want to contribute code, please read
  [Code Contributing Guidelines](./CONTRIBUTING.md) to get started.

## 📄 License and intellectual property

Google’s **Agent Development Kit (ADK)**—the reference product family (for example, the Python ADK), its documentation, and related Google materials—is a **copyrighted product of Google LLC** and/or its affiliates. Google retains applicable rights in that product and in the **ADK** and **Google** trademarks.

**This repository** is a **.NET port**: an open-source implementation aligned with ADK-style APIs and patterns. It is **not** described here as Google’s own first-party, directly Google-copyrighted release of ADK for .NET; copyright in this port belongs to **its contributors**, who license the code under the **Apache License, Version 2.0**. See **[LICENSE.md](LICENSE.md)** for the full terms, including how upstream ADK, this port, and trademarks are distinguished.

Use, reproduction, and distribution of this repository are governed by the **Apache License, Version 2.0**; it does **not** transfer ownership of Google’s ADK, Google’s trademarks, or third-party materials to you. Third-party components may be subject to separate licenses as noted in the applicable files or notices.

If you contribute code, your contributions may be subject to the contributor license arrangements described in the contribution documentation. Nothing in this README is legal advice or a waiver of Google’s or any third party’s rights except as stated in **[LICENSE.md](LICENSE.md)** and applicable licenses.

---

_Happy Agent Building!_
