using Microsoft.Extensions.Hosting;
using SeleniumGenius.Services;

namespace SeleniumGenius.HostedServices;

public class StopGeniusDriversHostedService(IHttpClientFactory httpClientFactory) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        GeniusDriverTermination.Terminate();
        return Task.CompletedTask;
    }
}