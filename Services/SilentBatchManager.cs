using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace PDFWin.Services;

/// <summary>
/// Aggregates multiple file invocations from Windows Explorer context menu into a single batch,
/// ensuring that selecting multiple images produces exactly ONE combined PDF rather than individual PDFs.
/// Resilient against AbandonedMutexException and concurrent burst requests.
/// </summary>
public static class SilentBatchManager
{
    private const string QueueMutexName = "Local\\PDFWin_Batch_Queue_Lock";
    private const string CoordinatorMutexName = "Local\\PDFWin_Batch_Coordinator_Lock";

    public static void EnqueueAndProcess(IEnumerable<string> filePaths)
    {
        var validFiles = filePaths.Where(File.Exists).Where(PdfService.IsSupported).ToList();
        if (validFiles.Count == 0) return;

        string queueDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PDFWin", "Queue");
        Directory.CreateDirectory(queueDir);
        string queueFile = Path.Combine(queueDir, "batch_items.txt");

        // 1. Add our file(s) to the shared queue file under the Queue Mutex
        using (var qMutex = new Mutex(false, QueueMutexName))
        {
            bool hasLock = false;
            try
            {
                try
                {
                    hasLock = qMutex.WaitOne(3000);
                }
                catch (AbandonedMutexException)
                {
                    hasLock = true;
                }

                if (hasLock)
                {
                    File.AppendAllLines(queueFile, validFiles);
                }
            }
            finally
            {
                if (hasLock)
                {
                    try { qMutex.ReleaseMutex(); } catch { }
                }
            }
        }

        // 2. Try to become the Coordinator for this batch
        bool isCoordinator = false;
        Mutex? coordMutex = null;
        try
        {
            coordMutex = new Mutex(false, CoordinatorMutexName);
            try
            {
                // Non-blocking try: if already held by another instance within 20ms, we are a worker
                isCoordinator = coordMutex.WaitOne(20);
            }
            catch (AbandonedMutexException)
            {
                isCoordinator = true;
            }
        }
        catch
        {
            isCoordinator = false;
        }

        if (!isCoordinator)
        {
            // Another instance is already coordinating the batch!
            coordMutex?.Dispose();
            return;
        }

        // 3. We ARE the coordinator. Drain queue in a loop to handle any batches queued during PDF generation.
        try
        {
            while (true)
            {
                int lastCount = 0;
                int stableTicks = 0;
                DateTime startTime = DateTime.Now;

                // Wait until the number of queued items stays constant for ~240ms (or max 2.5 seconds)
                while ((DateTime.Now - startTime).TotalMilliseconds < 2500)
                {
                    Thread.Sleep(80);

                    int currentCount = 0;
                    using (var qMutex = new Mutex(false, QueueMutexName))
                    {
                        bool hasLock = false;
                        try
                        {
                            try { hasLock = qMutex.WaitOne(1000); } catch (AbandonedMutexException) { hasLock = true; }
                            if (hasLock && File.Exists(queueFile))
                            {
                                currentCount = File.ReadAllLines(queueFile).Length;
                            }
                        }
                        finally
                        {
                            if (hasLock) try { qMutex.ReleaseMutex(); } catch { }
                        }
                    }

                    if (currentCount == lastCount && currentCount > 0)
                    {
                        stableTicks++;
                        if (stableTicks >= 3) // ~240ms of stability -> Explorer finished spawning
                        {
                            break;
                        }
                    }
                    else
                    {
                        stableTicks = 0;
                        lastCount = currentCount;
                    }
                }

                // 4. Retrieve all collected files from the queue
                List<string> collected = new();
                using (var qMutex = new Mutex(false, QueueMutexName))
                {
                    bool hasLock = false;
                    try
                    {
                        try { hasLock = qMutex.WaitOne(1000); } catch (AbandonedMutexException) { hasLock = true; }
                        if (hasLock && File.Exists(queueFile))
                        {
                            collected = File.ReadAllLines(queueFile)
                                .Where(line => !string.IsNullOrWhiteSpace(line))
                                .Distinct(StringComparer.OrdinalIgnoreCase)
                                .Where(File.Exists)
                                .Where(PdfService.IsSupported)
                                .ToList();

                            File.Delete(queueFile);
                        }
                    }
                    finally
                    {
                        if (hasLock) try { qMutex.ReleaseMutex(); } catch { }
                    }
                }

                if (collected.Count == 0)
                {
                    break;
                }

                // 5. Group collected files by parent folder
                var groupedByFolder = collected.GroupBy(f => Path.GetDirectoryName(f) ?? "", StringComparer.OrdinalIgnoreCase);

                foreach (var group in groupedByFolder)
                {
                    var folderFiles = group.ToList();
                    var sorted = PdfService.SortNaturally(folderFiles);

                    // Create exactly 1 combined PDF for this group
                    string customName = sorted.Count > 1 ? "Documento_Combinado.pdf" : $"{Path.GetFileNameWithoutExtension(sorted[0])}.pdf";
                    string outputPath = PdfService.CreatePdfAsync(sorted, outputDirectory: group.Key, customFileName: customName)
                        .GetAwaiter().GetResult();

                    ToastNotificationService.ShowPdfCreated(outputPath);
                }

                // Check if new items were queued while we were processing this batch
                bool hasMoreItems = false;
                using (var qMutex = new Mutex(false, QueueMutexName))
                {
                    bool hasLock = false;
                    try
                    {
                        try { hasLock = qMutex.WaitOne(500); } catch (AbandonedMutexException) { hasLock = true; }
                        if (hasLock && File.Exists(queueFile) && new FileInfo(queueFile).Length > 0)
                        {
                            hasMoreItems = true;
                        }
                    }
                    finally
                    {
                        if (hasLock) try { qMutex.ReleaseMutex(); } catch { }
                    }
                }

                if (!hasMoreItems)
                {
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            try
            {
                string logPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PDFWin", "error.log");
                File.AppendAllText(logPath, $"[{DateTime.Now}] Batch coordinator error: {ex}\n");
            }
            catch { }
        }
        finally
        {
            try { coordMutex?.ReleaseMutex(); } catch { }
            coordMutex?.Dispose();
        }
    }
}
