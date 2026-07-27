using Microsoft.Extensions.DependencyInjection;

namespace Kebechet.Blazor.SortableJS;

/// <summary>Registers Blazor.SortableJS services.</summary>
public static class SortableServiceCollectionExtensions
{
    /// <summary>Registers default options resolved through dependency injection.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Configures the defaults applied underneath each component's options.</param>
    /// <returns>The same service collection, for chaining.</returns>
    /// <remarks>
    /// Registered as scoped, so on Blazor Server each circuit gets its own instance and a
    /// per-user or per-tenant default cannot leak into anyone else's session.
    /// </remarks>
    public static IServiceCollection AddSortableJs(this IServiceCollection services, Action<SortableOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        return services.AddScoped<ISortableDefaults>(_ =>
        {
            var options = new SortableOptions();
            configure(options);
            return new SortableDefaultsInstance(options);
        });
    }

    private sealed class SortableDefaultsInstance : ISortableDefaults
    {
        internal SortableDefaultsInstance(SortableOptions options)
        {
            Options = options;
        }

        public SortableOptions? Options { get; }
    }
}
