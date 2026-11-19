using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace CsConsoleWidgetProvider
{
    internal static class ExplorerFolderWindows
    {
        private const uint WmClose = 0x0010;
        private const uint OwnerWindow = 4;
        private static readonly string ExplorerPath =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe");

        internal static int CloseAll()
        {
            int count = 0;
            foreach (nint window in FindWindows())
            {
                if (IsFolderWindow(window) && PostMessage(window, WmClose, 0, 0))
                    count++;
            }
            return count;
        }

        internal static List<nint> FindWindows()
        {
            var windows = new List<nint>();
            EnumWindows((window, _) =>
            {
                if (IsFolderWindow(window))
                    windows.Add(window);
                return true;
            }, 0);
            return windows;
        }

        private static bool IsFolderWindow(nint window)
        {
            if (!IsWindowVisible(window) || GetWindow(window, OwnerWindow) != 0)
                return false;
            var name = new StringBuilder(32);
            GetClassName(window, name, name.Capacity);
            if (name.ToString() != "CabinetWClass")
                return false;

            GetWindowThreadProcessId(window, out uint processId);
            if (processId == 0)
                return false;

            try
            {
                using var process = Process.GetProcessById((int)processId);
                return string.Equals(process.MainModule?.FileName, ExplorerPath, StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception e) when (e is ArgumentException || e is InvalidOperationException || e is Win32Exception)
            {
                return false;
            }
        }

        private delegate bool WindowCallback(nint window, nint parameter);

        [DllImport("user32.dll")] private static extern bool EnumWindows(WindowCallback callback, nint parameter);
        [DllImport("user32.dll")] private static extern bool IsWindowVisible(nint window);
        [DllImport("user32.dll")] private static extern nint GetWindow(nint window, uint command);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetClassName(nint window, StringBuilder name, int capacity);
        [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(nint window, out uint process);
        [DllImport("user32.dll", EntryPoint = "PostMessageW")]
        private static extern bool PostMessage(nint window, uint message, nuint wParam, nint lParam);
    }
}
