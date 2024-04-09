using OpenQA.Selenium;

namespace SeleniumGenius.Exceptions;

public class UnhandledGeniusException(Screenshot screenshot, Uri uri, Exception innerException) : Exception(innerException.Message, innerException)
{
    public Screenshot Screenshot { get; set; } = screenshot;
    public Uri Uri { get; set; } = uri;
}