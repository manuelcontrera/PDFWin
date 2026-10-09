using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using PDFWin.Services;

namespace PDFWin;

public static class Program
{
    private const string AppMutexName = "Local\\PDFWin_SingleInstance_Mutex_Manal";

    [STAThread]
    public static void Main(string[] args)
    {
        try
        {
            string debugDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PDFWin");
            Directory.CreateDirectory(debugDir);
            File.AppendAllText(Path.Combine(debugDir, "debug.log"), $"{DateTime.Now}: Main invoked with {args.Length} args: [{string.Join(", ", args)}]\n");
        }
        catch { }

        // 0. Check for Toast Notification activation (unpackaged mode)
        if (ToastNotificationService.HandleStartupNotification())
        {
            return;
        }

        // 1. Check for command-line options
        if (args.Length > 0)
        {
            if (HandleCommandLineArgs(args))
            {
                return;
            }
        }

        // 2. Single-instance check for GUI mode
        using var mutex = new System.Threading.Mutex(true, AppMutexName, out bool isNewInstance);
        if (!isNewInstance)
        {
            // Another instance is already running; signal it to show and focus
            IntPtr hwnd = Native.Win32Helper.FindWindow(null, "PDFWin");
            if (hwnd != IntPtr.Zero)
            {
                uint msg = Native.Win32Helper.RegisterWindowMessage("PDFWin_Show_Command");
                Native.Win32Helper.PostMessage(hwnd, msg, IntPtr.Zero, IntPtr.Zero);
                Native.Win32Helper.SetForegroundWindow(hwnd);
            }
            return;
        }

        // 3. Launch normal WinUI 3 Application
        WinRT.ComWrappersSupport.InitializeComWrappers();
        Microsoft.UI.Xaml.Application.Start((p) =>
        {
            var context = new Microsoft.UI.Dispatching.DispatcherQueueSynchronizationContext(
                Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread());
            System.Threading.SynchronizationContext.SetSynchronizationContext(context);
            _ = new App();
        });
    }

    private static bool HandleCommandLineArgs(string[] args)
    {
        string firstArg = args[0].ToLowerInvariant();

        if (firstArg == "--register-menu")
        {
            string exePath = Environment.ProcessPath ?? "";
            ContextMenuService.RegisterContextMenu(exePath);
            return true;
        }

        if (firstArg == "--unregister-menu")
        {
            ContextMenuService.UnregisterContextMenu();
            return true;
        }

        if (firstArg == "--folder" && args.Length > 1)
        {
            string folderPath = args[1].Trim().Trim('"').TrimEnd('\\');
            if (Directory.Exists(folderPath))
            {
                var files = Directory.GetFiles(folderPath)
                    .Where(PdfService.IsSupported)
                    .ToList();

                if (files.Count > 0)
                {
                    SilentBatchManager.EnqueueAndProcess(files);
                    return true;
                }
            }
        }

        if (firstArg == "--silent-create" || firstArg == "-s")
        {
            var files = args.Skip(1).Where(File.Exists).Where(PdfService.IsSupported).ToList();
            if (files.Count > 0)
            {
                SilentBatchManager.EnqueueAndProcess(files);
                return true;
            }
        }

        // Check if raw files were passed via Explorer "Open with..." or multi-select
        var rawFiles = args.Where(File.Exists).Where(PdfService.IsSupported).ToList();
        if (rawFiles.Count > 0 && !args.Contains("--gui"))
        {
            SilentBatchManager.EnqueueAndProcess(rawFiles);
            return true;
        }

        return false;
    }

    private static void ExecuteSilentCreation(List<string> files)
    {
        try
        {
            Console.WriteLine($"Procesando {files.Count} archivo(s)...");
            var sorted = PdfService.SortNaturally(files);
            string outputPath = PdfService.CreatePdfAsync(sorted).GetAwaiter().GetResult();
            Console.WriteLine($"PDF creado exitosamente: {outputPath}");
            ToastNotificationService.ShowPdfCreated(outputPath);
        }
        catch (Exception ex)
        {
            string err = $"[PDFWin Error] {DateTime.Now}: {ex}\n";
            Console.Error.WriteLine(err);
            try
            {
                string logPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PDFWin", "error.log");
                Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
                File.AppendAllText(logPath, err);
            }
            catch { }
        }
    }
}
