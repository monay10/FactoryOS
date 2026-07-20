using FactoryOS.Gateway.Tenancy;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FactoryOS.IntegrationTests.Gateway;

/// <summary>
/// "Tenant is always in scope", literally: once the gateway resolves a request's tenant, the rest of the
/// pipeline runs inside a logging scope carrying it, so every log line for the request is stamped with its
/// tenant. A capturing logger provider observes the scope the middleware opens.
/// </summary>
public sealed class TenantLoggingScopeTests
{
    [Fact]
    public async Task Opens_a_logging_scope_carrying_the_resolved_tenant()
    {
        var capture = new ScopeCapturingLoggerProvider();

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Logging.AddProvider(capture);
        builder.Services.AddTenantResolution();

        await using var app = builder.Build();
        app.UseTenantResolution();
        app.MapGet("/probe", () => Results.Ok());
        await app.StartAsync();

        var request = new HttpRequestMessage(HttpMethod.Get, new Uri("/probe", UriKind.Relative));
        request.Headers.Add("X-FactoryOS-Tenant", "acme");
        await app.GetTestClient().SendAsync(request);

        Assert.Contains(capture.Scopes, scope =>
            scope is IReadOnlyDictionary<string, object> state
            && state.TryGetValue(TenantResolutionMiddleware.TenantScopeKey, out var value)
            && value is "acme");
    }

    [Fact]
    public async Task Opens_no_tenant_scope_when_none_is_resolved()
    {
        var capture = new ScopeCapturingLoggerProvider();

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Logging.AddProvider(capture);
        builder.Services.AddTenantResolution();

        await using var app = builder.Build();
        app.UseTenantResolution();
        app.MapGet("/probe", () => Results.Ok());
        await app.StartAsync();

        await app.GetTestClient().GetAsync(new Uri("/probe", UriKind.Relative));

        Assert.DoesNotContain(capture.Scopes, scope =>
            scope is IReadOnlyDictionary<string, object> state
            && state.ContainsKey(TenantResolutionMiddleware.TenantScopeKey));
    }

    private sealed class ScopeCapturingLoggerProvider : ILoggerProvider
    {
        public List<object?> Scopes { get; } = [];

        public ILogger CreateLogger(string categoryName) => new CapturingLogger(this);

        public void Dispose()
        {
        }

        private sealed class CapturingLogger(ScopeCapturingLoggerProvider owner) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state)
                where TState : notnull
            {
                owner.Scopes.Add(state);
                return null;
            }

            public bool IsEnabled(LogLevel logLevel) => false;

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
            }
        }
    }
}
