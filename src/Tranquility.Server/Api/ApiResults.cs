using System.Text.Json;
using Tranquility.Wire;
using Tranquility.Wire.Json;

namespace Tranquility.Server.Api;

public static class ApiResults
{
    /// <summary>Writes the documented exception envelope (L2-API-004).</summary>
    public static async Task WriteErrorAsync(HttpContext context, int statusCode, string type, string msg)
    {
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";
        if (statusCode == StatusCodes.Status401Unauthorized)
        {
            context.Response.Headers.WWWAuthenticate = "Bearer";
        }

        await JsonSerializer.SerializeAsync(context.Response.Body,
            new ExceptionEnvelope(new ExceptionInfo(type, msg)), WireJson.Options,
            context.RequestAborted);
    }
}
