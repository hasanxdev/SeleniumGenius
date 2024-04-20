using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.ObjectPool;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Internal;
using SeleniumGenius.Exceptions;
using SeleniumGenius.Models;

namespace SeleniumGenius;

public class SeleniumGeniusFactory(
    SeleniumGeniusOptions seleniumGeniusOptions,
    ChromeDriverService chromeDriverService,
    ILogger<SeleniumGeniusFactory> logger,
    IHttpClientFactory httpClientFactoryFactory) : IDisposable, IAsyncDisposable
{
    private static readonly ConcurrentDictionary<int, GeniusCreateResult> DriversDictionary = new();
    private static SemaphoreSlim _semaphoreSlim = new(1, 1);
    private int _currentPort = -1;

    public async Task<GeniusCreateResult> CreateAsync(CancellationToken cancellationToken)
    {
        logger.LogTrace("wait for available port");
        _currentPort = await seleniumGeniusOptions.AvailablePorts.Reader.ReadAsync(cancellationToken);
        logger.LogTrace("get success get port " + _currentPort);

        if (DriversDictionary.TryGetValue(_currentPort, out var result))
        {
            return result;
        }

        return await Task.Run(async () =>
        {
            await ShutdownChromeDriver(_currentPort);

            if (seleniumGeniusOptions.ClearCacheAfterDispose)
            {
                var userDataDir = GenerateUserDataDir(_currentPort);
                if (Directory.Exists(userDataDir))
                {
                    Directory.Delete(userDataDir, true);
                }
            }

            return await StartDriver(_currentPort, cancellationToken);
        }, cancellationToken);
    }

    private async Task<GeniusCreateResult> StartDriver(int port, CancellationToken cancellationToken)
    {
        var service = ChromeDriverService.CreateDefaultService(chromeDriverService.DriverServicePath,
            chromeDriverService.DriverServiceExecutableName);

        service.DriverServicePath = chromeDriverService.DriverServicePath;
        service.DriverServiceExecutableName = chromeDriverService.DriverServiceExecutableName;
        service.Port = port;
        service.HostName = chromeDriverService.HostName;
        service.InitializationTimeout = chromeDriverService.InitializationTimeout;
        service.LogPath = chromeDriverService.LogPath;
        service.DisableBuildCheck = chromeDriverService.DisableBuildCheck;
        service.EnableAppendLog = chromeDriverService.EnableAppendLog;
        service.EnableVerboseLogging = chromeDriverService.EnableVerboseLogging;
        service.PortServerAddress = chromeDriverService.PortServerAddress;
        service.UrlPathPrefix = chromeDriverService.UrlPathPrefix;
        service.AndroidDebugBridgePort = chromeDriverService.AndroidDebugBridgePort;
        service.HideCommandPromptWindow = chromeDriverService.HideCommandPromptWindow;
        service.SuppressInitialDiagnosticInformation = chromeDriverService.SuppressInitialDiagnosticInformation;
        service.WhitelistedIPAddresses = chromeDriverService.WhitelistedIPAddresses;
        service.Start();

        var options = new ChromeOptions()
        {
            BrowserVersion = seleniumGeniusOptions.BrowserVersion,
            DebuggerAddress = seleniumGeniusOptions.DebuggerAddress,
            AndroidOptions = seleniumGeniusOptions.AndroidOptions,
            MinidumpPath = seleniumGeniusOptions.MinidumpPath,
            BinaryLocation = seleniumGeniusOptions.BinaryLocation,
            LeaveBrowserRunning = seleniumGeniusOptions.LeaveBrowserRunning,
            PerformanceLoggingPreferences = seleniumGeniusOptions.PerformanceLoggingPreferences,
            Proxy = seleniumGeniusOptions.Proxy,
            EnableDownloads = seleniumGeniusOptions.EnableDownloads,
            PlatformName = seleniumGeniusOptions.PlatformName,
            AcceptInsecureCertificates = seleniumGeniusOptions.AcceptInsecureCertificates,
            PageLoadStrategy = seleniumGeniusOptions.PageLoadStrategy,
            UnhandledPromptBehavior = seleniumGeniusOptions.UnhandledPromptBehavior,
            UseStrictFileInteractability = seleniumGeniusOptions.UseStrictFileInteractability,
            UseWebSocketUrl = seleniumGeniusOptions.UseWebSocketUrl
        };
        options.AddArguments(seleniumGeniusOptions.Arguments);
        options.AddExtensions(seleniumGeniusOptions.Extensions);

        if (seleniumGeniusOptions.UserDataDir is not null)
        {
            options.AddArguments("user-data-dir=" + GenerateUserDataDir(port));
        }

        await _semaphoreSlim.WaitAsync(cancellationToken);
        GeniusDriver driver;
        
        try
        {
            driver = new(service, options, TimeSpan.FromMinutes(10));
        }
        finally
        {
            _semaphoreSlim.Release();
        }

        driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(5);

        var loginResult = seleniumGeniusOptions.Login?.Invoke(new LoginRequest()
        {
            Driver = driver.Browser,
            LoginData = seleniumGeniusOptions.LoginDataList[
                Math.Abs((SeleniumGeniusOptions.StaticPort - _currentPort) % seleniumGeniusOptions.LoginDataList.Count)]
        });

        var result = new GeniusCreateResult()
        {
            Success = loginResult?.Success ?? true,
            Message = loginResult?.Message ?? "Success without login",
            Driver = driver
        };

        DriversDictionary.TryAdd(port, result);

        return result;
    }

    private string GenerateUserDataDir(int port)
    {
        return Path.Combine(seleniumGeniusOptions.UserDataDir, (port - SeleniumGeniusOptions.StaticPort).ToString());
    }

    private async Task ShutdownChromeDriver(int port)
    {
        try
        {
            using var httpClient = httpClientFactoryFactory.CreateClient(nameof(SeleniumGeniusFactory));
            await httpClient.GetAsync($"http://localhost:{port}/shutdown");
        }
        catch
        {
            // ignore
        }
    }


    public void Dispose()
    {
        DisposeAsync().GetAwaiter().GetResult();
    }

    public async ValueTask DisposeAsync()
    {
        if (_currentPort != -1)
        {
            if (seleniumGeniusOptions.DisposeDriverByDi)
            {
                if (DriversDictionary.Remove(_currentPort, out var geniusCreateResult))
                {
                    geniusCreateResult.Driver.Dispose();
                }

                if (seleniumGeniusOptions.ClearCacheAfterDispose)
                {
                    var userDataDir = GenerateUserDataDir(_currentPort);
                    if (Directory.Exists(userDataDir))
                    {
                        Directory.Delete(userDataDir, true);
                    }
                }
            }

            await seleniumGeniusOptions.AvailablePorts.Writer.WriteAsync(_currentPort);
        }
    }
}