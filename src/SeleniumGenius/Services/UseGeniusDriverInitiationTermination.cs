using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Management;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace SeleniumGenius.Services;

public static class GeniusDriverTermination
{
    [SuppressMessage("Interoperability", "CA1416:Validate platform compatibility")]
    public static void Terminate()
    {
        if (Environment.OSVersion.Platform is not
            (PlatformID.Win32S or PlatformID.Win32Windows or PlatformID.Win32NT or PlatformID.WinCE))
        {
            throw new NotSupportedException("termination just supported on Windows platforms");
        }

        Process[] processes = Process.GetProcessesByName("chrome");
        for (int p = 0; p < processes.Length; p++)
        {
            ManagementObjectSearcher commandLineSearcher =
                new("SELECT CommandLine FROM Win32_Process WHERE ProcessId = " + processes[p].Id);
            
            string commandLine = "";
            foreach (var commandLineObject in commandLineSearcher.Get())
            {
                commandLine += (string)commandLineObject["CommandLine"];
            }

            if (commandLine.Contains("test-type=webdriver"))
            {
                // ParentProcessId
                int parentPid = 0;
                using (ManagementObject mo = new ManagementObject("win32_process.handle='" + processes[p].Id + "'"))
                {
                    mo.Get();
                    parentPid = Convert.ToInt32(mo["ParentProcessId"]);
                }

                Process.GetProcessById(processes[p].Id).Kill();
                try
                {
                    Process.GetProcessById(parentPid).Kill();
                }
                catch
                {
                    // ignored
                }
            }
        }
    }
}