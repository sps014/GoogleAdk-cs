using GoogleAdk.Core.Abstractions.Tools;
using GoogleAdk.Core.Agents;

namespace GoogleAdk.Samples.RequireConfirmation;

public static partial class RequireConfirmationTools
{
    /// <summary>Reimburse the employee for the given amount.</summary>
    /// <param name="amount">Dollar amount to reimburse.</param>
    [FunctionTool(Name = "reimburse")]
    public static object? Reimburse(int amount)
    {
        if (amount <= 0)
            return new Dictionary<string, object?> { ["status"] = "Invalid reimbursement amount." };

        return new Dictionary<string, object?>
        {
            ["status"] = "ok",
            ["amount"] = amount
        };
    }

    /// <summary>Request time off for the employee.</summary>
    /// <param name="days">Number of days requested.</param>
    /// <param name="context">Agent context used for confirmation flow.</param>
    [FunctionTool(Name = "request_time_off", RequireConfirmation = true)]
    public static object? RequestTimeOff(int days, AgentContext context)
    {
        if (days <= 0)
            return new Dictionary<string, object?> { ["status"] = "Invalid days to request." };

        return new Dictionary<string, object?>
        {
            ["status"] = "pending_approval",
            ["requested_days"] = days
        };
    }
}
