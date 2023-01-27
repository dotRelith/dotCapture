using Microsoft.Win32;
using System.Runtime.InteropServices;

namespace dotCaptureV2
{
    internal static class Program
    {
        private delegate IntPtr LowLevelKeyboardProce(int nCode, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProce lpfn, IntPtr hMod, uint dwThreadId);
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        private static IntPtr hookHandle = IntPtr.Zero;

        [STAThread]
        static void Main()
        {
            RegistryKey key = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);
            key.SetValue("dotCapture", Application.ExecutablePath.ToString());

            ApplicationConfiguration.Initialize();

            hookHandle = SetWindowsHookEx(13, LowLevelKeyboardProc, IntPtr.Zero, 0);

            dotCapture_ScreenCapture form = new dotCapture_ScreenCapture();
            Application.Run();
        }
        private static IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam){
            // Check if the key press event was sent
            if (nCode == 0 && wParam == (IntPtr)0x0100){
                // Get the key code from the lParam
                int keyCode = Marshal.ReadInt32(lParam);
                // Check if the key code is the Print Screen key
                if (keyCode == ((int)Keys.PrintScreen))
                {
                    dotCapture_ScreenCapture.instance.PressedCaptureScreen();
                }
                if (keyCode == ((int)Keys.Escape))
                {
                    dotCapture_ScreenCapture.instance.ExitScreenCapture();
                }
            }
            // Call the next hook in the chain
            return CallNextHookEx(hookHandle, nCode, wParam, lParam);
        }
    }
}