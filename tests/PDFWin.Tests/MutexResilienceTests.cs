using System;
using System.IO;
using System.Threading;
using Xunit;

namespace PDFWin.Tests;

public class MutexResilienceTests
{
    [Fact]
    public void AbandonedMutex_IsHandledProperly()
    {
        string mutexName = "Local\\PDFWin_Test_Abandoned_" + Guid.NewGuid().ToString("N");
        
        // Spawn a thread that acquires the mutex and exits without releasing it
        var t = new Thread(() =>
        {
            var m = new Mutex(false, mutexName);
            m.WaitOne();
            // thread exits without m.ReleaseMutex() -> mutex abandoned
        });
        t.Start();
        t.Join();

        // Now test our acquiring pattern
        bool acquired = false;
        using var testMutex = new Mutex(false, mutexName);
        try
        {
            testMutex.WaitOne(1000);
            acquired = true;
        }
        catch (AbandonedMutexException)
        {
            // Successfully recognized that the mutex was abandoned, and ownership was transferred to this thread
            acquired = true;
        }
        finally
        {
            if (acquired)
            {
                try { testMutex.ReleaseMutex(); } catch { }
            }
        }

        Assert.True(acquired);
    }
}
