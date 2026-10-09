using System;
using System.IO;
using PDFWin.Services;
using Xunit;

namespace PDFWin.Tests;

public class StartupServiceTests
{
    [Fact]
    public void StartupService_RegisterAndUnregister_Succeeds()
    {
        string dummyExe = Path.Combine(Path.GetTempPath(), "dummy_pdfwin.exe");
        File.WriteAllText(dummyExe, "dummy");

        try
        {
            // Register
            bool registered = StartupService.RegisterStartup(dummyExe);
            Assert.True(registered);
            Assert.True(StartupService.IsStartupEnabled());

            // Unregister
            bool unregistered = StartupService.UnregisterStartup();
            Assert.True(unregistered);
            Assert.False(StartupService.IsStartupEnabled());
        }
        finally
        {
            try { File.Delete(dummyExe); } catch { }
            try { StartupService.UnregisterStartup(); } catch { }
        }
    }
}
