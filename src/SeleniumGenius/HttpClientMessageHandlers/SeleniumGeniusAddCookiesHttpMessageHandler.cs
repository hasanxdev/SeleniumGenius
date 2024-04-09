using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenQA.Selenium;
using SeleniumGenius.Models;

namespace SeleniumGenius.HttpClientMessageHandlers;

public class SeleniumGeniusAddCookiesHttpMessageHandler(
    IServiceScopeFactory serviceScopeFactory,
    IOptions<GeniusHttpMessageHandlerConfiguration> options) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (request.RequestUri?.ToString().Contains("token=") is false)
        {
            await using var serviceScope = serviceScopeFactory.CreateAsyncScope();
            var seleniumGeniusFactory = serviceScope.ServiceProvider
                .GetRequiredService<SeleniumGeniusFactory>();
            var geniusCreateResult = await seleniumGeniusFactory.CreateAsync(cancellationToken);
            
            ArgumentNullException.ThrowIfNull(geniusCreateResult);
            if (geniusCreateResult.Success is false)
            {
                throw new Exception(geniusCreateResult.Message);
            }

            var cookies = options.Value.ValidCookiesDomain.Any() 
                ? geniusCreateResult.Driver.GetCookiesContainsDomain(options.Value.ValidCookiesDomain) 
                : geniusCreateResult.Driver.GetAllCookies();

            var cookieString = string.Join(' ', cookies.Select(s => $"{s.Name}={s.Value};").ToArray());
            request.Headers.Add("cookie", cookieString);
        }   

        return await base.SendAsync(request, cancellationToken);
    }
}