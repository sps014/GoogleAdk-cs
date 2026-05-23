namespace GoogleAdk.ApiServer;

public record ApiErrorResponse(string Error, string? Code = null, object? Details = null);
