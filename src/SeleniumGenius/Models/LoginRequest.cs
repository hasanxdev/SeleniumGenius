using OpenQA.Selenium.Chrome;

namespace SeleniumGenius.Models;

public class LoginRequest
{
    public required ChromeDriver Driver { get; set; }
    public required LoginData LoginData { get; set; }
}

public class LoginData
{
    public required string Username { get; set; }
    public required string Email { get; set; }
    public required string Password { get; set; }
}