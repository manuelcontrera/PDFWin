using System;
using System.Diagnostics;
using System.IO;
using Microsoft.Windows.AppLifecycle;
using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;

namespace PDFWin.Services;

public static class ToastNotificationService
{
    private static bool _isRegistered = false;

    public static void Initialize()
    {
        try
        {
            if (!_isRegistered)
            {
                AppNotificationManager.Default.NotificationInvoked += OnNotificationInvoked;
                AppNotificationManager.Default.Register();
                _isRegistered = true;
            }
        }
        catch
        {
            // Ignore if notifications cannot be registered in certain test environments
        }
    }

    public static bool HandleStartupNotification()
    {
        try
        {
            var appArgs = AppInstance.GetCurrent().GetActivatedEventArgs();
            if (appArgs != null && appArgs.Kind == ExtendedActivationKind.AppNotification)
            {
                if (appArgs.Data is AppNotificationActivatedEventArgs notifArgs)
                {
                    OnNotificationInvoked(AppNotificationManager.Default, notifArgs);
                    return true;
                }
            }
        }
        catch
        {
            // Fallback
        }
        return false;
    }

    private static void OnNotificationInvoked(AppNotificationManager sender, AppNotificationActivatedEventArgs args)
    {
        if (args.Arguments.TryGetValue("path", out string? path) && !string.IsNullOrWhiteSpace(path))
        {
            if (File.Exists(path) || Directory.Exists(path))
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = path,
                        UseShellExecute = true
                    });
                }
                catch
                {
                    // Fallback
                }
            }
        }
    }

    public static void ShowPdfCreated(string pdfPath)
    {
        try
        {
            Initialize();

            var notification = new AppNotificationBuilder()
                .AddText("PDFWin - PDF Created")
                .AddText(Path.GetFileName(pdfPath))
                .AddButton(new AppNotificationButton("Open PDF")
                    .AddArgument("action", "open")
                    .AddArgument("path", pdfPath))
                .AddButton(new AppNotificationButton("Open Folder")
                    .AddArgument("action", "folder")
                    .AddArgument("path", Path.GetDirectoryName(pdfPath) ?? ""))
                .BuildNotification();

            AppNotificationManager.Default.Show(notification);
        }
        catch
        {
            // If Windows App Notifications are disabled or unavailable in unpackaged context, fallback
        }
    }
}
