# SeleniumGenius 🚀

SeleniumGenius is an expert tool for working with Selenium, providing access to the core Selenium settings along with the following features:

## What Problems Can I Solve? 🛠️

- 💾 Memory leakage issue after force-closing the application on its first run. 
- 📥 Automatic download of compatible driver versions for the browser.
- 🔐 Ensuring that your browser is always logged in.
- 🏁 Resolving concurrency issues with multiple threads sending commands to Chrome.
- ✨ Support from CancellationToken
- 🔀 Running multiple instances of the driver to utilize more processing power simultaneously without interference between different instances.
- 🧹 Optional disposal of the driver after completing tasks and clearing the browser cache after disposal.
- 🐋 Settings for Docker support.
- 🌐 Having a dedicated HttpClient for sending requests to the site using browser cookies.

# Using SeleniumGenius 📝
All of these features are abstracted behind a single method called `CreateAsync`. After adding the service, simply inject the `SeleniumGeniusFactory` class and use its `CreateAsync` method:

```csharp
_ = await seleniumGeniusFactory.CreateAsync(cancellationToken);
```

## Installation and Setup: [![Nuget](https://img.shields.io/nuget/v/SeleniumGenius)](https://www.nuget.org/packages/SeleniumGenius/)

#### Via PMC (Package manager console): 

```
PM> Install-Package SeleniumGenius
```
#### Via dotnet CLI
```
dotnet add package SeleniumGenius
```

Then add the following settings to your services:

```csharp
builder.AddSeleniumGenius(options =>
{
    options.WithLoginData(new List<LoginData>()
    {
        new()
        {
            Username = "admin",
            Password = "Admin@123",
            Email = "admin@admin.com"
        }
    });

    options.WithLogin(loginRequest =>
    {
        return new LoginResult(true, "successfully logged in");
    });
}, service =>
{
    service.EnableVerboseLogging = false;
    service.SuppressInitialDiagnosticInformation = true;
    service.HideCommandPromptWindow = true;
});
```

- Here, options represents ChromeOptions, and service represents ChromeDriverService.
Various sample settings are provided below, and we'll delve into each setting in detail.
- 📢 Also you can use `builder.AddSeleniumGeniusWithPreTermination()` this method just work on windows linux and mac not supported, this method within itself use static class `GeniusDriverTermination.Terminate()` this class force close chrome drivers and chrome browsers.
    - 💡Tips: Have a HostedService with name `StopGeniusDriversHostedService` that when application normally stopped try to kill all chrome drivers and chrome browsers 

# Options Settings ⚙️:  
01. `options.WithDimensions()`: Set the dimensions and position of your Chrome window. Note that it does not work in headless mode.  
02. `options.WithRelativeCachePath()`: Customize the cache path for Chrome files to reduce login frequency and retain cookies.  
03. `options.WithLogin()`: A delegate executed before each run to ensure login. It's advisable to include logic to check for login status.  
04. `options.AddHttpClientIntegratedWithSeleniumCookies()`: Provides an HttpClient that sends all site cookies in a header. It also configures a proxy for HttpClient when running the application on Windows during development.  
05. `options.WithHttpProxyServer()`: Set a proxy server for your Chrome instance.  
06. `options.WithIncognito()`: Run the browser in incognito mode.  
07. `options.WithDisableImages()`: Disable all images for faster website loading.  
08. `options.WithSupportDocker()`: Configures settings on Chrome for running inside Docker.  
09. `options.WithStartMaximized()`: Launch the browser in maximized mode, not supported in headless mode.  
10. `options.WithExtensions()`: Add your own extensions to Chrome.  
11. `options.WithDisablePopupBlocking()`: Block all popups, useful for sites with excessive ads.  
12. `options.WithHeadless()`: Run Chrome without UI. Note that websites may detect you as a bot based on the UserAgent.  
13. `options.WithDisposeDriverAfterTaskCompleted()`: Dispose of the driver and browser resources after completing each operation.  
14. `options.WithClearCacheAfterDispose()`: Clear the cache after disposing of resources.  
15. `options.WithMaxConcurrentDriver()`: Determine the number of Chrome drivers to run simultaneously.  
16. `options.WithLoginData()`: Provide user data for logging in.

# Errors:❗
1. `UnhandledGeniusException`: This class helps you throw exceptions with a screenshot of the moment the error occurred.

