using Microsoft.Extensions.Configuration;
using Com.Ctrip.Framework.Apollo.Logging;
using Microsoft.Extensions.Hosting;

public static class ApolloConfigurationExtension
{
    public static IHostBuilder UseApollo(this IHostBuilder builder)
    {
        LogManager.UseConsoleLogging(LogLevel.Info);
        return builder
            .ConfigureAppConfiguration(config => { config.AddApollo(config.Build().GetSection("apollo")); });
    }
}