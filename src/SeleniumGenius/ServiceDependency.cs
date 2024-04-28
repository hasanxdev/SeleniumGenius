using System.Diagnostics;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using OpenQA.Selenium.Chrome;
using SeleniumGenius.HostedServices;
using SeleniumGenius.Services;

namespace SeleniumGenius;

public static class SeleniumGeniusServiceDependency
{
    /// <summary>
    /// this method terminate all running chrome drivers before application runned, then added SeleniumGenius
    /// </summary>
    public static void AddSeleniumGeniusWithPreTermination(this WebApplicationBuilder builder, Action<SeleniumGeniusOptions> options,
        Action<ChromeDriverService> service)
    {
        GeniusDriverTermination.Terminate();
        AddSeleniumGenius(builder, options, service);
    }
    
    public static void AddSeleniumGenius(this WebApplicationBuilder builder, Action<SeleniumGeniusOptions> options,
        Action<ChromeDriverService> service)
    {
        builder.Services.AddHostedService<StopGeniusDriversHostedService>();

        var chromeDriverOptions = new SeleniumGeniusOptions();
        options.Invoke(chromeDriverOptions);

        var chromeDriverPath = DownloadCompatibleDriver(builder);

        var chromeService = ChromeDriverService.CreateDefaultService(chromeDriverPath);
        service.Invoke(chromeService);

        builder.Services.AddSingleton<SeleniumGeniusOptions>(_ => chromeDriverOptions);
        builder.Services.AddTransient<ChromeDriverService>(_ => chromeService);
        builder.Services.AddScoped<SeleniumGeniusFactory>();
        builder.Services.AddHttpClient(nameof(SeleniumGeniusFactory));
    }

    private static string DownloadCompatibleDriver(WebApplicationBuilder builder)
    {
        var driverPath = Path.Combine(builder.Environment.ContentRootPath, "ChromeDriver");
        var osPlatform = Environment.OSVersion.Platform switch
        {
            PlatformID.Unix => "linux",
            PlatformID.Win32NT or PlatformID.Win32Windows => "windows",
            _ => throw new NotSupportedException("your os not supported")
        };
        
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                WindowStyle = ProcessWindowStyle.Hidden,
                FileName = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                    $"selenium-manager/{osPlatform}/selenium-manager"),
                Arguments = $"--browser chrome --cache-path {driverPath}"
            }
        };

        process.Start();

        process.WaitForExit();

        var chromeDriverPath = Directory.GetFiles(driverPath, "*.*", SearchOption.AllDirectories)
            .Where(file => Path.GetExtension(file).ToLower() != ".json")
            .OrderBy(s => s)
            .First(p => p.Contains("chromedriver"));

        if (Environment.OSVersion.Platform == PlatformID.Unix)
        {
            Process.Start("chmod", "+x" + chromeDriverPath.Replace("/app/", "./"));
        }

        driverPath = chromeDriverPath.Replace("/app/", "./");
        Console.WriteLine("[INF] found chrome driver path: " + driverPath);
        
        return driverPath;
    }
}