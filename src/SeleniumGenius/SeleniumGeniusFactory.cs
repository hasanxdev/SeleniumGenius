using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using OpenQA.Selenium.Chrome;
using SeleniumGenius.Models;

namespace SeleniumGenius;

public class SeleniumGeniusFactory(
    SeleniumGeniusOptions seleniumGeniusOptions,
    ChromeDriverService chromeDriverService,
    ILogger<SeleniumGeniusFactory> logger,
    IHttpClientFactory httpClientFactoryFactory) : IDisposable, IAsyncDisposable
{
    private bool _isDisposed;
    private readonly ThreadLocal<int> _currentPorts = new(() => -1, true);
    private static readonly ConcurrentDictionary<int, GeniusCreateResult> DriversDictionary = new();
    private static SemaphoreSlim SemaphoreSlim = new(1, 1);

    public async Task<GeniusCreateResult> CreateAsync(CancellationToken cancellationToken)
    {
        logger.LogTrace("wait for available port");
        var currentPort = await seleniumGeniusOptions.AvailablePorts.Reader.ReadAsync(cancellationToken);
        _currentPorts.Value = currentPort;
        logger.LogTrace("get success get port " + currentPort);

        if (DriversDictionary.TryGetValue(currentPort, out var result))
        {
            return result;
        }

        return await Task.Run(async () =>
        {
            await ShutdownChromeDriver(currentPort);

            if (seleniumGeniusOptions.ClearCacheAfterDispose)
            {
                var userDataDir = GenerateUserDataDir(currentPort);
                if (Directory.Exists(userDataDir))
                {
                    Directory.Delete(userDataDir, true);
                }
            }

            return await StartDriver(currentPort, cancellationToken);
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
        options.AddArgument($"--scriptpid-{Process.GetCurrentProcess().Id}");
        
        if (seleniumGeniusOptions.UserDataDir is not null)
        {
            options.AddArguments("user-data-dir=" + GenerateUserDataDir(port));
        }

        GeniusDriver driver;
        
        try
        {
            await SemaphoreSlim.WaitAsync(cancellationToken);
            driver = new(service, options, TimeSpan.FromMinutes(10));
        }
        finally
        {
            SemaphoreSlim.Release();
        }

        driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(5);

        var loginResult = seleniumGeniusOptions.Login?.Invoke(new LoginRequest()
        {
            Driver = driver.Browser,
            LoginData = seleniumGeniusOptions.LoginDataList[
                Math.Abs((SeleniumGeniusOptions.StaticPort - port) % seleniumGeniusOptions.LoginDataList.Count)]
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
        if (DriversDictionary.Any() is false || _isDisposed)
        {
            return;
        }

        _isDisposed = true;
        var threadPorts = _currentPorts.Values.Where(p => p > -1);
        var portDriverPairList = DriversDictionary
            .Where(p => threadPorts.Contains(p.Key));
        foreach (var portDriverPair in portDriverPairList)
        {
            if (seleniumGeniusOptions.DisposeDriverByDi)
            {
                portDriverPair.Value.Driver.Dispose();

                if (seleniumGeniusOptions.ClearCacheAfterDispose)
                {
                    var userDataDir = GenerateUserDataDir(portDriverPair.Key);
                    if (Directory.Exists(userDataDir))
                    {
                        Directory.Delete(userDataDir, true);
                    }
                }
            }

            await seleniumGeniusOptions.AvailablePorts.Writer.WriteAsync(portDriverPair.Key);
        }
    }
}