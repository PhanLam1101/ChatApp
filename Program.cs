using System;
using System.Windows.Forms;

namespace MessagingApp
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            AppPaths.Initialize();
            Application.EnableVisualStyles();
            Application.Run(new MainForm());
        }
    }
}
