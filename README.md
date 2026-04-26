# Agent Development Kit (ADK) for .NET (C#)

[![License](https://img.shields.io/badge/License-Apache_2.0-blue.svg)](LICENSE.md)
[![NuGet](https://img.shields.io/badge/NuGet-Preview-informational)](#-installation)

<html>
    <h2 align="center">
      <img src="docs/assets/agent-development-kit.png" width="256"/>
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

### 🧠 Intelligence & Models

- **Model Backends**: Support for **Gemini** (`GoogleAdk.Models.Gemini`) and **MEAI** (`GoogleAdk.Models.Meai`) for providers like Ollama. [More info](docs/models.md).
- **Planning**: Equip agents with ReAct-style planners via natural-language processing. [More info](docs/planning.md).
- **Caching**: Prompt and context caching using Gemini-backed implicit caching. [More info](docs/caching.md).

### 🛠️ Tools & Integrations

- **C# Source Gen Tools**: Effortlessly turn methods into tools using the `[FunctionTool]` attribute. [More info](docs/tools.md).
- **Agent Skills**: Folder-based skills (`SKILL.md`, `references/`, `assets/`, `scripts/`) with `SkillToolset` and optional `SkillLoader.LoadFromDirectory` for disk-backed skills, aligned with the Python ADK pattern. [More info](docs/skills.md).
- **MCP Support**: Full Model Context Protocol integration with dynamic tool discovery. [More info](docs/mcp.md).
- **OpenAPI Integration**: Generate tools seamlessly from OpenAPI specs.
- **Code Execution**: Run generated code within secure environments. [More info](docs/code-execution.md).
- **Text-to-Speech (TTS)**: Generate audio directly from compatible LLMs like `gemini-2.5-flash-preview-tts`. [More info](docs/tts.md).
- **Built-in Tools**: Use pre-configured tools for Computer Use (Browser Automation), Cloud Spanner (Query and Vector Search), BigQuery, Google Search, Vertex AI, code execution, and more. [More info](docs/features.md).

### 🔄 Orchestration & Workflows

- **Agent Orchestration**: Combine agents using `SequentialAgent`, `ParallelAgent`, or `LoopAgent` with customizable LLM pipelines. [More info](docs/orchestration.md).
- **A2A Protocol**: Built-in support for Remote Agent-to-Agent communication (both Client and Server). [More info](docs/a2a.md).
- **Plugins & Telemetry**: Lifecycle hooks, streaming events, and OpenTelemetry-style tracing. [More info](docs/plugins.md).

### 💾 State & Memory

- **State Management**: Flexible session state with placeholder injection for dynamic instructions. [More info](docs/state.md).
- **Memory**: Built-in memory services and persistence options including EF Core storage. [More info](docs/memory.md).

### 🚀 Development & Deployment

- **Code-First Approach**: Define agents, tools, and logic in C# for great testability and versioning.
- **ADK Web UI**: Embedded development server with a rich UI, REST/WebSocket APIs, and Swagger. [More info](docs/running-agents.md).
- **Evaluation**: Tools for systematic prompt/agent tuning and LLM-as-judge scoring. [More info](docs/evaluation-optimization.md).

## 🚀 Installation

Install the preview package via NuGet:

```bash
dotnet add package GoogleAdk --prerelease
```

## 📚 Documentation

For building, evaluating, and deploying agents, follow the docs and samples:

- **[In-repo docs](docs/index.md)** (feature guides for this .NET port)
- **[Skills](docs/skills.md)** — defining skills in code, loading from disk with `SkillLoader`, and wiring `SkillToolset` on an agent
- **[Samples](https://github.com/sps014/GoogleAdk-cs/tree/main/samples)** — includes **`GoogleAdk.Samples.Skills`**, which loads a `.skill/` folder next to the project and runs the agent in the console

## 🏁 Feature Highlight

### Same Features & Familiar Interface As Other ADKs:

```csharp
// Load env variables like GOOGLE_API_KEY
AdkEnv.Load();

var rootAgent = new LlmAgent(new()
{
    Name = "weather_assistant",
    Description = "An assistant that provides weather data.",
    Model = "gemini-2.5-flash",
    Instruction = "You are a helpful assistant. If the user asks about weather, use the GetWeatherData tool to provide the forecast.",
    
    // GetWeatherDataTool is generated from GetWeatherData in the format ADK expects
    Tools = [GetWeatherDataTool]
});

// Creates a webserver that can launch the ADK web UI and other endpoints 
await AdkServer.RunAsync(rootAgent);

//await ConsoleRunner.RunAsync(rootAgent); for running in console or you can easily implement your custom runner

/// <summary>
/// Fetches the current weather data for a given location.
/// </summary>
/// <param name="location">The location to get the weather for (e.g., 'New York')</param>
/// <returns>A WeatherData object containing the location and forecast</returns>
[FunctionTool]
static WeatherData? GetWeatherData(string location)
{
    return new WeatherData(location, "Sunny with a chance of rainbows");
}

public record WeatherData(string Location, string Forecast);
```

### Development UI

Same as the Python Development UI. A built-in development UI to help you test,
evaluate, debug, and showcase your agent(s).

<img alt="Image" src="https://github.com/user-attachments/assets/1f8db230-b8f2-4b3c-85c8-54c32ea9e379" />

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
