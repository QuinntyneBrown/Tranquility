namespace Tranquility.Server;

/// <summary>
/// Composition root shared by <c>Program</c> and the Kestrel-based acceptance
/// fixtures. Building through one method keeps the tested pipeline identical
/// to the deployed pipeline.
/// </summary>
public static class TranquilityApp
{
    public static WebApplication Build(string[] args, Action<WebApplicationBuilder>? configure = null)
    {
        var builder = WebApplication.CreateBuilder(args);
        configure?.Invoke(builder);

        var app = builder.Build();
        return app;
    }
}
