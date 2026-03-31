// Copyright 2025 Google LLC
// SPDX-License-Identifier: Apache-2.0

using GoogleAdk.Core.Abstractions.Events;
using GoogleAdk.Core.Abstractions.Models;
using GoogleAdk.Core.Abstractions.Sessions;
using GoogleAdk.Core.Agents;
using GoogleAdk.Core.CodeExecutors;

namespace GoogleAdk.Core.Tests;

public class CodeExecutorTests
{
    [Fact]
    public void CodeExecutionInput_DefaultValues()
    {
        var input = new CodeExecutionInput();
        Assert.Equal(string.Empty, input.Code);
        Assert.Empty(input.InputFiles);
        Assert.Null(input.ExecutionId);
    }

    [Fact]
    public void CodeExecutionOutput_DefaultValues()
    {
        var output = new CodeExecutionOutput();
        Assert.Equal(string.Empty, output.Stdout);
        Assert.Equal(string.Empty, output.Stderr);
        Assert.Empty(output.OutputFiles);
    }

    [Fact]
    public void CodeFile_Properties()
    {
        var file = new CodeFile
        {
            Name = "data.csv",
            Content = Convert.ToBase64String("a,b\n1,2"u8.ToArray()),
            MimeType = "text/csv"
        };

        Assert.Equal("data.csv", file.Name);
        Assert.Equal("text/csv", file.MimeType);
        Assert.NotEmpty(file.Content);
    }

    [Fact]
    public void BaseCodeExecutor_DefaultConfiguration()
    {
        var executor = new BuiltInCodeExecutor();

        Assert.False(executor.OptimizeDataFile);
        Assert.False(executor.Stateful);
        Assert.Equal(2, executor.ErrorRetryAttempts);
        Assert.Equal(2, executor.CodeBlockDelimiters.Count);
    }

    [Fact]
    public async Task BuiltInCodeExecutor_ReturnsEmptyOutput()
    {
        var executor = new BuiltInCodeExecutor();
        var context = new InvocationContext
        {
            Session = Session.Create("s1", "app", "user"),
            Agent = new TestAgent("root"),
        };

        var result = await executor.ExecuteCodeAsync(context, new CodeExecutionInput { Code = "print('hi')" });

        Assert.Equal(string.Empty, result.Stdout);
        Assert.Equal(string.Empty, result.Stderr);
        Assert.Empty(result.OutputFiles);
    }

    [Fact]
    public void BuiltInCodeExecutor_ProcessLlmRequest_AddsToolDeclaration()
    {
        var executor = new BuiltInCodeExecutor();
        var request = new LlmRequest { Model = "gemini-2.5-flash" };

        executor.ProcessLlmRequest(request);

        Assert.NotNull(request.Config?.Tools);
        Assert.Single(request.Config!.Tools!);
    }

    [Fact]
    public void BuiltInCodeExecutor_ProcessLlmRequest_ThrowsForOldModel()
    {
        var executor = new BuiltInCodeExecutor();
        var request = new LlmRequest { Model = "gemini-1.5-pro" };

        Assert.Throws<InvalidOperationException>(() => executor.ProcessLlmRequest(request));
    }

    [Fact]
    public void CodeExecutorContext_GetSetExecutionId()
    {
        var state = new State();
        var ctx = new CodeExecutorContext(state);

        Assert.Null(ctx.GetExecutionId());

        ctx.SetExecutionId("exec-42");
        Assert.Equal("exec-42", ctx.GetExecutionId());
    }

    [Fact]
    public void CodeExecutorContext_ProcessedFileNames()
    {
        var state = new State();
        var ctx = new CodeExecutorContext(state);

        Assert.Empty(ctx.GetProcessedFileNames());

        ctx.AddProcessedFileName("file1.csv");
        ctx.AddProcessedFileName("file2.json");

        var names = ctx.GetProcessedFileNames();
        Assert.Contains("file1.csv", names);
        Assert.Contains("file2.json", names);
    }

    [Fact]
    public void CodeExecutorContext_ErrorCount()
    {
        var state = new State();
        var ctx = new CodeExecutorContext(state);

        Assert.Equal(0, ctx.GetErrorCount());

        ctx.SetErrorCount(ctx.GetErrorCount() + 1);
        ctx.SetErrorCount(ctx.GetErrorCount() + 1);

        Assert.Equal(2, ctx.GetErrorCount());
    }

    [Fact]
    public void CodeExecutorContext_GetStateDelta_ReturnsContext()
    {
        var state = new State();
        var ctx = new CodeExecutorContext(state);

        ctx.SetExecutionId("exec-1");
        var delta = ctx.GetStateDelta();

        Assert.NotEmpty(delta);
    }
}
