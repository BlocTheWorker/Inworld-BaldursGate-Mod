using BaldursGateInworld.Overlay;
using SharpDX.DirectInput;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;

namespace BaldursGateInworld.Util
{

    internal class InputManager
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct MPoint
        {
            public int X;
            public int Y;

            public static implicit operator Point(MPoint point)
            {
                return new Point(point.X, point.Y);
            }
        }
        public enum MouseMessages : int
        {
            WM_LBUTTONDOWN = 0x201, //Left mousebutton down
            WM_LBUTTONUP = 0x202,   //Left mousebutton up
            WM_LBUTTONDBLCLK = 0x203, //Left mousebutton doubleclick
            WM_RBUTTONDOWN = 0x204, //Right mousebutton down
            WM_RBUTTONUP = 0x205,   //Right mousebutton up
            WM_RBUTTONDBLCLK = 0x206, //Right mousebutton do
        }

        [DllImport("user32.dll")]
        public static extern bool GetCursorPos(out MPoint lpPoint);
        [DllImport("user32.dll", CharSet = CharSet.Auto, ExactSpelling = true)]
        public static extern void BlockInput([In, MarshalAs(UnmanagedType.Bool)] bool fBlockIt);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = System.Runtime.InteropServices.CharSet.Auto)]
        public static extern IntPtr GetModuleHandle(string lpModuleName);

        private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);
        private const int WH_MOUSE_LL = 14;
        private LowLevelMouseProc _proc;
        private IntPtr _hookID = IntPtr.Zero;
        private OverlayLogic _logic;
        private static InputManager instance = null;
        private Keyboard keyboard;
        private Mouse mouse;
        private KeyboardUpdate[] kdata;

        private InputManager()
        {
            // Initialize DirectInput
            var directInput = new DirectInput();
            keyboard = new Keyboard(directInput);
            keyboard.Properties.BufferSize = 128;
            keyboard.Acquire();

            mouse = new Mouse(directInput);
            mouse.Properties.BufferSize = 128;
            mouse.Acquire();
        }

        public void PollKeyData()
        {
            kdata = new KeyboardUpdate[0];
            if (keyboard == null) return;
            keyboard.Poll();
            kdata = keyboard.GetBufferedData();
        }

        public bool IsKeyDown(Key key)
        {
            foreach (var state in kdata)
            {
                if (state.Key == key && state.IsPressed)
                    return true;
            }

            return false;
        }

        public bool IsKeyHold(Key key)
        {
            if (keyboard == null) return false;
            keyboard.Poll();
            var datas = keyboard.GetBufferedData();
            foreach (var state in datas)
            {
                if (state.Key == key)
                    return true;
            }
            return false;
        }


        public static InputManager Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new InputManager();
                }
                return instance;
            }
        }

        public void HookMouse(OverlayLogic logic)
        {
            try
            {
                _proc = HookCallback;
                _logic = logic;
                using Process curProcess = Process.GetCurrentProcess();
                using ProcessModule curModule = curProcess.MainModule;
                _hookID = SetWindowsHookEx(WH_MOUSE_LL, _proc, GetModuleHandle(curModule.ModuleName), 0);
            }
            catch
            {

            }
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            // Check if the mouse button is down
            if (nCode >= 0 && MouseMessages.WM_LBUTTONDOWN == (MouseMessages)wParam)
            {
                if (_logic?.HandleClick() == false)
                {
                    return (IntPtr)1;
                }
            }
            return CallNextHookEx(_hookID, nCode, wParam, lParam);
        }


        // Get the absolute mouse position
        public Point GetMousePosition()
        {
            try
            {
                MPoint lpPoint;
                GetCursorPos(out lpPoint);
                return lpPoint;
            }
            catch
            {
                return new Point();
            }
        }

    }
}
