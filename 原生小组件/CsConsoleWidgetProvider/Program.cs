// Derived from the Microsoft Windows App SDK Widgets sample (MIT).
using Microsoft.Windows.Widgets.Providers;
using System;
using System.Runtime.InteropServices;
using WidgetHelper;
using Windows.Storage;
using System.IO;

namespace CsConsoleWidgetProvider
{
    public static class Program
    {
        [MTAThread]
        static void Main(string[] args)
        {
            if (args.Length == 0)
                return;

            if (args[0] == "--close-explorer-hotkey")
            {
                CloseExplorerHotkey.Run();
                return;
            }

            try
            {
                WinRT.ComWrappersSupport.InitializeComWrappers();
                if (args[0] == "-RegisterProcessAsComServer")
                {
                    using var manager = RegistrationManager<WidgetProvider>.RegisterProvider();
                    WidgetTrace.Write("COM registered");
                    manager.GetDisposedEvent().WaitOne();
                }
                else if (args[0] == "--refresh")
                {
                    var manager = WidgetManager.GetDefault();
                    var widgets = manager.GetWidgetInfos();
                    WidgetTrace.Write("Refresh found " + widgets.Length + " pinned instances");
                    foreach (var entry in widgets)
                    {
                        var context = entry.WidgetContext;
                        if (context.DefinitionId == FolderWidget.DefinitionId)
                            new FolderWidget(context.Id, entry.CustomState).Refresh(context.Size, "manual refresh");
                    }
                }
            }
            catch (Exception ex)
            {
                WidgetTrace.Failure("Startup", ex);
            }
        }
    }

    internal static class WidgetTrace
    {
        private static readonly object Sync = new();

        internal static void Failure(string operation, Exception e) =>
            Write($"{operation} failed with {e.GetType().Name} 0x{e.HResult:X8}");

        internal static void Write(string status)
        {
            try
            {
                lock (Sync)
                {
                    string file = Path.Combine(ApplicationData.Current.LocalFolder.Path, "widget.log");
                    if (File.Exists(file) && new FileInfo(file).Length > 16384)
                        File.WriteAllText(file, "");
                    File.AppendAllText(file, $"{DateTimeOffset.Now:O} {status}{Environment.NewLine}");
                }
            }
            catch (Exception) { }
        }
    }
}
