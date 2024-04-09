namespace SeleniumGenius.Models;

public class GeniusCreateResult
{
    public bool Success { get; set; }
    public  string Message { get; set; }
    public  GeniusDriver Driver { get; set; }
}