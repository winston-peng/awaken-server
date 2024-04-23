using Microsoft.Extensions.Configuration;
using Com.Ctrip.Framework.Apollo.Logging;
using Microsoft.Extensions.Hosting;

namespace CAServer;

public static class ApolloConfigurationExtension
{
    public static IHostBuilder UseApollo(this IHostBuilder builder)
    {
        LogManager.UseConsoleLogging(LogLevel.Info);
        var result = builder
            .ConfigureAppConfiguration(config =>
            {
                config.AddApollo(config.Build().GetSection("apollo"));
            });
        return result;
    }
}