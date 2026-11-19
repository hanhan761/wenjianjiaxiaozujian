using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace CsConsoleWidgetProvider
{
    internal static class CloseExplorerHotkey
    {
        private const int HotkeyId = 1;
        private const uint Modifiers = 0x4000 | 0x0001 | 0x0002 | 0x0004; // No repeat, Alt, Ctrl, Shift
        private const uint KeyW = 0x57;
        private const uint WmHotkey = 0x0312;
        private const uint WmClose = 0x0010;
        private const uint OwnerWindow = 4;
        private const string ReadyMutex = @"Local\FolderCardsWidget.CloseExplorerHotkey";
        private static readonly string ExplorerPath =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe");

        internal static void Run()
        {
            if (!RegisterHotKey(0, HotkeyId, Modifiers, KeyW))
                return;

            using var ready = new Mutex(false, ReadyMutex);
            try
            {
                while (GetMessage(out var message, 0, 0, 0) > 0)
                {
                    if (message.Id == WmHotkey && message.WParam == HotkeyId)
                        foreach (nint window in FindWindows())
                            if (IsFolderWindow(window))
                                PostMessage(window, WmClose, 0, 0);
                }
            }
            finally
            {
                UnregisterHotKey(0, HotkeyId);
            }
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

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeMessage
        {
            private nint window;
            internal uint Id;
            internal nuint WParam;
            private nint lParam;
            private uint time;
            private int x, y;
            private uint reserved;
        }

        private delegate bool WindowCallback(nint window, nint parameter);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterHotKey(nint window, int id, uint modifiers, uint virtualKey);
        [DllImport("user32.dll")] private static extern bool UnregisterHotKey(nint window, int id);
        [DllImport("user32.dll", EntryPoint = "GetMessageW")]
        private static extern int GetMessage(out NativeMessage message, nint window, uint first, uint last);
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
