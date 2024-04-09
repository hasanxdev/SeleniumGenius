using System.Drawing;
using System.Net;
using System.Threading.Channels;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.Win32;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using SeleniumGenius.HttpClientMessageHandlers;
using SeleniumGenius.Models;

namespace SeleniumGenius;

public class SeleniumGeniusOptions : ChromeOptions
{
    public string? UserDataDir { get; private set; }
    public bool DisposeDriverByDi { get; private set; }
    public bool ClearCacheAfterDispose { get; private set; }
    public Channel<int> AvailablePorts { get; private set; }
    public const int StaticPort = 55555;
    public Func<LoginRequest, LoginResult>? Login { get; private set; }
    public new List<string> Extensions { get; private set; } = new();
    public List<LoginData> LoginDataList { get; set; } = new();


    public SeleniumGeniusOptions() : base()
    {
        AvailablePorts = Channel.CreateBounded<int>(new BoundedChannelOptions(1)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = true
        });
        AvailablePorts.Writer.TryWrite(StaticPort);
    }

    public SeleniumGeniusOptions WithDimensions(Size size, Point location)
    {
        AddArgument($"--window-size={size.Width},{size.Height}");
        AddArgument($"--window-position={location.X},{location.Y}");
        return this;
    }

    public SeleniumGeniusOptions WithRelativeCachePath(IWebHostEnvironment env, string relativeCachePath)
    {
        UserDataDir = Path.Combine(env.ContentRootPath, "ChromeDriver", relativeCachePath);
        return this;
    }

    public SeleniumGeniusOptions WithLogin(Func<LoginRequest, LoginResult> login)
    {
        if (LoginDataList.Any() is false)
        {
            throw new Exception("please first use " + nameof(WithLoginData));
        }
        
        Login = login;
        return this;
    }

    public IHttpClientBuilder AddHttpClientIntegratedWithSeleniumCookies(WebApplicationBuilder bulder, string name)
    {
        var httpClient = bulder.Services.AddHttpClient(name);
        httpClient.ConfigurePrimaryHttpMessageHandler(() =>
        {
            if (bulder.Environment.IsDevelopment())
            {
                var internetSettings = Registry.CurrentUser
                    .OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Internet Settings")!;
                var proxyEnabled = internetSettings?.GetValue("ProxyEnable") as int?;
                if (proxyEnabled is not 0)
                {
                    // Get the proxy server address and port number
                    string proxyServer = (string)internetSettings.GetValue("ProxyServer")!;
                    return new HttpClientHandler
                    {
                        Proxy = new WebProxy($"http://{proxyServer}"),
                        ServerCertificateCustomValidationCallback = (_,_,_,_) => true
                    };
                }   
            }

            return new HttpClientHandler()
            {
                ServerCertificateCustomValidationCallback = (_,_,_,_) => true,
            };
        });
        
        httpClient.AddHttpMessageHandler<SeleniumGeniusAddCookiesHttpMessageHandler>();
        bulder.Services.AddScoped<SeleniumGeniusAddCookiesHttpMessageHandler>();

        if (bulder.Services.Any(x => x.ServiceType == typeof(IConfigureOptions<GeniusHttpMessageHandlerConfiguration>)) is
            false)
        {
            bulder.Services.Configure<GeniusHttpMessageHandlerConfiguration>(s => { s.ValidCookiesDomain = new(); });
        }

        return httpClient;
    }

    /// <summary>
    /// Sample: http://localhost:80
    /// </summary>
    public SeleniumGeniusOptions WithHttpProxyServer(string httpProxyServer)
    {
        AddArgument("--proxy-server=" + httpProxyServer);
        return this;
    }

    public SeleniumGeniusOptions WithIncognito()
    {
        AddArgument("--incognito");
        return this;
    }

    public SeleniumGeniusOptions WithDisableImages()
    {
        AddArgument("--blink-settings=imagesEnabled=false");
        return this;
    }

    public SeleniumGeniusOptions WithSupportDocker()
    {
        WithHeadless();
        AddArguments("disable-infobars"); // disabling infobars
        AddArguments("--disable-gpu"); // applicable to windows os only
        AddArguments("--disable-dev-shm-usage"); // overcome limited resource problems
        AddArguments("--no-sandbox"); // Bypass OS security model
        return this;
    }

    public SeleniumGeniusOptions WithStartMaximized()
    {
        AddArgument("--start-maximized");
        return this;
    }

    public SeleniumGeniusOptions WithExtensions(IWebHostEnvironment env, params string[] relativePathExtensions)
    {
        foreach (var relativePathExtension in relativePathExtensions)
        {
            Extensions.Add(Path.Combine(env.ContentRootPath, relativePathExtension));
        }

        return this;
    }

    public SeleniumGeniusOptions WithDisablePopupBlocking()
    {
        AddExcludedArgument("disable-popup-blocking");
        return this;
    }

    public SeleniumGeniusOptions WithHeadless()
    {
        AddArguments("--headless=new");
        AddArgument("--user-agent=Mozilla/5.0 (iPad; CPU OS 6_0 like Mac OS X) AppleWebKit/536.26 (KHTML, like Gecko) Version/6.0 Mobile/10A5355d Safari/8536.25");

        return this;
    }
    
    public void WithDisposeDriverAfterTaskCompleted()
    {
        DisposeDriverByDi = true;
    }
    
    public void WithClearCacheAfterDispose()
    {
        if (UserDataDir is null)
        {
            throw new Exception("please set cash path when you need to remove it");
        }
        
        ClearCacheAfterDispose = true;
    }
    
    public void WithMaxConcurrentDriver(int count)
    {
        AvailablePorts = Channel.CreateBounded<int>(new BoundedChannelOptions(count)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = true
        });
        
        foreach (var ports in Enumerable.Range(StaticPort, StaticPort + (count - 1)))
        {
            AvailablePorts.Writer.TryWrite(ports);
        }
    }
    
    public void WithLoginData(List<LoginData> loginData)
    {
        LoginDataList = loginData;
    }
    
    public enum BrowserVersions
    {
        /// <summary>
        /// Current CfT version
        /// </summary>
        Stable,

        /// <summary>
        /// Next version to stable.
        /// </summary>
        Beta,

        /// <summary>
        /// Version in development at this moment.
        /// </summary>
        Dev,

        // Nightly build for developers
        Canary,

        /// <summary>
        /// Extended Support Release (only for Firefox).
        /// </summary>
        Esr,
    }
}