using Application.Configuration;
using Domain;
using Infrastructure.Messaging;
using Infrastructure.Redis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<RedisSettings>(configuration.GetSection("RedisSettings"));

        services.AddSingleton<IConnectionMultiplexer>(serviceProvider =>
        {
            var redisSettings = serviceProvider
                .GetRequiredService<Microsoft.Extensions.Options.IOptions<RedisSettings>>()
                .Value;
            return ConnectionMultiplexer.Connect(redisSettings.Configuration);
        });

        services.AddSingleton<RedisStateRepository>();
        services.AddSingleton<IServerConfigurationStore>(serviceProvider => serviceProvider.GetRequiredService<RedisStateRepository>());
        services.AddSingleton<IServerStateStore>(serviceProvider => serviceProvider.GetRequiredService<RedisStateRepository>());
        services.AddSingleton<IStatusEventPublisher, StatusEventPublisher>();

        return services;
    }
}
