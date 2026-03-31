// Copyright 2025 Google LLC
// SPDX-License-Identifier: Apache-2.0

// ============================================================================
// Code Executors Sample — Code Execution Models & Context
// ============================================================================
//
// Demonstrates:
//   1. CodeExecutionInput/Output — data models for code execution
//   2. BuiltInCodeExecutor — model-native code execution (Gemini 2.0+)
//   3. CodeExecutorContext — persistent execution state across invocations
//   4. BaseCodeExecutor — extensible base with configurable delimiters
// ============================================================================

using GoogleAdk.Core.Abstractions.Events;
using GoogleAdk.Core.Abstractions.Models;
using GoogleAdk.Core.Abstractions.Sessions;
using GoogleAdk.Core.CodeExecutors;

Console.WriteLine("=== Code Executors Sample ===\n");

// ── 1. Code Execution Data Models ──────────────────────────────────────────

Console.WriteLine("--- CodeExecutionInput/Output ---\n");

var input = new CodeExecutionInput
{
    Code = "import math\nprint(math.sqrt(144))",
    InputFiles = new List<CodeFile>
    {
        new() { Name = "data.csv", Content = Convert.ToBase64String("a,b\n1,2\n3,4"u8.ToArray()), MimeType = "text/csv" }
    },
    ExecutionId = "exec-001"
};

Console.WriteLine($"Code: {input.Code}");
Console.WriteLine($"Input Files: {input.InputFiles.Count} ({input.InputFiles[0].Name})");
Console.WriteLine($"Execution ID: {input.ExecutionId}");

var output = new CodeExecutionOutput
{
    Stdout = "12.0\n",
    Stderr = "",
    OutputFiles = new List<CodeFile>
    {
        new() { Name = "result.png", Content = "iVBORw0KGgo=", MimeType = "image/png" }
    }
};

Console.WriteLine($"\nStdout: {output.Stdout.Trim()}");
Console.WriteLine($"Stderr: {(string.IsNullOrEmpty(output.Stderr) ? "(empty)" : output.Stderr)}");
Console.WriteLine($"Output Files: {output.OutputFiles.Count} ({output.OutputFiles[0].Name})");

// ── 2. BuiltInCodeExecutor ─────────────────────────────────────────────────

Console.WriteLine("\n--- BuiltInCodeExecutor ---\n");

var builtIn = new BuiltInCodeExecutor();
Console.WriteLine($"OptimizeDataFile: {builtIn.OptimizeDataFile}");
Console.WriteLine($"Stateful: {builtIn.Stateful}");
Console.WriteLine($"ErrorRetryAttempts: {builtIn.ErrorRetryAttempts}");

// Show how it modifies LLM requests to enable code execution
var llmRequest = new LlmRequest
{
    Model = "gemini-2.5-flash"
};

builtIn.ProcessLlmRequest(llmRequest);
Console.WriteLine($"LLM Request tools count after processing: {llmRequest.Config?.Tools?.Count}");
Console.WriteLine("(BuiltInCodeExecutor adds code_execution tool declaration to the LLM request)");

// ── 3. CodeExecutorContext ─────────────────────────────────────────────────

Console.WriteLine("\n--- CodeExecutorContext ---\n");

var state = new State();
var execCtx = new CodeExecutorContext(state);

Console.WriteLine($"Initial Execution ID: {execCtx.GetExecutionId() ?? "(none)"}");
Console.WriteLine($"Initial Error Count: {execCtx.GetErrorCount()}");
Console.WriteLine($"Initial Processed Files: [{string.Join(", ", execCtx.GetProcessedFileNames())}]");

execCtx.SetExecutionId("session-42");
execCtx.AddProcessedFileName("data.csv");
execCtx.AddProcessedFileName("config.json");
execCtx.SetErrorCount(execCtx.GetErrorCount() + 1);

Console.WriteLine($"\nAfter updates:");
Console.WriteLine($"  Execution ID: {execCtx.GetExecutionId()}");
Console.WriteLine($"  Error Count: {execCtx.GetErrorCount()}");
Console.WriteLine($"  Processed Files: [{string.Join(", ", execCtx.GetProcessedFileNames())}]");

var delta = execCtx.GetStateDelta();
Console.WriteLine($"  State delta keys: [{string.Join(", ", delta.Keys)}]");

// ── 4. BaseCodeExecutor Configuration ──────────────────────────────────────

Console.WriteLine("\n--- BaseCodeExecutor Configuration ---\n");

Console.WriteLine("Default code block delimiters:");
foreach (var (open, close) in builtIn.CodeBlockDelimiters)
    Console.WriteLine($"  Open: {open.Trim()} → Close: {close.Trim()}");

Console.WriteLine($"Execution result delimiters: {builtIn.ExecutionResultDelimiters.Open.Trim()} → {builtIn.ExecutionResultDelimiters.Close.Trim()}");

Console.WriteLine("\n=== Code Executors Sample Complete ===");
