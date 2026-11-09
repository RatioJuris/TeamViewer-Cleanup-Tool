using System;
using System.Windows.Forms;

namespace TeamViewerCleanup
{
    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            var options = Cli.Parse(args);

            if (options.Screenshot != null)
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                using (var form = new MainForm { AutoScan = false })
                {
                    form.StartPosition = FormStartPosition.Manual;
                    form.Location = new System.Drawing.Point(-3000, -3000);
                    form.Show();
                    form.ScanNow();
                    Application.DoEvents();
                    form.SaveScreenshot(options.Screenshot);
                }
                return 0;
            }

            if (args.Length > 0) return Cli.Run(options);

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
            return 0;
        }
    }
}
