using MediaServer.Server.Networking;

namespace MediaServer.Server.Middleware;

public sealed class LanAccessMiddleware
{
    private readonly RequestDelegate _next;
    private readonly LanAccessEvaluator _evaluator;

    public LanAccessMiddleware(RequestDelegate next, LanAccessEvaluator evaluator)
    {
        _next = next;
        _evaluator = evaluator;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var remoteIp = context.Connection.RemoteIpAddress;
        if (!_evaluator.IsAllowed(remoteIp))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsync("Forbidden: LAN access only");
            return;
        }

        await _next(context);
    }
}
