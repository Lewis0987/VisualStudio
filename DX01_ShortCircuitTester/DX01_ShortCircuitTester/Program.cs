using System;
using System.Windows.Forms;

namespace DX01_ShortCircuitTester
{
    internal static class Program
    {
        /// <summary>應用程式進入點。</summary>
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }
}
