using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Reflection;
using System.Text;
using System.Windows.Forms;

[assembly: AssemblyTitle("AA - Always Awake")]
[assembly: AssemblyDescription("Utrzymuje komputer aktywnym (zapobiega wygaszeniu i blokadzie ekranu)")]
[assembly: AssemblyProduct("AA - Always Awake")]
[assembly: AssemblyCopyright("Copyright (c) 2026 Galeziok")]
[assembly: AssemblyVersion("2.2.0.0")]
[assembly: AssemblyFileVersion("2.2.0.0")]

namespace AlwaysAwake
{
    static class Program
    {
        static System.Threading.Mutex mutex;

        [STAThread]
        static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            Settings settings = Settings.Load();
            L.Init(settings.Language);

            bool createdNew;
            // Nazwa z "Local\" => jedna instancja na sesje uzytkownika.
            mutex = new System.Threading.Mutex(true, "Local\\AA_AlwaysAwake_SingleInstance", out createdNew);
            if (!createdNew) // juz dziala - nie uruchamiaj drugiej
            {
                MessageBox.Show(L.S("AlreadyRunning"), AppInfo.Name,
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                Application.Run(new TrayContext(settings, args));
            }
            finally
            {
                GC.KeepAlive(mutex);
                mutex.ReleaseMutex();
            }
        }
    }

    // ---------------------------------------------------------------
    // Informacje o programie
    // ---------------------------------------------------------------
    static class AppInfo
    {
        public const string Name = "AA - Always Awake";
        public const string Version = "2.2";
        public const string Author = "Galeziok";
        public const string AuthorUrl = "https://github.com/gynsior";
        public const string RepoUrl = "https://github.com/gynsior/always-awake";
    }

    // ---------------------------------------------------------------
    // Teksty interfejsu: polski, angielski, niemiecki.
    // "Auto" = jezyk Windows (pl/de), w pozostalych przypadkach angielski.
    // ---------------------------------------------------------------
    static class L
    {
        public static readonly string[] Codes = { "Auto", "pl", "en", "de" };
        static int idx = 1; // 0 = pl, 1 = en, 2 = de

        public static void Init(string language)
        {
            string code = language;
            if (string.IsNullOrEmpty(code) || code == "Auto")
                code = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
            idx = code == "pl" ? 0 : code == "de" ? 2 : 1;
        }

        public static string S(string key)
        {
            string[] v;
            return T.TryGetValue(key, out v) ? v[idx] : key;
        }

        // Nazwa jezyka w menu / liscie wyboru.
        public static string LanguageName(string code)
        {
            switch (code)
            {
                case "pl": return "Polski";
                case "en": return "English";
                case "de": return "Deutsch";
                default: return S("LangAuto");
            }
        }

        public static string AutoOffLabel(int minutes)
        {
            if (minutes == 0) return S("Never");
            if (minutes < 60) return minutes + " min";
            return string.Format(S("HoursFmt"), minutes / 60);
        }

        static readonly Dictionary<string, string[]> T = new Dictionary<string, string[]>
        {
            // --- menu w zasobniku ---
            { "MenuActive",    new[] { "Aktywne", "Active", "Aktiv" } },
            { "MenuMode",      new[] { "Tryb", "Mode", "Modus" } },
            { "MenuAutoOff",   new[] { "Auto-wyłącz po", "Auto-off after", "Automatisch aus nach" } },
            { "Never",         new[] { "Nigdy", "Never", "Nie" } },
            { "HoursFmt",      new[] { "{0} godz.", "{0} h", "{0} Std." } },
            { "MenuIdle",      new[] { "Tryb bezczynności", "Idle-aware mode", "Leerlaufmodus" } },
            { "MenuJitter",    new[] { "Jitter interwału", "Interval jitter", "Intervall-Jitter" } },
            { "MenuAutoStart", new[] { "Uruchamiaj przy starcie", "Run at startup", "Mit Windows starten" } },
            { "MenuLanguage",  new[] { "Język (Language)", "Language", "Sprache (Language)" } },
            { "LangAuto",      new[] { "Automatycznie (system)", "Automatic (system)", "Automatisch (System)" } },
            { "MenuSettings",  new[] { "Ustawienia...", "Settings...", "Einstellungen..." } },
            { "MenuAbout",     new[] { "O programie...", "About...", "Über AA..." } },
            { "MenuExit",      new[] { "Zakończ", "Exit", "Beenden" } },
            { "AutoStartFail", new[] { "Nie udało się zmienić autostartu.",
                                       "Could not change the startup setting.",
                                       "Autostart konnte nicht geändert werden." } },

            // --- podpowiedz przy ikonie (max 63 znaki razem) ---
            { "TipActive",     new[] { "AA - aktywne ({0})", "AA - active ({0})", "AA - aktiv ({0})" } },
            { "TipPaused",     new[] { "AA - wstrzymane ({0})", "AA - paused ({0})", "AA - pausiert ({0})" } },
            { "TipOffIn",      new[] { ", wyłączy się za {0}", ", off in {0}", ", aus in {0}" } },

            // --- druga kopia programu ---
            { "AlreadyRunning", new[] {
                "AA już działa - może być uruchomiona tylko jedna kopia programu.\n\n" +
                "Ikona (kropka) jest w zasobniku przy zegarze; jeśli jej nie widać, " +
                "kliknij strzałkę ^ obok zegara.",
                "AA is already running - only one copy of the program can run at a time.\n\n" +
                "Its icon (a dot) is in the notification area next to the clock; if you can't see it, " +
                "click the ^ arrow next to the clock.",
                "AA läuft bereits - es kann nur eine Instanz des Programms gleichzeitig laufen.\n\n" +
                "Das Symbol (ein Punkt) befindet sich im Infobereich neben der Uhr; falls es nicht " +
                "sichtbar ist, klicken Sie auf den Pfeil ^ neben der Uhr." } },

            // --- okno ustawien ---
            { "SettingsTitle", new[] { "AA - Ustawienia", "AA - Settings", "AA - Einstellungen" } },
            { "LblMode",       new[] { "Tryb:", "Mode:", "Modus:" } },
            { "ModeHint",      new[] { "Display = API zasilania, Input = klawisz F15, Both = oba.",
                                       "Display = power API, Input = F15 key, Both = both.",
                                       "Display = Energie-API, Input = Taste F15, Both = beides." } },
            { "LblInterval",   new[] { "Odświeżaj co (s):", "Refresh every (s):", "Aktualisieren alle (s):" } },
            { "ChkJitter",     new[] { "Jitter interwału (±10%)", "Interval jitter (±10%)", "Intervall-Jitter (±10%)" } },
            { "ChkIdle",       new[] { "Tryb bezczynności (wysyłaj tylko, gdy nieaktywny)",
                                       "Idle-aware (send only when you are idle)",
                                       "Leerlaufmodus (nur senden, wenn inaktiv)" } },
            { "LblIdle",       new[] { "Próg bezczynności (s):", "Idle threshold (s):", "Leerlauf-Schwelle (s):" } },
            { "LblAutoOff",    new[] { "Auto-wyłącz po:", "Auto-off after:", "Automatisch aus nach:" } },
            { "ChkStartActive", new[] { "Startuj jako aktywne", "Start as active", "Aktiv starten" } },
            { "ChkAutoStart",  new[] { "Uruchamiaj przy starcie systemu", "Run at Windows startup", "Mit Windows starten" } },
            { "LblLanguage",   new[] { "Język:", "Language:", "Sprache:" } },
            { "Ok",            new[] { "OK", "OK", "OK" } },
            { "Cancel",        new[] { "Anuluj", "Cancel", "Abbrechen" } },

            // --- okno o programie ---
            { "AboutTitle",    new[] { "O programie", "About", "Über" } },
            { "AboutDesc",     new[] { "Utrzymuje komputer aktywnym - zapobiega wygaszeniu i blokadzie ekranu.",
                                       "Keeps your computer awake - prevents the screen from turning off or locking.",
                                       "Hält den Computer wach - verhindert das Abschalten und Sperren des Bildschirms." } },
            { "AboutVersion",  new[] { "Wersja:", "Version:", "Version:" } },
            { "AboutAuthor",   new[] { "Autor:", "Author:", "Autor:" } },
            { "AboutLicense",  new[] { "Licencja:", "License:", "Lizenz:" } },
            { "AboutLicenseText", new[] { "MIT (darmowy, open source)", "MIT (free, open source)", "MIT (kostenlos, Open Source)" } },
            { "AboutSource",   new[] { "Kod źródłowy:", "Source code:", "Quellcode:" } },
            { "AboutSettings", new[] { "Plik ustawień:", "Settings file:", "Einstellungsdatei:" } },
            { "Close",         new[] { "Zamknij", "Close", "Schließen" } },
        };
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
        public string Language = "Auto";       // Auto | pl | en | de

        static string Dir()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AA");
        }
        public static string FilePath() { return Path.Combine(Dir(), "settings.ini"); }

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
                            case "Language":
                                if (Array.IndexOf(L.Codes, v) >= 0) s.Language = v;
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
                sb.AppendLine("Language=" + Language);
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
                        new object[] { AppInfo.Name });
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
        ToolStripMenuItem miLanguageMenu, miSettings, miAbout, miExit;
        int[] autoOffValues = new int[] { 0, 30, 60, 120, 240 };

        // otwarte okna (zeby nie otwierac drugiej kopii tego samego okna)
        SettingsForm settingsForm;
        AboutForm aboutForm;

        public TrayContext(Settings loaded, string[] args)
        {
            settings = loaded;
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
            ApplyTexts();
            UpdateUi();
            DoKeepAwake(true);
        }

        // ---- budowa menu ----
        ContextMenuStrip menu;

        void BuildMenu()
        {
            miToggle = new ToolStripMenuItem();
            miToggle.CheckOnClick = true;
            miToggle.Click += delegate { SetActive(miToggle.Checked); };

            miModeMenu = new ToolStripMenuItem();
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

            miAutoOffMenu = new ToolStripMenuItem();
            for (int i = 0; i < autoOffValues.Length; i++)
            {
                ToolStripMenuItem item = new ToolStripMenuItem();
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

            miIdle = new ToolStripMenuItem();
            miIdle.CheckOnClick = true;
            miIdle.Click += delegate { settings.IdleAware = miIdle.Checked; settings.Save(); };

            miJitter = new ToolStripMenuItem();
            miJitter.CheckOnClick = true;
            miJitter.Click += delegate { settings.Jitter = miJitter.Checked; settings.Save(); ApplyTimerBase(); };

            miAutoStart = new ToolStripMenuItem();
            miAutoStart.CheckOnClick = true;
            miAutoStart.Click += delegate
            {
                bool ok = AutoStart.Set(miAutoStart.Checked);
                miAutoStart.Checked = AutoStart.IsEnabled();
                if (!ok && notify != null)
                    notify.ShowBalloonTip(4000, "AA", L.S("AutoStartFail"), ToolTipIcon.Warning);
            };

            miLanguageMenu = new ToolStripMenuItem();
            foreach (string code in L.Codes)
            {
                ToolStripMenuItem item = new ToolStripMenuItem();
                item.Tag = code;
                item.Click += delegate(object s, EventArgs e)
                {
                    SetLanguage((string)((ToolStripMenuItem)s).Tag);
                };
                miLanguageMenu.DropDownItems.Add(item);
            }

            miSettings = new ToolStripMenuItem();
            miSettings.Click += delegate { OpenSettings(); };

            miAbout = new ToolStripMenuItem();
            miAbout.Click += delegate { OpenAbout(); };

            miExit = new ToolStripMenuItem();
            miExit.Click += delegate { ExitApp(); };
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
            menu.Items.Add(miLanguageMenu);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(miSettings);
            menu.Items.Add(miAbout);
            menu.Items.Add(miExit);
            return menu;
        }

        // Ustawia teksty menu w biezacym jezyku (wywolywane tez po zmianie jezyka).
        void ApplyTexts()
        {
            miToggle.Text = L.S("MenuActive");
            miModeMenu.Text = L.S("MenuMode");
            miAutoOffMenu.Text = L.S("MenuAutoOff");
            foreach (ToolStripMenuItem it in miAutoOffMenu.DropDownItems)
                it.Text = L.AutoOffLabel((int)it.Tag);
            miIdle.Text = L.S("MenuIdle");
            miJitter.Text = L.S("MenuJitter");
            miAutoStart.Text = L.S("MenuAutoStart");
            miLanguageMenu.Text = L.S("MenuLanguage");
            foreach (ToolStripMenuItem it in miLanguageMenu.DropDownItems)
                it.Text = L.LanguageName((string)it.Tag);
            miSettings.Text = L.S("MenuSettings");
            miAbout.Text = L.S("MenuAbout");
            miExit.Text = L.S("MenuExit");
        }

        void SetLanguage(string code)
        {
            settings.Language = code;
            settings.Save();
            L.Init(code);
            ApplyTexts();
            UpdateUi();
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
            foreach (ToolStripMenuItem it in miLanguageMenu.DropDownItems)
                it.Checked = ((string)it.Tag == settings.Language);
        }

        // NotifyIcon.Text ma limit 63 znakow (.NET Framework rzuca wyjatek powyzej).
        void UpdateTooltip()
        {
            string text;
            if (active)
            {
                text = string.Format(L.S("TipActive"), settings.Mode);
                if (settings.AutoOffMinutes > 0)
                {
                    TimeSpan left = activationTime.AddMinutes(settings.AutoOffMinutes) - DateTime.Now;
                    int mins = (int)Math.Ceiling(Math.Max(0, left.TotalMinutes));
                    text += string.Format(L.S("TipOffIn"),
                        string.Format("{0}:{1:00}", mins / 60, mins % 60));
                }
            }
            else
            {
                text = string.Format(L.S("TipPaused"), settings.Mode);
            }
            if (text.Length > 63) text = text.Substring(0, 63);
            notify.Text = text;
        }

        void OpenSettings()
        {
            if (settingsForm != null) { settingsForm.Activate(); return; }
            using (settingsForm = new SettingsForm(settings.Clone(), AutoStart.IsEnabled()))
            {
                if (settingsForm.ShowDialog() == DialogResult.OK)
                {
                    settings = settingsForm.Result;
                    settings.Save();
                    AutoStart.Set(settingsForm.AutoStartEnabled);
                    L.Init(settings.Language);
                    ApplyTexts();
                    ApplyTimerBase();
                    if (active) activationTime = DateTime.Now;
                    ClearKeepAwake();
                    UpdateUi();
                    DoKeepAwake(true);
                }
            }
            settingsForm = null;
        }

        void OpenAbout()
        {
            if (aboutForm != null) { aboutForm.Activate(); return; }
            using (aboutForm = new AboutForm())
                aboutForm.ShowDialog();
            aboutForm = null;
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
    // Okno Ustawienia
    // ---------------------------------------------------------------
    public class SettingsForm : Form
    {
        public Settings Result;
        public bool AutoStartEnabled;

        ComboBox cmbMode, cmbAutoOff, cmbLanguage;
        NumericUpDown numInterval, numIdle;
        CheckBox chkJitter, chkIdle, chkStartActive, chkAutoStart;
        int[] autoOffValues = new int[] { 0, 30, 60, 120, 240 };

        const int x1 = 16, x2 = 210, w2 = 180, h = 26;

        public SettingsForm(Settings s, bool autoStart)
        {
            Result = s;
            AutoStartEnabled = autoStart;

            Text = L.S("SettingsTitle");
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterScreen;
            MaximizeBox = false; MinimizeBox = false;

            int y = 16;

            AddLabel(L.S("LblMode"), y);
            cmbMode = new ComboBox();
            cmbMode.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbMode.Items.AddRange(new object[] { "Display", "Input", "Both" });
            cmbMode.SelectedItem = s.Mode;
            cmbMode.SetBounds(x2, y - 3, w2, h); Controls.Add(cmbMode); y += h + 2;

            Label hint = new Label();
            hint.Text = L.S("ModeHint");
            hint.ForeColor = Color.Gray;
            hint.SetBounds(x1, y, x2 + w2 - x1, 20); Controls.Add(hint); y += 26;

            AddLabel(L.S("LblInterval"), y);
            numInterval = new NumericUpDown();
            numInterval.Minimum = 5; numInterval.Maximum = 3600;
            numInterval.Value = Clamp(s.IntervalSeconds, 5, 3600);
            numInterval.SetBounds(x2, y - 3, w2, h); Controls.Add(numInterval); y += h + 6;

            chkJitter = AddCheck(L.S("ChkJitter"), s.Jitter, ref y);
            y += 4;
            chkIdle = AddCheck(L.S("ChkIdle"), s.IdleAware, ref y);

            AddLabel(L.S("LblIdle"), y);
            numIdle = new NumericUpDown();
            numIdle.Minimum = 10; numIdle.Maximum = 3600;
            numIdle.Value = Clamp(s.IdleThresholdSeconds, 10, 3600);
            numIdle.SetBounds(x2, y - 3, w2, h); Controls.Add(numIdle); y += h + 6;

            AddLabel(L.S("LblAutoOff"), y);
            cmbAutoOff = new ComboBox();
            cmbAutoOff.DropDownStyle = ComboBoxStyle.DropDownList;
            foreach (int m in autoOffValues) cmbAutoOff.Items.Add(L.AutoOffLabel(m));
            cmbAutoOff.SelectedIndex = IndexOfValue(s.AutoOffMinutes);
            cmbAutoOff.SetBounds(x2, y - 3, w2, h); Controls.Add(cmbAutoOff); y += h + 10;

            chkStartActive = AddCheck(L.S("ChkStartActive"), s.StartActive, ref y);
            chkAutoStart = AddCheck(L.S("ChkAutoStart"), autoStart, ref y);
            y += 6;

            AddLabel(L.S("LblLanguage"), y);
            cmbLanguage = new ComboBox();
            cmbLanguage.DropDownStyle = ComboBoxStyle.DropDownList;
            foreach (string code in L.Codes) cmbLanguage.Items.Add(L.LanguageName(code));
            cmbLanguage.SelectedIndex = Math.Max(0, Array.IndexOf(L.Codes, s.Language));
            cmbLanguage.SetBounds(x2, y - 3, w2, h); Controls.Add(cmbLanguage); y += h + 16;

            Button ok = new Button();
            ok.Text = L.S("Ok"); ok.DialogResult = DialogResult.OK;
            ok.SetBounds(x2 + w2 - 190, y, 90, 28);
            ok.Click += delegate { ReadBack(); };
            Controls.Add(ok);

            Button cancel = new Button();
            cancel.Text = L.S("Cancel"); cancel.DialogResult = DialogResult.Cancel;
            cancel.SetBounds(x2 + w2 - 90, y, 90, 28);
            Controls.Add(cancel);

            AcceptButton = ok; CancelButton = cancel;
            ClientSize = new Size(x2 + w2 + x1, y + 28 + 16);
        }

        void AddLabel(string text, int y)
        {
            Label l = new Label();
            l.Text = text; l.SetBounds(x1, y, x2 - x1 - 8, 22);
            Controls.Add(l);
        }

        CheckBox AddCheck(string text, bool value, ref int y)
        {
            CheckBox c = new CheckBox();
            c.Text = text;
            c.Checked = value;
            c.SetBounds(x1, y, x2 + w2 - x1, h);
            Controls.Add(c);
            y += h;
            return c;
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
            Result.Language = L.Codes[cmbLanguage.SelectedIndex];
            AutoStartEnabled = chkAutoStart.Checked;
        }
    }

    // ---------------------------------------------------------------
    // Okno O programie
    // ---------------------------------------------------------------
    public class AboutForm : Form
    {
        const int x1 = 16, xv = 130, width = 440;

        public AboutForm()
        {
            Text = L.S("AboutTitle") + " - AA";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterScreen;
            MaximizeBox = false; MinimizeBox = false;

            Icon appIcon = null;
            try { appIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
            if (appIcon != null) Icon = appIcon;

            int y = 16;

            PictureBox pic = new PictureBox();
            pic.SetBounds(x1, y, 32, 32);
            pic.SizeMode = PictureBoxSizeMode.Zoom;
            if (appIcon != null) pic.Image = appIcon.ToBitmap();
            Controls.Add(pic);

            Label title = new Label();
            title.Text = AppInfo.Name;
            title.Font = new Font(Font.FontFamily, 12, FontStyle.Bold);
            title.SetBounds(x1 + 44, y + 4, width - x1 - 60, 26);
            Controls.Add(title);
            y += 46;

            Label desc = new Label();
            desc.Text = L.S("AboutDesc");
            desc.SetBounds(x1, y, width - 2 * x1, 34);
            Controls.Add(desc);
            y += 42;

            AddRow(L.S("AboutVersion"), AppInfo.Version, null, ref y);
            AddRow(L.S("AboutAuthor"), AppInfo.Author + " (github.com/gynsior)", AppInfo.AuthorUrl, ref y);
            AddRow(L.S("AboutLicense"), L.S("AboutLicenseText"), null, ref y);
            AddRow(L.S("AboutSource"), "github.com/gynsior/always-awake", AppInfo.RepoUrl, ref y);
            AddRow(L.S("AboutSettings"), Settings.FilePath(), null, ref y);
            y += 8;

            Label copy = new Label();
            copy.Text = "© 2026 " + AppInfo.Author;
            copy.ForeColor = Color.Gray;
            copy.SetBounds(x1, y + 6, 200, 20);
            Controls.Add(copy);

            Button close = new Button();
            close.Text = L.S("Close"); close.DialogResult = DialogResult.OK;
            close.SetBounds(width - x1 - 90, y, 90, 28);
            Controls.Add(close);
            AcceptButton = close; CancelButton = close;

            ClientSize = new Size(width, y + 28 + 16);
        }

        // Wiersz "etykieta: wartosc"; gdy url != null, wartosc jest klikalnym linkiem.
        void AddRow(string label, string value, string url, ref int y)
        {
            Label l = new Label();
            l.Text = label; l.SetBounds(x1, y, xv - x1 - 6, 20);
            Controls.Add(l);

            if (url == null)
            {
                Label v = new Label();
                v.Text = value; v.AutoEllipsis = true;
                v.SetBounds(xv, y, width - xv - x1, 20);
                Controls.Add(v);
            }
            else
            {
                LinkLabel v = new LinkLabel();
                v.Text = value;
                v.SetBounds(xv, y, width - xv - x1, 20);
                v.LinkClicked += delegate
                {
                    try { Process.Start(url); } catch { }
                };
                Controls.Add(v);
            }
            y += 24;
        }
    }
}
