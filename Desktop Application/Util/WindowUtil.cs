using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace BaldursGateInworld.Util
{
    internal class WindowUtil
    {

        [StructLayout(LayoutKind.Sequential)]
        public struct Dimension
        {
            public int LeftTopX;
            public int LeftTopY;
            public int Width;
            public int Height;
        }

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool GetWindowRect(IntPtr hWnd, ref Dimension Rect);

        public static Dimension GetWindowDimensions(Process? process)
        {
            Dimension dim = new();
            try
            {
                if (process == null) return dim;

                var handle = process.MainWindowHandle;
                GetWindowRect(handle, ref dim);
                return dim;
            }
            catch
            {
                return dim;
            }
        }

        /// <summary>Returns true if the current application has focus, false otherwise</summary>
        public static bool ApplicationIsActivated(Process proc)
        {
            if (proc == null) return false;
            var activatedHandle = GetForegroundWindow();
            if (activatedHandle == IntPtr.Zero)
            {
                return false;       // No window is currently activated
            }

            var procId = proc.Id;
            int activeProcId;
            GetWindowThreadProcessId(activatedHandle, out activeProcId);
            return activeProcId == procId;
        }


        [DllImport("user32.dll", CharSet = CharSet.Auto, ExactSpelling = true)]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern int GetWindowThreadProcessId(IntPtr handle, out int processId);

    }
}
