using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using TeamViewerCleanup.Core;
using Trace = TeamViewerCleanup.Core.Trace;

namespace TeamViewerCleanup
{
    public sealed class MainForm : Form
    {
        private static readonly Color Navy = Color.FromArgb(15, 31, 77);
        private static readonly Color Accent = Color.FromArgb(14, 99, 156);
        private static readonly Color Good = Color.FromArgb(22, 128, 61);
        private static readonly Color Warn = Color.FromArgb(180, 83, 9);

        private readonly Scanner _scanner = new Scanner(ScanTargets.ForThisComputer());
        private ScanResult _scan = new ScanResult();

        private readonly Label _status = new Label();
        private readonly Panel _statusBar = new Panel();
        private readonly Panel _adminBar = new Panel();
        private readonly ListView _list = new ListView();
        private readonly Button _scanButton = new Button();
        private readonly Button _uninstallButton = new Button();
        private readonly Button _removeButton = new Button();
        private readonly Button _reportButton = new Button();

        public MainForm()
        {
            Text = "TeamViewer Cleanup Tool";
            Font = new Font("Segoe UI", 9.75f);
            AutoScaleMode = AutoScaleMode.Dpi;
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(820, 560);
            Size = new Size(940, 640);
            BackColor = Color.White;
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);

            Controls.Add(BuildList());
            Controls.Add(BuildButtons());
            Controls.Add(BuildStatus());
            Controls.Add(BuildAdminBar());
            Controls.Add(BuildHeader());
            Controls.Add(BuildFooter());

            Shown += async (s, e) => { if (AutoScan) await ScanAsync(); };
        }

        /// <summary>Scan when the window opens. Turned off for documentation screenshots.</summary>
        public bool AutoScan { get; set; } = true;

        private Control BuildHeader()
        {
            var header = new Panel { Dock = DockStyle.Top, Height = 92, BackColor = Navy, Padding = new Padding(24, 16, 24, 12) };
            var title = new Label
            {
                Text = "TeamViewer Cleanup Tool",
                ForeColor = Color.White,
                Font = new Font("Segoe UI Semibold", 17f),
                AutoSize = true,
                Location = new Point(22, 14),
            };
            var subtitle = new Label
            {
                Text = "Find and remove everything TeamViewer leaves behind: for a clean reinstall, or to make sure it is completely gone.",
                ForeColor = Color.FromArgb(203, 213, 225),
                AutoSize = true,
                Location = new Point(24, 54),
            };
            header.Controls.Add(title);
            header.Controls.Add(subtitle);
            return header;
        }

        private Control BuildAdminBar()
        {
            _adminBar.Dock = DockStyle.Top;
            _adminBar.Height = 44;
            _adminBar.BackColor = Color.FromArgb(254, 243, 199);
            _adminBar.Padding = new Padding(24, 0, 16, 0);
            _adminBar.Visible = !Elevation.IsAdministrator;

            var text = new Label
            {
                Text = "Running without administrator rights. You can scan, but some items can only be removed as administrator.",
                ForeColor = Color.FromArgb(120, 53, 15),
                AutoSize = true,
                Location = new Point(24, 13),
            };
            var button = new Button { Text = "Restart as administrator", AutoSize = true, Anchor = AnchorStyles.Top | AnchorStyles.Right, FlatStyle = FlatStyle.System };
            button.Click += (s, e) => { if (Elevation.RestartAsAdministrator()) Close(); };
            _adminBar.Controls.Add(text);
            _adminBar.Controls.Add(button);
            _adminBar.Resize += (s, e) => button.Location = new Point(_adminBar.Width - button.Width - 16, (_adminBar.Height - button.Height) / 2);
            return _adminBar;
        }

        private Control BuildStatus()
        {
            _statusBar.Dock = DockStyle.Top;
            _statusBar.Height = 64;
            _statusBar.Padding = new Padding(24, 0, 24, 0);
            _status.Dock = DockStyle.Fill;
            _status.TextAlign = ContentAlignment.MiddleLeft;
            _status.Font = new Font("Segoe UI Semibold", 11.5f);
            _status.Text = "Scanning…";
            _statusBar.Controls.Add(_status);
            return _statusBar;
        }

        private Control BuildList()
        {
            var host = new Panel { Dock = DockStyle.Fill, Padding = new Padding(24, 0, 24, 8) };
            _list.Dock = DockStyle.Fill;
            _list.View = View.Details;
            _list.CheckBoxes = true;
            _list.FullRowSelect = true;
            _list.HeaderStyle = ColumnHeaderStyle.Nonclickable;
            _list.BorderStyle = BorderStyle.FixedSingle;
            _list.Columns.Add("Item", 260);
            _list.Columns.Add("Type", 150);
            _list.Columns.Add("Location", 440);
            _list.ItemCheck += (s, e) =>
            {
                if (_list.Items[e.Index].Tag is Trace t && !t.Removable) e.NewValue = CheckState.Unchecked;
            };
            _list.ItemChecked += (s, e) => UpdateButtons();
            host.Controls.Add(_list);
            return host;
        }

        private Control BuildButtons()
        {
            var bar = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 60,
                Padding = new Padding(22, 10, 22, 10),
                FlowDirection = FlowDirection.LeftToRight,
            };
            Style(_removeButton, "Remove selected", primary: true);
            Style(_uninstallButton, "Run TeamViewer uninstaller", primary: false);
            Style(_scanButton, "Scan again", primary: false);
            Style(_reportButton, "Save report…", primary: false);

            _removeButton.Click += async (s, e) => await RemoveSelectedAsync();
            _uninstallButton.Click += async (s, e) => await RunUninstallerAsync();
            _scanButton.Click += async (s, e) => await ScanAsync();
            _reportButton.Click += (s, e) => SaveReport();

            bar.Controls.AddRange(new Control[] { _removeButton, _uninstallButton, _scanButton, _reportButton });
            return bar;
        }

        private Control BuildFooter()
        {
            var footer = new Panel { Dock = DockStyle.Bottom, Height = 36, BackColor = Color.FromArgb(241, 245, 249), Padding = new Padding(24, 0, 24, 0) };
            var scam = new LinkLabel { Text = "Were you targeted by a remote-access scam?", AutoSize = true, Location = new Point(24, 9), LinkColor = Warn };
            scam.LinkClicked += (s, e) => ShowScamHelp();
            var about = new LinkLabel { Text = "About", AutoSize = true, Anchor = AnchorStyles.Top | AnchorStyles.Right, LinkColor = Accent };
            about.LinkClicked += (s, e) => ShowAbout();
            footer.Controls.Add(scam);
            footer.Controls.Add(about);
            footer.Resize += (s, e) => about.Location = new Point(footer.Width - about.Width - 24, 9);
            return footer;
        }

        private static void Style(Button b, string text, bool primary)
        {
            b.Text = text;
            b.AutoSize = true;
            b.Height = 36;
            b.Padding = new Padding(10, 2, 10, 2);
            b.Margin = new Padding(0, 0, 10, 0);
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderColor = primary ? Accent : Color.FromArgb(203, 213, 225);
            b.BackColor = primary ? Accent : Color.White;
            b.ForeColor = primary ? Color.White : Color.FromArgb(30, 41, 59);
            b.Cursor = Cursors.Hand;
        }

        public async Task ScanAsync()
        {
            SetBusy(true, "Scanning…");
            _scan = await Task.Run(() => _scanner.Scan());
            ShowResult();
            SetBusy(false);
        }

        public void ScanNow()
        {
            _scan = _scanner.Scan();
            ShowResult();
        }

        private void ShowResult()
        {
            _list.BeginUpdate();
            _list.Items.Clear();
            foreach (var t in _scan.Traces)
            {
                var name = t.Version.Length > 0 ? $"{t.Name} ({t.Version})" : t.Name;
                var item = new ListViewItem(new[] { name, t.KindLabel, t.Location }) { Tag = t, Checked = t.Removable };
                if (!t.Removable) item.ForeColor = Color.FromArgb(100, 116, 139);
                _list.Items.Add(item);
            }
            _list.EndUpdate();

            _status.Text = Report.Summary(_scan);
            _status.ForeColor = _scan.IsClean ? Good : Warn;
            _statusBar.BackColor = _scan.IsClean ? Color.FromArgb(240, 253, 244) : Color.FromArgb(255, 251, 235);
            UpdateButtons();
        }

        private void UpdateButtons()
        {
            _removeButton.Enabled = _list.CheckedItems.Count > 0;
            _uninstallButton.Visible = _scan.IsInstalled;
            _reportButton.Enabled = true;
        }

        private void SetBusy(bool busy, string? message = null)
        {
            UseWaitCursor = busy;
            foreach (var b in new[] { _removeButton, _uninstallButton, _scanButton, _reportButton }) b.Enabled = !busy;
            if (busy && message != null)
            {
                _status.Text = message;
                _status.ForeColor = Color.FromArgb(51, 65, 85);
                _statusBar.BackColor = Color.White;
            }
            if (!busy) UpdateButtons();
        }

        private async Task RunUninstallerAsync()
        {
            var installation = _scan.Installations.FirstOrDefault();
            if (installation == null) return;
            SetBusy(true, "Waiting for TeamViewer's uninstaller to finish…");
            bool started = await Task.Run(() => Cleaner.RunUninstaller(installation, silent: false));
            if (!started) MessageBox.Show(this, "TeamViewer's uninstaller could not be started. Uninstall TeamViewer from Settings › Apps, then scan again.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            await ScanAsync();
        }

        private async Task RemoveSelectedAsync()
        {
            var selected = _list.CheckedItems.Cast<ListViewItem>().Select(i => (Trace)i.Tag).ToList();
            if (selected.Count == 0) return;

            if (_scan.IsInstalled)
            {
                var answer = MessageBox.Show(this,
                    "TeamViewer is still installed. For a clean result, run TeamViewer's own uninstaller first.\n\nRun the uninstaller now?",
                    Text, MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
                if (answer == DialogResult.Cancel) return;
                if (answer == DialogResult.Yes) { await RunUninstallerAsync(); return; }
            }

            if (!Elevation.IsAdministrator && selected.Any(t => t.NeedsAdmin))
            {
                var answer = MessageBox.Show(this,
                    "Some selected items can only be removed with administrator rights.\n\nRestart the tool as administrator?",
                    Text, MessageBoxButtons.YesNoCancel, MessageBoxIcon.Information);
                if (answer == DialogResult.Cancel) return;
                if (answer == DialogResult.Yes) { if (Elevation.RestartAsAdministrator()) Close(); return; }
            }

            var confirm = MessageBox.Show(this,
                $"Remove {selected.Count} item{(selected.Count == 1 ? "" : "s")}? Running TeamViewer programs will be closed. This cannot be undone.",
                Text, MessageBoxButtons.OKCancel, MessageBoxIcon.Warning);
            if (confirm != DialogResult.OK) return;

            SetBusy(true, "Removing…");
            var results = await Task.Run(() => new Cleaner().Remove(selected));
            var before = _scan;
            await ScanAsync();

            int failed = results.Count(r => !r.Success);
            if (failed == 0)
            {
                MessageBox.Show(this, "Done. " + Report.Summary(_scan), Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                var details = string.Join("\n", results.Where(r => !r.Success).Take(8).Select(r => $"• {r.Trace.Name}: {r.Message}"));
                MessageBox.Show(this, $"{failed} item{(failed == 1 ? "" : "s")} could not be removed:\n\n{details}\n\nRestart Windows and try again, or save a report for details.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _lastReport = Report.Build(before, results);
            }
        }

        private string? _lastReport;

        private void SaveReport()
        {
            using (var dialog = new SaveFileDialog
            {
                FileName = $"TeamViewer-Cleanup-Report-{DateTime.Now:yyyyMMdd-HHmm}.txt",
                Filter = "Text file (*.txt)|*.txt",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
            })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                File.WriteAllText(dialog.FileName, _lastReport ?? Report.Build(_scan), System.Text.Encoding.UTF8);
            }
        }

        private void ShowScamHelp()
        {
            MessageBox.Show(this,
                "If someone you did not know asked you to install TeamViewer or another remote-access app, act quickly:\n\n" +
                "1.  Disconnect this computer from the internet.\n" +
                "2.  Use this tool to remove TeamViewer completely, then restart the computer.\n" +
                "3.  From a different device, change your email, banking and other important passwords.\n" +
                "4.  Call your bank immediately if you shared card, UPI or banking details.\n" +
                "5.  In India, call the National Cyber Crime Helpline 1930 or report at cybercrime.gov.in. Elsewhere, contact your local police.\n\n" +
                "Genuine banks, companies and government offices will not ask you to install remote-access software.",
                "Remote-access scams", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void ShowAbout()
        {
            var answer = MessageBox.Show(this,
                $"TeamViewer Cleanup Tool {AppInfo.Version}\nby RatioJuris · Free and open source (MIT)\n\n" +
                "Removes TeamViewer's leftover files, settings, services and startup entries after uninstalling, and checks that remote access is fully gone.\n\n" +
                "This tool does not reset TeamViewer IDs or change licence status. It is not affiliated with or endorsed by TeamViewer. TeamViewer is a trademark of TeamViewer Germany GmbH.\n\n" +
                "Open the project page?",
                "About", MessageBoxButtons.YesNo, MessageBoxIcon.Information);
            if (answer == DialogResult.Yes) Process.Start(new ProcessStartInfo(AppInfo.Website) { UseShellExecute = true });
        }

        /// <summary>Renders the window to an image (used for documentation screenshots).</summary>
        public void SaveScreenshot(string path)
        {
            // Capture only the window's content: the frame drawn by DrawToBitmap uses the old classic theme.
            var client = PointToScreen(Point.Empty);
            var offset = new Point(client.X - Left, client.Y - Top);
            using (var full = new Bitmap(Width, Height))
            using (var content = new Bitmap(ClientSize.Width, ClientSize.Height))
            {
                DrawToBitmap(full, new Rectangle(0, 0, Width, Height));
                using (var g = Graphics.FromImage(content))
                    g.DrawImage(full, new Rectangle(Point.Empty, ClientSize), new Rectangle(offset, ClientSize), GraphicsUnit.Pixel);
                content.Save(path, System.Drawing.Imaging.ImageFormat.Png);
            }
        }
    }
}
