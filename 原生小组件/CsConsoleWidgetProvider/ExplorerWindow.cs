using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using FolderLauncher;
using Microsoft.CSharp.RuntimeBinder;

namespace CsConsoleWidgetProvider
{
    internal static class ExplorerWindow
    {
        private const int Restore = 9;
        private const int ExtendedStyle = -20;
        private const long TopmostStyle = 8;
        private const uint PositionFlags = 0x0010 | 0x0002 | 0x0001 | 0x0040;
        private static readonly nint Topmost = -1;
        private static readonly nint NotTopmost = -2;

        internal static void OpenFolder(string path)
        {
            WidgetTrace.Write("Explorer open requested");
            var existing = FindFolderWindows(path);
            using (Process.Start(new ProcessStartInfo(path)
            {
                UseShellExecute = true,
                Verb = "open",
                WindowStyle = ProcessWindowStyle.Normal
            })) { }

            var timer = Stopwatch.StartNew();
            bool matchedWindow = false;
            while (timer.ElapsedMilliseconds < 2400)
            {
                var matching = FindFolderWindows(path);
                nint foreground = GetForegroundWindow();
                if (matching.Contains(foreground))
                {
                    WidgetTrace.Write("Explorer reached foreground");
                    return;
                }

                // Prefer the window created by the shell. Allow time for Explorer to
                // register it before falling back to an already open matching window.
                nint target = matching.FirstOrDefault(window => !existing.Contains(window));
                if (target == 0 && timer.ElapsedMilliseconds >= 650)
                    target = matching.FirstOrDefault();

                if (target != 0)
                {
                    matchedWindow = true;
                    if (IsIconic(target))
                        ShowWindowAsync(target, Restore);
                    if (Activate(target, false) ||
                        (timer.ElapsedMilliseconds >= 400 && Activate(target, true)))
                    {
                        WidgetTrace.Write("Explorer reached foreground");
                        return;
                    }
                }
                Thread.Sleep(70);
            }
            WidgetTrace.Write(matchedWindow
                ? "Explorer window matched; foreground activation unavailable"
                : "Explorer opened; no window registered for the target folder");
        }

        private static bool Activate(nint target, bool raiseWindow)
        {
            nint foregroundWindow = GetForegroundWindow();

            if (TryFocus(target, foregroundWindow))
                return true;
            if (!raiseWindow)
                return false;

            // Raise only the matching Explorer window. Restore its normal Z order
            // immediately, including windows that were already configured as topmost.
            bool alreadyTopmost = (GetWindowLongPtr(target, ExtendedStyle).ToInt64() & TopmostStyle) != 0;
            bool lifted = !alreadyTopmost && SetWindowPos(target, Topmost, 0, 0, 0, 0, PositionFlags);
            try
            {
                foregroundWindow = GetForegroundWindow();
                return TryFocus(target, foregroundWindow);
            }
            finally
            {
                if (lifted)
                    SetWindowPos(target, NotTopmost, 0, 0, 0, 0, PositionFlags);
            }
        }

        private static bool TryFocus(nint target, nint foregroundWindow)
        {
            SetForegroundWindow(target);
            if (GetForegroundWindow() == target)
                return true;

            uint current = GetCurrentThreadId();
            uint foreground = GetWindowThreadProcessId(foregroundWindow, out _);
            // Worker threads do not have an input queue until they call USER.
            PeekMessage(out _, 0, 0, 0, 0);
            if (foreground == 0 || foreground == current || !AttachThreadInput(current, foreground, true))
                return false;

            try
            {
                if (GetForegroundWindow() == foregroundWindow)
                {
                    BringWindowToTop(target);
                    SetForegroundWindow(target);
                }
                return GetForegroundWindow() == target;
            }
            finally
            {
                AttachThreadInput(current, foreground, false);
            }
        }

        private static HashSet<nint> FindFolderWindows(string path)
        {
            var match = new HashSet<nint>();
            object shell = null;
            object list = null;
            try
            {
                var type = Type.GetTypeFromProgID("Shell.Application");
                if (type == null)
                    return match;
                shell = Activator.CreateInstance(type);
                list = ((dynamic)shell).Windows();
                int count = ((dynamic)list).Count;
                for (int index = 0; index < count; index++)
                {
                    object entry = null;
                    try
                    {
                        entry = ((dynamic)list).Item(index);
                        if (entry == null)
                            continue;
                        dynamic view = entry;
                        nint window = (nint)(long)view.HWND;
                        var name = new StringBuilder(32);
                        GetClassName(window, name, name.Capacity);
                        if (name.ToString() != "CabinetWClass")
                            continue;

                        var url = (string)view.LocationURL;
                        if (Uri.TryCreate(url, UriKind.Absolute, out var location) && location.IsFile &&
                            Library.SamePath(Library.Normalize(location.LocalPath), path))
                            match.Add(window);
                    }
                    catch (Exception e) when (e is COMException || e is RuntimeBinderException ||
                                              e is ArgumentException || e is System.IO.IOException || e is NotSupportedException) { }
                    finally { Release(entry); }
                }
            }
            catch (Exception e) when (e is COMException || e is RuntimeBinderException) { }
            finally
            {
                Release(list);
                Release(shell);
            }
            return match;
        }

        private static void Release(object instance)
        {
            if (instance != null && Marshal.IsComObject(instance))
                Marshal.ReleaseComObject(instance);
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeMessage
        {
            private nint window;
            private uint message;
            private nuint wParam;
            private nint lParam;
            private uint time;
            private int x, y;
            private uint reserved;
        }

        [DllImport("user32.dll")] private static extern nint GetForegroundWindow();
        [DllImport("user32.dll")] private static extern bool SetForegroundWindow(nint window);
        [DllImport("user32.dll")] private static extern bool ShowWindowAsync(nint window, int command);
        [DllImport("user32.dll")] private static extern bool IsIconic(nint window);
        [DllImport("user32.dll")] private static extern bool BringWindowToTop(nint window);
        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] private static extern nint GetWindowLongPtr(nint window, int index);
        [DllImport("user32.dll")] private static extern bool SetWindowPos(nint window, nint after, int x, int y, int width, int height, uint flags);
        [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(nint window, out uint process);
        [DllImport("user32.dll")] private static extern bool AttachThreadInput(uint thread, uint target, bool attach);
        [DllImport("user32.dll", EntryPoint = "PeekMessageW")]
        private static extern bool PeekMessage(out NativeMessage message, nint window, uint min, uint max, uint remove);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetClassName(nint window, StringBuilder name, int capacity);
        [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
    }
}
