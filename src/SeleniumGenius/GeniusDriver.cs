using System.Collections.ObjectModel;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;

namespace SeleniumGenius;

public class GeniusDriver : IWebDriver, ITakesScreenshot, IJavaScriptExecutor
{
    private readonly ChromeDriverService _service;
    private readonly ChromeOptions _options;
    private readonly TimeSpan _commandTimeout;
    
    /// <summary>
    /// This not thread safe
    /// </summary>
    public ChromeDriver Browser;
    
    public string Url
    {
        get => Browser.Url;
        set => Browser.Url = value;
    }

    public string Title => Browser.Title;
    public string PageSource => Browser.PageSource;
    public string CurrentWindowHandle => Browser.CurrentWindowHandle;
    public ReadOnlyCollection<string> WindowHandles => Browser.WindowHandles;

    public GeniusDriver(ChromeDriverService service, ChromeOptions options, TimeSpan commandTimeout)
    {
        _service = service;
        _options = options;
        _commandTimeout = commandTimeout;
        Launch();
    }

    private void Launch()
    {
        Browser = new ChromeDriver(_service, _options, _commandTimeout);
    }

    public void ReLaunch()
    {
        Browser.Dispose();
        Launch();
    }

    public void ReCreate()
    {
        Browser.Dispose();
        if (_options.Arguments.SingleOrDefault(p => p.Contains("user-data-dir=")) is var cachePath &&
            string.IsNullOrWhiteSpace(cachePath) is false)
        {
            Directory.Delete(cachePath.Split('=').Last(), true);
        }

        Launch();
    }

    public void Close() => Browser.Close();
    public void Quit() => Browser.Quit();
    public IOptions Manage() => Browser.Manage();
    public INavigation Navigate() => Browser.Navigate();
    public ITargetLocator SwitchTo() => Browser.SwitchTo();
    public IWebElement FindElement(By by) => Browser.FindElement(by);
    public ReadOnlyCollection<IWebElement> FindElements(By by) => Browser.FindElements(by);

    public void Dispose()
    {
        _service.Dispose();
        Browser.Dispose();
    }

    public IReadOnlyCollection<Cookie> GetCookiesContainsDomain(IEnumerable<string> domains)
    {
        return Browser.Manage().Cookies.AllCookies
            .Where(p => domains.Any(domain => p.Domain.Contains(domain)))
            .ToList();
    }
    
    public IReadOnlyCollection<Cookie> GetAllCookies()
    {
        return Browser.Manage().Cookies.AllCookies;
    }

    public Screenshot GetScreenshot() => Browser.GetScreenshot();
    public object ExecuteScript(string script, params object[] args) => Browser.ExecuteScript(script, args);

    public object ExecuteScript(PinnedScript script, params object[] args) => Browser.ExecuteScript(script, args);

    public object ExecuteAsyncScript(string script, params object[] args) => Browser.ExecuteAsyncScript(script, args);
}