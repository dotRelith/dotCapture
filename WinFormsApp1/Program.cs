using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace WinFormsApp1
{
    static class Program
    {
        // Delegate for the LowLevelKeyboardProc hook
        private delegate IntPtr LowLevelKeyboardProce(int nCode, IntPtr wParam, IntPtr lParam);

        // Import the SetWindowsHookEx function from user32.dll
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProce lpfn, IntPtr hMod, uint dwThreadId);

        // Import the CallNextHookEx function from user32.dll
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        // Import the UnhookWindowsHookEx function from user32.dll
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        // The hook handle
        private static IntPtr hookHandle = IntPtr.Zero;
        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            hookHandle = SetWindowsHookEx(13, LowLevelKeyboardProc, IntPtr.Zero, 0);
            Application.SetHighDpiMode(HighDpiMode.SystemAware);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new ScreenCapture());
        }
        private static IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam)
        {
            // Check if the key press event was sent
            if (nCode == 0 && wParam == (IntPtr)0x0100)
            {
                // Get the key code from the lParam
                int keyCode = Marshal.ReadInt32(lParam);

                // Check if the key code is the Print Screen key
                if (keyCode == 0x2C)
                {
                    ScreenCapture.Instance.PressedCaptureScreen();
                }
            }
            // Call the next hook in the chain
            return CallNextHookEx(hookHandle, nCode, wParam, lParam);
        }
    }
}
