using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Runtime.InteropServices;
using System.Reflection;
using System.Text;
using System.Windows.Forms;

[assembly: AssemblyTitle("AA - Always Awake")]
[assembly: AssemblyDescription("Utrzymuje komputer aktywnym (zapobiega wygaszeniu i blokadzie ekranu)")]
[assembly: AssemblyProduct("AA - Always Awake")]
[assembly: AssemblyCopyright("Copyright (c) 2026 Galeziok")]
[assembly: AssemblyVersion("2.1.0.0")]
[assembly: AssemblyFileVersion("2.1.0.0")]

namespace AlwaysAwake
{
    static class Program
    {
        static System.Threading.Mutex mutex;

        [STAThread]
        static void Main(string[] args)
        {
            bool createdNew;
            // Nazwa z "Local\" => jedna instancja na sesje uzytkownika.
            mutex = new System.Threading.Mutex(true, "Local\\AA_AlwaysAwake_SingleInstance", out createdNew);
            if (!createdNew) return; // juz dziala - nie uruchamiaj drugiej

            try
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new TrayContext(args));
            }
            finally
            {
                GC.KeepAlive(mutex);
                mutex.ReleaseMutex();
            }
        }
    }

    // ---------------------------------------------------------------
    // Ustawienia zapisywane do %AppData%\AA\settings.ini
    // ---------------------------------------------------------------
    public class Settings
    {
        public string Mode = "Both";          // Display | Input | Both
        public int IntervalSeconds = 60;
        public bool StartActive = true;
        public bool IdleAware = true;
        public int IdleThresholdSeconds = 50;
        public bool Jitter = false;
        public int AutoOffMinutes = 0;         // 0 = nigdy

        static string Dir()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AA");
        }
        static string FilePath() { return Path.Combine(Dir(), "settings.ini"); }

        static bool ParseBool(string v, bool def)
        {
            bool b;
            if (bool.TryParse(v, out b)) return b;
            if (v == "1") return true;
            if (v == "0") return false;
            return def;
        }

        public static Settings Load()
        {
            Settings s = new Settings();
            try
            {
                string p = FilePath();
                if (File.Exists(p))
                {
                    foreach (string line in File.ReadAllLines(p))
                    {
                        string t = line.Trim();
                        if (t.Length == 0 || t.StartsWith("#")) continue;
                        int eq = t.IndexOf('=');
                        if (eq <= 0) continue;
                        string k = t.Substring(0, eq).Trim();
                        string v = t.Substring(eq + 1).Trim();
                        int n;
                        switch (k)
                        {
                            case "Mode":
                                if (v == "Display" || v == "Input" || v == "Both") s.Mode = v;
                                break;
                            case "IntervalSeconds":
                                if (int.TryParse(v, out n) && n > 0) s.IntervalSeconds = n;
                                break;
                            case "StartActive": s.StartActive = ParseBool(v, s.StartActive); break;
                            case "IdleAware": s.IdleAware = ParseBool(v, s.IdleAware); break;
                            case "IdleThresholdSeconds":
                                if (int.TryParse(v, out n) && n > 0) s.IdleThresholdSeconds = n;
                                break;
                            case "Jitter": s.Jitter = ParseBool(v, s.Jitter); break;
                            case "AutoOffMinutes":
                                if (int.TryParse(v, out n) && n >= 0) s.AutoOffMinutes = n;
                                break;
                        }
                    }
                }
            }
            catch { }
            return s;
        }

        public void Save()
        {
            try
            {
                string dir = Dir();
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("# AA (Always Awake) - ustawienia");
                sb.AppendLine("Mode=" + Mode);
                sb.AppendLine("IntervalSeconds=" + IntervalSeconds);
                sb.AppendLine("StartActive=" + StartActive);
                sb.AppendLine("IdleAware=" + IdleAware);
                sb.AppendLine("IdleThresholdSeconds=" + IdleThresholdSeconds);
                sb.AppendLine("Jitter=" + Jitter);
                sb.AppendLine("AutoOffMinutes=" + AutoOffMinutes);
                File.WriteAllText(FilePath(), sb.ToString());
            }
            catch { }
        }

        public Settings Clone() { return (Settings)this.MemberwiseClone(); }

        // Argumenty wiersza polecen nadpisuja ustawienia na starcie:
        //   -Mode Display|Input|Both  -IntervalSeconds <n>  -Inactive
        // Akceptowane prefiksy: -, --, /; wielkosc liter bez znaczenia.
        // Zwraca true, gdy podano -Inactive (start wstrzymany, bez zmiany StartActive).
        public bool ApplyArgs(string[] args)
        {
            bool inactive = false;
            if (args == null) return inactive;
            for (int i = 0; i < args.Length; i++)
            {
                string k = args[i].TrimStart('-', '/').ToLowerInvariant();
                string v = (i + 1 < args.Length) ? args[i + 1] : null;
                int n;
                switch (k)
                {
                    case "mode":
                        if (v == null) break;
                        foreach (string m in new string[] { "Display", "Input", "Both" })
                            if (string.Equals(v, m, StringComparison.OrdinalIgnoreCase)) Mode = m;
                        i++;
                        break;
                    case "intervalseconds":
                        if (v != null && int.TryParse(v, out n) && n > 0) IntervalSeconds = n;
                        i++;
                        break;
                    case "inactive":
                        inactive = true;
                        break;
                }
            }
            return inactive;
        }
    }

    // ---------------------------------------------------------------
    // Autostart - skrot w folderze Startup uzytkownika (bez rejestru,
    // bez admina). Skrot tworzony przez WScript.Shell (late binding).
    // ---------------------------------------------------------------
    static class AutoStart
    {
        const string AppName = "AA";

        static string LnkPath()
        {
            string startup = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
            return Path.Combine(startup, AppName + ".lnk");
        }

        public static bool IsEnabled()
        {
            try { return File.Exists(LnkPath()); }
            catch { return false; }
        }

        // zwraca true, gdy stan koncowy zgadza sie z zadanym
        public static bool Set(bool on)
        {
            try
            {
                string lnk = LnkPath();
                if (on)
                {
                    string exe = Application.ExecutablePath;
                    Type t = Type.GetTypeFromProgID("WScript.Shell");
                    if (t == null) return false;
                    object shell = Activator.CreateInstance(t);
                    object sc = t.InvokeMember("CreateShortcut",
                        BindingFlags.InvokeMethod, null, shell, new object[] { lnk });
                    Type st = sc.GetType();
                    st.InvokeMember("TargetPath", BindingFlags.SetProperty, null, sc,
                        new object[] { exe });
                    st.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, sc,
                        new object[] { Path.GetDirectoryName(exe) });
                    st.InvokeMember("Description", BindingFlags.SetProperty, null, sc,
                        new object[] { "AA - Always Awake" });
                    st.InvokeMember("Save", BindingFlags.InvokeMethod, null, sc, null);
                }
                else
                {
                    if (File.Exists(lnk)) File.Delete(lnk);
                }
                return IsEnabled() == on;
            }
            catch { return false; }
        }
    }

    // ---------------------------------------------------------------
    // Aplikacja w zasobniku
    // ---------------------------------------------------------------
    public class TrayContext : ApplicationContext
    {
        public const string AppVersion = "2.1";

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern uint SetThreadExecutionState(uint esFlags);
        [DllImport("user32.dll", SetLastError = true)]
        static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);
        [DllImport("user32.dll", SetLastError = true)]
        static extern bool DestroyIcon(IntPtr handle);

        [StructLayout(LayoutKind.Sequential)]
        struct LASTINPUTINFO { public uint cbSize; public uint dwTime; }
        [DllImport("user32.dll")]
        static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

        const uint ES_CONTINUOUS       = 0x80000000;
        const uint ES_SYSTEM_REQUIRED  = 0x00000001;
        const uint ES_DISPLAY_REQUIRED = 0x00000002;
        const byte VK_F15 = 0x7E;
        const uint KEYEVENTF_KEYUP = 0x0002;

        Settings settings;
        NotifyIcon notify;
        Timer timer;
        Random rng = new Random();

        bool active;
        DateTime activationTime;

        Icon iconActive, iconInactive;
        ToolStripMenuItem miToggle, miModeMenu, miAutoOffMenu, miAutoStart, miIdle, miJitter;
        int[] autoOffValues = new int[] { 0, 30, 60, 120, 240 };
        string[] autoOffLabels = new string[] { "Nigdy", "30 min", "1 godz", "2 godz", "4 godz" };

        public TrayContext(string[] args)
        {
            settings = Settings.Load();
            bool argInactive = settings.ApplyArgs(args);
            active = settings.StartActive && !argInactive;

            iconActive   = MakeDot(Color.FromArgb(40, 180, 70));
            iconInactive = MakeDot(Color.FromArgb(150, 150, 150));

            BuildMenu();

            notify = new NotifyIcon();
            notify.ContextMenuStrip = BuildContextMenu();
            notify.Visible = true;
            notify.DoubleClick += delegate { SetActive(!active); };

            timer = new Timer();
            timer.Tick += delegate { OnTick(); };
            ApplyTimerBase();
            timer.Start();

            if (active) { activationTime = DateTime.Now; }
            UpdateUi();
            DoKeepAwake(true);
        }

        // ---- budowa menu ----
        ContextMenuStrip menu;

        void BuildMenu()
        {
            miToggle = new ToolStripMenuItem("Aktywne");
            miToggle.CheckOnClick = true;
            miToggle.Click += delegate { SetActive(miToggle.Checked); };

            miModeMenu = new ToolStripMenuItem("Tryb");
            foreach (string m in new string[] { "Display", "Input", "Both" })
            {
                ToolStripMenuItem item = new ToolStripMenuItem(m);
                item.Tag = m;
                item.Click += delegate(object s, EventArgs e)
                {
                    settings.Mode = (string)((ToolStripMenuItem)s).Tag;
                    settings.Save();
                    ClearKeepAwake(); // np. Both -> Input: zdejmij flagi ekranu
                    UpdateUi();
                    DoKeepAwake(true);
                };
                miModeMenu.DropDownItems.Add(item);
            }

            miAutoOffMenu = new ToolStripMenuItem("Auto-wylacz po");
            for (int i = 0; i < autoOffValues.Length; i++)
            {
                ToolStripMenuItem item = new ToolStripMenuItem(autoOffLabels[i]);
                item.Tag = autoOffValues[i];
                item.Click += delegate(object s, EventArgs e)
                {
                    settings.AutoOffMinutes = (int)((ToolStripMenuItem)s).Tag;
                    settings.Save();
                    if (active) activationTime = DateTime.Now;
                    UpdateUi();
                };
                miAutoOffMenu.DropDownItems.Add(item);
            }

            miIdle = new ToolStripMenuItem("Tryb bezczynnosci");
            miIdle.CheckOnClick = true;
            miIdle.Click += delegate { settings.IdleAware = miIdle.Checked; settings.Save(); };

            miJitter = new ToolStripMenuItem("Jitter interwalu");
            miJitter.CheckOnClick = true;
            miJitter.Click += delegate { settings.Jitter = miJitter.Checked; settings.Save(); ApplyTimerBase(); };

            miAutoStart = new ToolStripMenuItem("Uruchamiaj przy starcie");
            miAutoStart.CheckOnClick = true;
            miAutoStart.Click += delegate
            {
                bool ok = AutoStart.Set(miAutoStart.Checked);
                miAutoStart.Checked = AutoStart.IsEnabled();
                if (!ok && notify != null)
                    notify.ShowBalloonTip(4000, "AA",
                        "Nie udalo sie zmienic autostartu.", ToolTipIcon.Warning);
            };
        }

        ContextMenuStrip BuildContextMenu()
        {
            menu = new ContextMenuStrip();
            menu.Items.Add(miToggle);
            menu.Items.Add(miModeMenu);
            menu.Items.Add(miAutoOffMenu);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(miIdle);
            menu.Items.Add(miJitter);
            menu.Items.Add(miAutoStart);
            menu.Items.Add(new ToolStripSeparator());

            ToolStripMenuItem miSettings = new ToolStripMenuItem("Ustawienia / O programie...");
            miSettings.Click += delegate { OpenSettings(); };
            menu.Items.Add(miSettings);

            ToolStripMenuItem miExit = new ToolStripMenuItem("Zakoncz");
            miExit.Click += delegate { ExitApp(); };
            menu.Items.Add(miExit);
            return menu;
        }

        // ---- logika ----
        int GetIdleSeconds()
        {
            LASTINPUTINFO lii = new LASTINPUTINFO();
            lii.cbSize = (uint)Marshal.SizeOf(typeof(LASTINPUTINFO));
            if (!GetLastInputInfo(ref lii)) return 0;
            uint tick = (uint)Environment.TickCount;
            uint idleMs = tick - lii.dwTime;
            return (int)(idleMs / 1000);
        }

        int JitterMs()
        {
            int baseMs = settings.IntervalSeconds * 1000;
            if (!settings.Jitter) return baseMs;
            double f = 1.0 + (rng.NextDouble() * 0.2 - 0.1); // +/-10%
            int ms = (int)(baseMs * f);
            if (ms < 1000) ms = 1000;
            return ms;
        }

        void ApplyTimerBase()
        {
            int baseMs = settings.IntervalSeconds * 1000;
            if (baseMs < 1000) baseMs = 1000;
            timer.Interval = baseMs;
        }

        void OnTick()
        {
            if (!active) return;
            if (settings.AutoOffMinutes > 0 &&
                (DateTime.Now - activationTime).TotalMinutes >= settings.AutoOffMinutes)
            {
                SetActive(false);
                return;
            }
            DoKeepAwake(false);
            timer.Interval = JitterMs();
            UpdateTooltip();
        }

        void DoKeepAwake(bool immediate)
        {
            if (!active) return;
            if (settings.Mode == "Display" || settings.Mode == "Both")
                SetThreadExecutionState(ES_CONTINUOUS | ES_SYSTEM_REQUIRED | ES_DISPLAY_REQUIRED);

            if (settings.Mode == "Input" || settings.Mode == "Both")
            {
                bool send = true;
                if (settings.IdleAware && !immediate)
                    send = GetIdleSeconds() >= settings.IdleThresholdSeconds;
                if (send)
                {
                    keybd_event(VK_F15, 0, 0, UIntPtr.Zero);
                    keybd_event(VK_F15, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
                }
            }
        }

        void ClearKeepAwake() { SetThreadExecutionState(ES_CONTINUOUS); }

        void SetActive(bool on)
        {
            active = on;
            if (on) { activationTime = DateTime.Now; DoKeepAwake(true); }
            else { ClearKeepAwake(); }
            UpdateUi();
        }

        void UpdateUi()
        {
            notify.Icon = active ? iconActive : iconInactive;
            UpdateTooltip();
            miToggle.Checked = active;
            miIdle.Checked = settings.IdleAware;
            miJitter.Checked = settings.Jitter;
            miAutoStart.Checked = AutoStart.IsEnabled();

            foreach (ToolStripMenuItem it in miModeMenu.DropDownItems)
                it.Checked = ((string)it.Tag == settings.Mode);
            foreach (ToolStripMenuItem it in miAutoOffMenu.DropDownItems)
                it.Checked = ((int)it.Tag == settings.AutoOffMinutes);
        }

        // NotifyIcon.Text ma limit 63 znakow (.NET Framework rzuca wyjatek powyzej).
        void UpdateTooltip()
        {
            string text;
            if (active)
            {
                text = "AA - aktywne (" + settings.Mode + ")";
                if (settings.AutoOffMinutes > 0)
                {
                    TimeSpan left = activationTime.AddMinutes(settings.AutoOffMinutes) - DateTime.Now;
                    int mins = (int)Math.Ceiling(Math.Max(0, left.TotalMinutes));
                    text += string.Format(", wylaczy sie za {0}:{1:00}", mins / 60, mins % 60);
                }
            }
            else
            {
                text = "AA - wstrzymane (" + settings.Mode + ")";
            }
            if (text.Length > 63) text = text.Substring(0, 63);
            notify.Text = text;
        }

        void OpenSettings()
        {
            using (SettingsForm f = new SettingsForm(settings.Clone(), AutoStart.IsEnabled()))
            {
                if (f.ShowDialog() == DialogResult.OK)
                {
                    settings = f.Result;
                    settings.Save();
                    AutoStart.Set(f.AutoStartEnabled);
                    ApplyTimerBase();
                    if (active) activationTime = DateTime.Now;
                    ClearKeepAwake();
                    UpdateUi();
                    DoKeepAwake(true);
                }
            }
        }

        void ExitApp()
        {
            active = false;
            ClearKeepAwake();
            timer.Stop();
            notify.Visible = false;
            notify.Dispose();
            ExitThread();
        }

        Icon MakeDot(Color color)
        {
            using (Bitmap bmp = new Bitmap(16, 16))
            {
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.Clear(Color.Transparent);
                    using (SolidBrush brush = new SolidBrush(color))
                        g.FillEllipse(brush, 2, 2, 12, 12);
                    using (Pen pen = new Pen(Color.FromArgb(90, 0, 0, 0)))
                        g.DrawEllipse(pen, 2, 2, 12, 12);
                }
                IntPtr h = bmp.GetHicon();
                Icon ico = (Icon)Icon.FromHandle(h).Clone();
                DestroyIcon(h);
                return ico;
            }
        }
    }

    // ---------------------------------------------------------------
    // Okno Ustawienia / O programie
    // ---------------------------------------------------------------
    public class SettingsForm : Form
    {
        public Settings Result;
        public bool AutoStartEnabled;

        ComboBox cmbMode, cmbAutoOff;
        NumericUpDown numInterval, numIdle;
        CheckBox chkJitter, chkIdle, chkStartActive, chkAutoStart;
        int[] autoOffValues = new int[] { 0, 30, 60, 120, 240 };

        public SettingsForm(Settings s, bool autoStart)
        {
            Result = s;
            AutoStartEnabled = autoStart;

            Text = "AA - Always Awake";
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterScreen;
            MaximizeBox = false; MinimizeBox = false;
            ClientSize = new Size(360, 390);

            int x1 = 16, x2 = 170, y = 14, h = 26;

            Label title = new Label();
            title.Text = "AA - Always Awake  v" + TrayContext.AppVersion;
            title.Font = new Font(Font.FontFamily, 10, FontStyle.Bold);
            title.SetBounds(x1, y, 330, 22); Controls.Add(title); y += 34;

            AddLabel("Tryb:", x1, y);
            cmbMode = new ComboBox();
            cmbMode.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbMode.Items.AddRange(new object[] { "Display", "Input", "Both" });
            cmbMode.SelectedItem = s.Mode;
            cmbMode.SetBounds(x2, y - 3, 160, h); Controls.Add(cmbMode); y += h + 6;

            AddLabel("Odswiezaj co (s):", x1, y);
            numInterval = new NumericUpDown();
            numInterval.Minimum = 5; numInterval.Maximum = 3600;
            numInterval.Value = Clamp(s.IntervalSeconds, 5, 3600);
            numInterval.SetBounds(x2, y - 3, 160, h); Controls.Add(numInterval); y += h + 6;

            chkJitter = new CheckBox();
            chkJitter.Text = "Jitter interwalu (+/-10%)";
            chkJitter.Checked = s.Jitter;
            chkJitter.SetBounds(x1, y, 320, h); Controls.Add(chkJitter); y += h + 10;

            chkIdle = new CheckBox();
            chkIdle.Text = "Tryb bezczynnosci (wysylaj tylko gdy nieaktywny)";
            chkIdle.Checked = s.IdleAware;
            chkIdle.SetBounds(x1, y, 330, h); Controls.Add(chkIdle); y += h + 2;

            AddLabel("Prog bezczynnosci (s):", x1, y);
            numIdle = new NumericUpDown();
            numIdle.Minimum = 10; numIdle.Maximum = 3600;
            numIdle.Value = Clamp(s.IdleThresholdSeconds, 10, 3600);
            numIdle.SetBounds(x2, y - 3, 160, h); Controls.Add(numIdle); y += h + 6;

            AddLabel("Auto-wylacz po:", x1, y);
            cmbAutoOff = new ComboBox();
            cmbAutoOff.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbAutoOff.Items.AddRange(new object[] { "Nigdy", "30 min", "1 godz", "2 godz", "4 godz" });
            cmbAutoOff.SelectedIndex = IndexOfValue(s.AutoOffMinutes);
            cmbAutoOff.SetBounds(x2, y - 3, 160, h); Controls.Add(cmbAutoOff); y += h + 10;

            chkStartActive = new CheckBox();
            chkStartActive.Text = "Startuj jako aktywne";
            chkStartActive.Checked = s.StartActive;
            chkStartActive.SetBounds(x1, y, 330, h); Controls.Add(chkStartActive); y += h;

            chkAutoStart = new CheckBox();
            chkAutoStart.Text = "Uruchamiaj przy starcie systemu";
            chkAutoStart.Checked = autoStart;
            chkAutoStart.SetBounds(x1, y, 330, h); Controls.Add(chkAutoStart); y += h + 8;

            Label about = new Label();
            about.Text = "Utrzymuje komputer aktywnym (zapobiega wygaszeniu/blokadzie).\n" +
                         "Display = API zasilania, Input = klawisz F15, Both = oba.";
            about.SetBounds(x1, y, 330, 34); about.ForeColor = Color.Gray;
            Controls.Add(about); y += 40;

            Button ok = new Button();
            ok.Text = "OK"; ok.DialogResult = DialogResult.OK;
            ok.SetBounds(170, y, 80, 28);
            ok.Click += delegate { ReadBack(); };
            Controls.Add(ok);

            Button cancel = new Button();
            cancel.Text = "Anuluj"; cancel.DialogResult = DialogResult.Cancel;
            cancel.SetBounds(260, y, 80, 28);
            Controls.Add(cancel);

            AcceptButton = ok; CancelButton = cancel;
        }

        void AddLabel(string text, int x, int y)
        {
            Label l = new Label();
            l.Text = text; l.SetBounds(x, y, 150, 22);
            Controls.Add(l);
        }

        static int Clamp(int v, int lo, int hi)
        {
            if (v < lo) return lo;
            if (v > hi) return hi;
            return v;
        }

        int IndexOfValue(int minutes)
        {
            for (int i = 0; i < autoOffValues.Length; i++)
                if (autoOffValues[i] == minutes) return i;
            return 0;
        }

        void ReadBack()
        {
            Result.Mode = (string)cmbMode.SelectedItem;
            Result.IntervalSeconds = (int)numInterval.Value;
            Result.Jitter = chkJitter.Checked;
            Result.IdleAware = chkIdle.Checked;
            Result.IdleThresholdSeconds = (int)numIdle.Value;
            Result.AutoOffMinutes = autoOffValues[cmbAutoOff.SelectedIndex];
            Result.StartActive = chkStartActive.Checked;
            AutoStartEnabled = chkAutoStart.Checked;
        }
    }
}
