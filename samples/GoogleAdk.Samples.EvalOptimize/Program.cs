// ============================================================================
// Evaluation + Optimization Sample (LLM-powered)
// ============================================================================

using System.Text.RegularExpressions;
using GoogleAdk.Core;
using GoogleAdk.Core.Abstractions.Models;
using GoogleAdk.Core.Agents;
using GoogleAdk.Core.Runner;
using GoogleAdk.Evaluation;
using GoogleAdk.Evaluation.Models;
using GoogleAdk.Optimization;

AdkEnv.Load();

Console.WriteLine("=== Eval + Optimize Sample (LLM) ===\n");

var model = "gemini-2.5-flash";

// ---- 1. Run inference for an eval set -------------------------------------

var writerAgent = new LlmAgent(new LlmAgentConfig
{
    Name = "writer",
    Model = model,
    Instruction = "Answer in one concise sentence."
});

var writerRunner = new InMemoryRunner("eval-opt", writerAgent);

var evalSet = new EvalSet
{
    EvalSetId = "sample-set",
    EvalCases =
    [
        new EvalCase
        {
            EvalId = "gdpr",
            Conversation =
            [
                new Invocation
                {
                    UserContent = new Content
                    {
                        Role = "user",
                        Parts = [new Part { Text = "In one sentence, summarize GDPR." }]
                    }
                }
            ]
        },
        new EvalCase
        {
            EvalId = "slo",
            Conversation =
            [
                new Invocation
                {
                    UserContent = new Content
                    {
                        Role = "user",
                        Parts = [new Part { Text = "Explain SLOs to a new engineer in one sentence." }]
                    }
                }
            ]
        }
    ]
};

var evalService = new LocalEvalService();
var inferenceResults = await evalService.PerformInferenceAsync(writerRunner, evalSet);

Console.WriteLine($"Inference results: {inferenceResults.Count} cases");

// ---- 2. LLM-based evaluation ----------------------------------------------

var judgeRunner = CreateJudgeRunner(model);
var evaluator = new LlmJudgeEvaluator(judgeRunner);
var scoredResults = await evalService.EvaluateAsync(evalSet, inferenceResults, [evaluator]);

foreach (var caseResult in scoredResults)
{
    var metric = caseResult.Invocations[0].Metrics[evaluator.Name];
    Console.WriteLine($"Case {caseResult.EvalId} score: {metric.Score:0.00} ({metric.Reason})");
}

// ---- 3. LLM-based prompt optimization -------------------------------------

var optimizer = new SimplePromptOptimizer();
var sampler = new LlmPromptSampler(
    judgeRunner,
    "Write a concise, friendly refund reply that confirms the timeline.");

var optResult = await optimizer.OptimizeAsync(
    "Write a reply to the customer about their refund.",
    sampler);

Console.WriteLine($"\nOptimized prompt: {optResult.Optimized}");
Console.WriteLine($"Candidate count: {optResult.Candidates.Count}");

Console.WriteLine("\n=== Eval + Optimize Sample Complete ===");

static Runner CreateJudgeRunner(string model)
{
    var judgeAgent = new LlmAgent(new LlmAgentConfig
    {
        Name = "judge",
        Model = model,
        Instruction = "You are a strict grader. Output only a number between 0 and 1."
    });

    return new InMemoryRunner("eval-opt-judge", judgeAgent);
}


file sealed class LlmJudgeEvaluator : IEvalMetricEvaluator
{
    private readonly Runner _runner;
    public string Name => "llm_judge";

    public LlmJudgeEvaluator(Runner runner)
    {
        _runner = runner;
    }

    public async Task<EvalMetricResult> EvaluateAsync(
        Invocation invocation,
        InvocationResult result,
        CancellationToken cancellationToken = default)
    {
        var userText = invocation.UserContent?.Parts?.FirstOrDefault()?.Text ?? "(none)";
        var responseText = result.FinalResponse?.Parts?.FirstOrDefault()?.Text ?? "(none)";

        var prompt = $"""
Score the response from 0 to 1.
User: {userText}
Response: {responseText}
Return only a number between 0 and 1.
""";

        var (score, raw) = await EvalHelpers.ScoreWithLlmAsync(_runner, prompt, cancellationToken);
        return new EvalMetricResult
        {
            MetricName = Name,
            Score = score,
            Reason = raw
        };
    }
}

file sealed class LlmPromptSampler : ISampler<string>
{
    private readonly Runner _runner;
    private readonly string _goal;

    public LlmPromptSampler(Runner runner, string goal)
    {
        _runner = runner;
        _goal = goal;
    }

    public async Task<SamplingResult> SampleAndScoreAsync(
        string candidate,
        CancellationToken cancellationToken = default)
    {
        var prompt = $"""
Score how well this prompt helps an assistant achieve the goal.
Goal: {_goal}
Prompt: {candidate}
Return only a number between 0 and 1.
""";

        var (score, raw) = await EvalHelpers.ScoreWithLlmAsync(_runner, prompt, cancellationToken);
        return new SamplingResult
        {
            Score = score,
            Data = new Dictionary<string, object?> { ["raw"] = raw }
        };
    }
}

static class EvalHelpers
{
    public static async Task<(double score, string raw)> ScoreWithLlmAsync(
        Runner runner,
        string prompt,
        CancellationToken cancellationToken)
    {
        var content = new Content
        {
            Role = "user",
            Parts = [new Part { Text = prompt }]
        };

        string raw = "0";
        await foreach (var evt in runner.RunEphemeralAsync("judge", content, cancellationToken: cancellationToken))
        {
            if (evt.IsFinalResponse())
                raw = evt.StringifyContent().Trim();
        }

        return (ParseScore(raw), raw);
    }

    private static double ParseScore(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return 0;

        var match = Regex.Match(text, @"\d+(\.\d+)?");
        if (!match.Success) return 0;
        if (!double.TryParse(match.Value, out var score)) return 0;

        return Math.Clamp(score, 0, 1);
    }
}


