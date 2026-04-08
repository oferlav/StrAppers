using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace strAppersBackend.Logging;

/// <summary>
/// Drops "Request starting/finished" style lines for board chat polling (GET .../api/Boards/use/chat)
/// from <see cref="Microsoft.AspNetCore.Hosting.Diagnostics"/> while leaving other routes unchanged.
/// </summary>
internal static class BoardChatPollHostingLogSuppression
{
    internal const string ChatPathMarker = "/api/Boards/use/chat";

    internal static void WrapLoggerProviders(IServiceCollection services)
    {
        var descriptors = services.Where(d => d.ServiceType == typeof(ILoggerProvider)).ToList();
        foreach (var d in descriptors)
        {
            services.Remove(d);
            var captured = d;
            services.Add(new ServiceDescriptor(typeof(ILoggerProvider), sp => WrapProvider(captured, sp), d.Lifetime));
        }
    }

    private static ILoggerProvider WrapProvider(ServiceDescriptor original, IServiceProvider sp)
    {
        var inner = CreateInnerProvider(original, sp);
        return new WrappingLoggerProvider(inner);
    }

    private static ILoggerProvider CreateInnerProvider(ServiceDescriptor original, IServiceProvider sp)
    {
        if (original.ImplementationInstance is ILoggerProvider instance)
            return instance;
        if (original.ImplementationFactory != null)
            return (ILoggerProvider)original.ImplementationFactory(sp);
        if (original.ImplementationType != null)
            return (ILoggerProvider)ActivatorUtilities.CreateInstance(sp, original.ImplementationType);
        throw new InvalidOperationException("ILoggerProvider registration has no implementation.");
    }

    private sealed class WrappingLoggerProvider(ILoggerProvider inner) : ILoggerProvider
    {
        private readonly ConcurrentDictionary<string, ILogger> _loggers = new(StringComparer.Ordinal);

        public ILogger CreateLogger(string categoryName) =>
            _loggers.GetOrAdd(categoryName, cat =>
                cat == "Microsoft.AspNetCore.Hosting.Diagnostics"
                    ? new SuppressBoardChatGetLogger(inner.CreateLogger(cat))
                    : inner.CreateLogger(cat));

        public void Dispose() => inner.Dispose();
    }

    private sealed class SuppressBoardChatGetLogger(ILogger inner) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => inner.BeginScope(state);

        public bool IsEnabled(LogLevel logLevel) => inner.IsEnabled(logLevel);

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            var text = formatter(state, exception);
            if (IsBoardChatPollHostingLine(text))
                return;
            inner.Log(logLevel, eventId, state, exception, formatter);
        }

        private static bool IsBoardChatPollHostingLine(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            if (text.IndexOf(ChatPathMarker, StringComparison.OrdinalIgnoreCase) < 0) return false;
            // Hosting logs use "GET", "POST", etc.
            return text.Contains("GET", StringComparison.OrdinalIgnoreCase);
        }
    }
}
