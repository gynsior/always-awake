using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

[assembly: AssemblyTitle("AA - Always Awake")]
[assembly: AssemblyDescription("Utrzymuje komputer aktywnym (zapobiega wygaszeniu i blokadzie ekranu)")]
[assembly: AssemblyProduct("AA - Always Awake")]
[assembly: AssemblyCopyright("Copyright (c) 2026 Piotr Gałęziok")]
[assembly: AssemblyVersion("2.7.0.0")]
[assembly: AssemblyFileVersion("2.7.0.0")]

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

            // Po aktualizacji stara wersja moze jeszcze konczyc prace - poczekaj na nia do 10 s.
            bool afterUpdate = Array.Exists(args, delegate(string a) { return a.TrimStart('-', '/').ToLowerInvariant() == "updated"; });
            bool createdNew = false;
            DateTime waitUntil = DateTime.Now.AddSeconds(afterUpdate ? 10 : 0);
            while (true)
            {
                // Nazwa z "Local\" => jedna instancja na sesje uzytkownika.
                mutex = new System.Threading.Mutex(true, "Local\\AA_AlwaysAwake_SingleInstance", out createdNew);
                if (createdNew || DateTime.Now >= waitUntil) break;
                mutex.Close();
                System.Threading.Thread.Sleep(250);
            }
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
        public const string Version = "2.7";
        public const string Author = "Piotr Gałęziok";
        public const string WebsiteUrl = "https://galeziok.pl";
        public const string RepoUrl = "https://github.com/gynsior/always-awake";
        public const string ReleasesUrl = RepoUrl + "/releases/latest";
        public const string LatestApiUrl = "https://api.github.com/repos/gynsior/always-awake/releases/latest";

        public static void OpenUrl(string url)
        {
            try { Process.Start(url); } catch { }
        }
    }

    // ---------------------------------------------------------------
    // Sprawdzanie aktualizacji (GitHub Releases)
    // ---------------------------------------------------------------
    class ReleaseInfo
    {
        public string Version;      // np. "2.4"
        public string DownloadUrl;  // link do AA.exe w zalacznikach release'u
        public string Sha256;       // suma z opisu release'u ("SHA-256: `...`")
    }

    static class Updates
    {
        // Wyciaga wersje z odpowiedzi API, np. "tag_name": "v2.3" -> "2.3".
        public static string ParseTag(string json)
        {
            if (json == null) return null;
            Match m = Regex.Match(json, "\"tag_name\"\\s*:\\s*\"v?([0-9][^\"]*)\"");
            return m.Success ? m.Groups[1].Value : null;
        }

        public static ReleaseInfo Parse(string json)
        {
            string tag = ParseTag(json);
            if (tag == null) return null;
            ReleaseInfo r = new ReleaseInfo();
            r.Version = tag;
            Match url = Regex.Match(json, "\"browser_download_url\"\\s*:\\s*\"([^\"]*/AA\\.exe)\"", RegexOptions.IgnoreCase);
            if (url.Success) r.DownloadUrl = url.Groups[1].Value;
            Match sha = Regex.Match(json, "SHA-256:\\s*`?([0-9a-fA-F]{64})");
            if (sha.Success) r.Sha256 = sha.Groups[1].Value.ToLowerInvariant();
            return r;
        }

        public static string FileSha256(string path)
        {
            using (SHA256 sha = SHA256.Create())
            using (FileStream fs = File.OpenRead(path))
            {
                StringBuilder sb = new StringBuilder();
                foreach (byte b in sha.ComputeHash(fs)) sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }

        public static bool IsNewer(string remote, string local)
        {
            try { return new Version(remote) > new Version(local); }
            catch { return false; }
        }
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

        // 0 -> "Nigdy", 45 -> "45 min", 120 -> "2 godz.", 90 -> "1 godz. 30 min"
        public static string AutoOffLabel(int minutes)
        {
            if (minutes == 0) return S("Never");
            if (minutes < 60) return minutes + " min";
            string hours = string.Format(S("HoursFmt"), minutes / 60);
            return minutes % 60 == 0 ? hours : hours + " " + (minutes % 60) + " min";
        }

        static readonly Dictionary<string, string[]> T = new Dictionary<string, string[]>
        {
            // --- menu w zasobniku ---
            { "MenuActive",    new[] { "Aktywne", "Active", "Aktiv" } },
            { "MenuMode",      new[] { "Tryb", "Mode", "Modus" } },
            { "MenuAutoOff",   new[] { "Auto-wyłącz po", "Auto-off after", "Automatisch aus nach" } },
            { "Never",         new[] { "Nigdy", "Never", "Nie" } },
            { "HoursFmt",      new[] { "{0} godz.", "{0} h", "{0} Std." } },
            { "MenuAutoOffCustom", new[] { "Własny czas / godzina...", "Custom time / until...", "Eigene Zeit / Uhrzeit..." } },
            { "AutoOffUntil",  new[] { "Do {0}", "Until {0}", "Bis {0}" } },
            { "AutoOffCustom", new[] { "Własny: {0}", "Custom: {0}", "Eigene: {0}" } },
            { "MenuPause",     new[] { "Wstrzymaj na", "Pause for", "Pausieren für" } },
            { "MenuPausedUntil", new[] { "Wstrzymane do {0}", "Paused until {0}", "Pausiert bis {0}" } },
            { "MenuResume",    new[] { "Wznów teraz", "Resume now", "Jetzt fortsetzen" } },
            { "MenuSchedule",  new[] { "Harmonogram ({0})", "Schedule ({0})", "Zeitplan ({0})" } },
            { "MenuApps",      new[] { "Włączaj przy programach", "Turn on with programs", "Bei Programmen einschalten" } },
            { "MenuIdle",      new[] { "Tryb bezczynności", "Idle-aware mode", "Leerlaufmodus" } },
            { "MenuJitter",    new[] { "Jitter interwału", "Interval jitter", "Intervall-Jitter" } },
            { "MenuAutoStart", new[] { "Uruchamiaj przy starcie", "Run at startup", "Mit Windows starten" } },
            { "MenuLanguage",  new[] { "Język (Language)", "Language", "Sprache (Language)" } },
            { "LangAuto",      new[] { "Automatycznie (system)", "Automatic (system)", "Automatisch (System)" } },
            { "MenuSettings",  new[] { "Ustawienia...", "Settings...", "Einstellungen..." } },
            { "MenuUpdates",   new[] { "Sprawdź aktualizacje", "Check for updates", "Nach Updates suchen" } },
            { "MenuAbout",     new[] { "O programie...", "About...", "Über AA..." } },
            { "MenuExit",      new[] { "Zakończ", "Exit", "Beenden" } },
            { "AutoStartFail", new[] { "Nie udało się zmienić autostartu.",
                                       "Could not change the startup setting.",
                                       "Autostart konnte nicht geändert werden." } },

            // --- podpowiedz przy ikonie (max 63 znaki razem) ---
            { "TipActive",     new[] { "AA - aktywne ({0})", "AA - active ({0})", "AA - aktiv ({0})" } },
            { "TipPaused",     new[] { "AA - wstrzymane ({0})", "AA - paused ({0})", "AA - pausiert ({0})" } },
            { "TipOffIn",      new[] { ", wyłączy się za {0}", ", off in {0}", ", aus in {0}" } },
            { "TipUntil",      new[] { ", do {0}", ", until {0}", ", bis {0}" } },
            { "TipPausedUntil", new[] { "AA - wstrzymane do {0}", "AA - paused until {0}", "AA - pausiert bis {0}" } },
            { "TipApp",        new[] { ", przez {0}", ", by {0}", ", durch {0}" } },
            { "TipSchedule",   new[] { ", harmonogram do {0}", ", schedule until {0}", ", Zeitplan bis {0}" } },
            { "TipBattery",    new[] { "AA - wstrzymane (bateria {0})", "AA - paused (battery {0})", "AA - pausiert (Akku {0})" } },
            { "BalloonBattery", new[] { "Zasilanie z baterii ({0}) - AA nie podtrzymuje ekranu do podłączenia ładowarki.",
                                        "On battery ({0}) - AA won't keep the screen on until you plug in.",
                                        "Akkubetrieb ({0}) - AA hält den Bildschirm erst nach dem Anschließen wieder wach." } },
            { "BalloonPower",  new[] { "Ładowarka podłączona - AA znowu podtrzymuje ekran.",
                                       "Plugged in - AA keeps the screen on again.",
                                       "Netzteil angeschlossen - AA hält den Bildschirm wieder wach." } },
            { "BalloonPauseEnd", new[] { "Koniec przerwy - AA znowu aktywne.", "Pause over - AA is active again.", "Pause vorbei - AA ist wieder aktiv." } },

            // --- dymki ---
            { "BalloonOn",     new[] { "AA aktywne", "AA active", "AA aktiv" } },
            { "BalloonOff",    new[] { "AA wstrzymane", "AA paused", "AA pausiert" } },
            { "BalloonAutoOff", new[] { "Minął czas auto-wyłączenia - AA wstrzymane.",
                                        "Auto-off time reached - AA paused.",
                                        "Abschaltzeit erreicht - AA pausiert." } },
            { "HotkeyBusy",    new[] { "Skrót {0} jest zajęty przez inny program. Zmień go w ustawieniach.",
                                       "The shortcut {0} is already used by another program. Change it in Settings.",
                                       "Die Tastenkombination {0} wird bereits von einem anderen Programm verwendet. Ändern Sie sie in den Einstellungen." } },

            // --- aktualizacje ---
            { "UpdateBalloon", new[] { "Dostępna nowa wersja {0}. Kliknij, aby zaktualizować.",
                                       "Version {0} is available. Click to update.",
                                       "Version {0} ist verfügbar. Klicken Sie zum Aktualisieren." } },
            { "UpdateAsk",     new[] { "Dostępna jest nowa wersja {0} (masz {1}).\n\nZaktualizować teraz? AA pobierze nową wersję i uruchomi się ponownie.",
                                       "A new version {0} is available (you have {1}).\n\nUpdate now? AA will download the new version and restart.",
                                       "Eine neue Version {0} ist verfügbar (installiert: {1}).\n\nJetzt aktualisieren? AA lädt die neue Version herunter und startet neu." } },
            { "UpdateDownloading", new[] { "Pobieranie wersji {0}...", "Downloading version {0}...", "Version {0} wird heruntergeladen..." } },
            { "UpdateDone",    new[] { "Zaktualizowano AA do wersji {0}.", "AA has been updated to version {0}.", "AA wurde auf Version {0} aktualisiert." } },
            { "UpdateFailed",  new[] { "Nie udało się zainstalować aktualizacji ({0}).\n\nOtworzyć stronę pobierania, aby zaktualizować ręcznie?",
                                       "Could not install the update ({0}).\n\nOpen the download page to update manually?",
                                       "Das Update konnte nicht installiert werden ({0}).\n\nDownload-Seite öffnen, um manuell zu aktualisieren?" } },
            { "UpdateFailedBalloon", new[] { "Automatyczna aktualizacja nie powiodła się. Kliknij, aby pobrać ręcznie.",
                                             "Automatic update failed. Click to download manually.",
                                             "Automatisches Update fehlgeschlagen. Klicken Sie zum manuellen Download." } },
            { "UpdateNone",    new[] { "Masz najnowszą wersję ({0}).",
                                       "You have the latest version ({0}).",
                                       "Sie haben die neueste Version ({0})." } },
            { "UpdateError",   new[] { "Nie udało się sprawdzić aktualizacji. Sprawdź połączenie z internetem.",
                                       "Could not check for updates. Check your internet connection.",
                                       "Updates konnten nicht geprüft werden. Bitte Internetverbindung prüfen." } },

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
            { "ChkUpdates",    new[] { "Sprawdzaj aktualizacje automatycznie", "Check for updates automatically", "Automatisch nach Updates suchen" } },
            { "ChkPortable",   new[] { "Tryb przenośny (ustawienia w pliku obok AA.exe)",
                                       "Portable mode (settings stored next to AA.exe)",
                                       "Portabler Modus (Einstellungen neben AA.exe)" } },
            { "PortableFail",  new[] { "Nie udało się zmienić trybu przenośnego: {0}",
                                       "Could not change portable mode: {0}",
                                       "Portabler Modus konnte nicht geändert werden: {0}" } },
            { "AboutPortable", new[] { " (tryb przenośny)", " (portable)", " (portabel)" } },
            { "ChkAutoUpdate", new[] { "Instaluj aktualizacje automatycznie", "Install updates automatically", "Updates automatisch installieren" } },
            { "LblLanguage",   new[] { "Język:", "Language:", "Sprache:" } },
            { "ChkHotkey",     new[] { "Skrót klawiszowy:", "Keyboard shortcut:", "Tastenkombination:" } },
            { "HotkeyNone",    new[] { "(brak)", "(none)", "(keine)" } },
            { "HotkeyHint",    new[] { "Kliknij w pole i naciśnij kombinację (Ctrl/Alt + klawisz). Włącza/wyłącza AA.",
                                       "Click the box and press a combination (Ctrl/Alt + key). Toggles AA on/off.",
                                       "Ins Feld klicken und Kombination drücken (Strg/Alt + Taste). Schaltet AA ein/aus." } },
            { "HotkeyAltGr",   new[] { "Uwaga: {0} działa jak AltGr+{1} - zablokuje pisanie znaku z tym klawiszem (np. ą, ę).",
                                       "Note: {0} acts like AltGr+{1} on some keyboards and will block typing that character.",
                                       "Achtung: {0} entspricht AltGr+{1} und blockiert die Eingabe dieses Zeichens (z. B. @, €)." } },
            { "Ok",            new[] { "OK", "OK", "OK" } },
            { "Cancel",        new[] { "Anuluj", "Cancel", "Abbrechen" } },

            // --- zakladka Automatyka ---
            { "TabGeneral",    new[] { "Ogólne", "General", "Allgemein" } },
            { "TabAutomation", new[] { "Automatyka", "Automation", "Automatik" } },
            { "ChkSchedule",   new[] { "Harmonogram - aktywne tylko w wybrane dni i godziny",
                                       "Schedule - active only on selected days and hours",
                                       "Zeitplan - nur an ausgewählten Tagen und Uhrzeiten aktiv" } },
            { "Days",          new[] { "Pn,Wt,Śr,Cz,Pt,So,Nd", "Mo,Tu,We,Th,Fr,Sa,Su", "Mo,Di,Mi,Do,Fr,Sa,So" } },
            { "LblFrom",       new[] { "Od:", "From:", "Von:" } },
            { "LblTo",         new[] { "Do:", "To:", "Bis:" } },
            { "ScheduleHint",  new[] { "Poza tymi godzinami AA się wstrzymuje. Ręczne włączenie/wyłączenie obowiązuje do następnej zmiany harmonogramu.",
                                       "Outside these hours AA pauses. Turning AA on/off manually lasts until the next schedule change.",
                                       "Außerhalb dieser Zeiten pausiert AA. Manuelles Ein-/Ausschalten gilt bis zur nächsten Zeitplanänderung." } },
            { "ChkApps",       new[] { "Włączaj AA, gdy działa któryś z programów:",
                                       "Turn AA on while one of these programs is running:",
                                       "AA einschalten, solange eines dieser Programme läuft:" } },
            { "BtnPickApps",   new[] { "Dodaj z uruchomionych...", "Add from running...", "Aus laufenden hinzufügen..." } },
            { "AppsHint",      new[] { "Nazwy procesów bez .exe, po jednym w linii (np. POWERPNT, Teams, vlc). Po zamknięciu programu AA wraca do poprzedniego stanu.",
                                       "Process names without .exe, one per line (e.g. POWERPNT, Teams, vlc). When the program closes, AA returns to its previous state.",
                                       "Prozessnamen ohne .exe, einer pro Zeile (z. B. POWERPNT, Teams, vlc). Nach dem Beenden kehrt AA zum vorherigen Zustand zurück." } },
            { "ChkBattery",    new[] { "Wstrzymuj przy zasilaniu z baterii", "Pause when running on battery", "Im Akkubetrieb pausieren" } },
            { "LblBatteryWhen", new[] { "Kiedy:", "When:", "Wann:" } },
            { "BatteryAlways", new[] { "zawsze na baterii", "always on battery", "immer im Akkubetrieb" } },
            { "BatteryBelow",  new[] { "poniżej {0}% baterii", "below {0}% battery", "unter {0} % Akku" } },
            { "BatteryHint",   new[] { "Na baterii AA nie podtrzymuje ekranu (pomarańczowa kropka) i wraca samo po podłączeniu ładowarki.",
                                       "On battery AA stops keeping the screen on (orange dot) and resumes when you plug in.",
                                       "Im Akkubetrieb hält AA den Bildschirm nicht wach (oranger Punkt) und macht nach dem Anschließen weiter." } },
            { "PickTitle",     new[] { "AA - Wybierz programy", "AA - Choose programs", "AA - Programme auswählen" } },
            { "PickHint",      new[] { "Zaznacz programy (Ctrl = kilka naraz):", "Select programs (Ctrl = several):", "Programme auswählen (Strg = mehrere):" } },
            { "Add",           new[] { "Dodaj", "Add", "Hinzufügen" } },

            // --- okno wlasnego czasu auto-wylaczenia ---
            { "AutoOffTitle",  new[] { "AA - Auto-wyłączenie", "AA - Auto-off", "AA - Automatisch aus" } },
            { "RbAfter",       new[] { "Wyłącz po (min):", "Turn off after (min):", "Ausschalten nach (Min.):" } },
            { "RbAt",          new[] { "Wyłącz o godzinie:", "Turn off at:", "Ausschalten um:" } },
            { "AtHint",        new[] { "Jeśli ta godzina już minęła, AA wyłączy się jutro.",
                                       "If that time has already passed today, AA turns off tomorrow.",
                                       "Ist die Uhrzeit heute schon vorbei, schaltet AA morgen aus." } },

            // --- okno o programie ---
            { "AboutTitle",    new[] { "O programie", "About", "Über" } },
            { "AboutDesc",     new[] { "Utrzymuje komputer aktywnym - zapobiega wygaszeniu i blokadzie ekranu.",
                                       "Keeps your computer awake - prevents the screen from turning off or locking.",
                                       "Hält den Computer wach - verhindert das Abschalten und Sperren des Bildschirms." } },
            { "AboutVersion",  new[] { "Wersja:", "Version:", "Version:" } },
            { "AboutAuthor",   new[] { "Autor:", "Author:", "Autor:" } },
            { "AboutWebsite",  new[] { "Strona WWW:", "Website:", "Webseite:" } },
            { "AboutLicense",  new[] { "Licencja:", "License:", "Lizenz:" } },
            { "AboutLicenseText", new[] { "MIT (darmowy, open source)", "MIT (free, open source)", "MIT (kostenlos, Open Source)" } },
            { "AboutSource",   new[] { "Kod źródłowy:", "Source code:", "Quellcode:" } },
            { "AboutSettings", new[] { "Plik ustawień:", "Settings file:", "Einstellungsdatei:" } },
            { "Close",         new[] { "Zamknij", "Close", "Schließen" } },
        };
    }

    // ---------------------------------------------------------------
    // Skrot klawiszowy zapisywany jako tekst, np. "Ctrl+Alt+K".
    // ---------------------------------------------------------------
    static class HotkeyText
    {
        // Klawisze, ktore z Ctrl+Alt (= AltGr) daja znak w ukladzie polskim/niemieckim.
        static readonly Keys[] AltGrKeys = {
            Keys.A, Keys.C, Keys.E, Keys.L, Keys.N, Keys.O, Keys.S, Keys.X, Keys.Z, Keys.Q, Keys.M,
            Keys.D2, Keys.D3, Keys.D7, Keys.D8, Keys.D9, Keys.D0 };

        public static bool IsValid(Keys mods, Keys key)
        {
            if (key == Keys.None || IsModifierKey(key)) return false;
            return (mods & (Keys.Control | Keys.Alt)) != 0;
        }

        public static bool IsModifierKey(Keys key)
        {
            return key == Keys.ControlKey || key == Keys.ShiftKey || key == Keys.Menu ||
                   key == Keys.LControlKey || key == Keys.RControlKey ||
                   key == Keys.LShiftKey || key == Keys.RShiftKey ||
                   key == Keys.LMenu || key == Keys.RMenu || key == Keys.LWin || key == Keys.RWin;
        }

        public static bool ConflictsWithAltGr(Keys mods, Keys key)
        {
            bool ctrlAlt = (mods & Keys.Control) != 0 && (mods & Keys.Alt) != 0;
            return ctrlAlt && Array.IndexOf(AltGrKeys, key) >= 0;
        }

        public static string KeyName(Keys key)
        {
            if (key >= Keys.D0 && key <= Keys.D9) return ((int)(key - Keys.D0)).ToString();
            return key.ToString();
        }

        public static string Format(Keys mods, Keys key)
        {
            if (key == Keys.None) return "";
            StringBuilder sb = new StringBuilder();
            if ((mods & Keys.Control) != 0) sb.Append("Ctrl+");
            if ((mods & Keys.Alt) != 0) sb.Append("Alt+");
            if ((mods & Keys.Shift) != 0) sb.Append("Shift+");
            sb.Append(KeyName(key));
            return sb.ToString();
        }

        public static bool TryParse(string text, out Keys mods, out Keys key)
        {
            mods = Keys.None; key = Keys.None;
            if (string.IsNullOrEmpty(text)) return false;
            string[] parts = text.Split('+');
            for (int i = 0; i < parts.Length; i++)
            {
                string p = parts[i].Trim();
                if (i < parts.Length - 1)
                {
                    switch (p.ToLowerInvariant())
                    {
                        case "ctrl": mods |= Keys.Control; break;
                        case "alt": mods |= Keys.Alt; break;
                        case "shift": mods |= Keys.Shift; break;
                        default: return false;
                    }
                }
                else if (p.Length == 1 && char.IsDigit(p[0]))
                {
                    key = Keys.D0 + (p[0] - '0');
                }
                else
                {
                    try { key = (Keys)Enum.Parse(typeof(Keys), p, true); }
                    catch { return false; }
                }
            }
            return IsValid(mods, key);
        }
    }

    // Niewidoczne okno (message-only) odbierajace globalny skrot klawiszowy.
    class HotkeyWindow : NativeWindow, IDisposable
    {
        [DllImport("user32.dll", SetLastError = true)]
        static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
        [DllImport("user32.dll", SetLastError = true)]
        static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        const int WM_HOTKEY = 0x0312;
        const int HotkeyId = 1;
        const uint MOD_ALT = 0x1, MOD_CONTROL = 0x2, MOD_SHIFT = 0x4, MOD_NOREPEAT = 0x4000;

        bool registered;
        public event EventHandler Pressed;

        public HotkeyWindow()
        {
            CreateParams cp = new CreateParams();
            cp.Parent = new IntPtr(-3); // HWND_MESSAGE
            CreateHandle(cp);
        }

        public bool Register(Keys mods, Keys key)
        {
            Unregister();
            uint m = MOD_NOREPEAT;
            if ((mods & Keys.Control) != 0) m |= MOD_CONTROL;
            if ((mods & Keys.Alt) != 0) m |= MOD_ALT;
            if ((mods & Keys.Shift) != 0) m |= MOD_SHIFT;
            registered = RegisterHotKey(Handle, HotkeyId, m, (uint)key);
            return registered;
        }

        public void Unregister()
        {
            if (registered) { UnregisterHotKey(Handle, HotkeyId); registered = false; }
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_HOTKEY && m.WParam.ToInt32() == HotkeyId && Pressed != null)
                Pressed(this, EventArgs.Empty);
            base.WndProc(ref m);
        }

        public void Dispose()
        {
            Unregister();
            DestroyHandle();
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
        public string Language = "Auto";       // Auto | pl | en | de
        public bool CheckUpdates = true;
        public bool AutoUpdate = true;
        public bool HotkeyEnabled = true;
        public string Hotkey = "Ctrl+Alt+K";
        public bool ScheduleEnabled = false;
        public string ScheduleDays = "1111100";  // pon..niedz, 1 = aktywny dzien
        public int ScheduleStart = 8 * 60;       // minuty od polnocy
        public int ScheduleEnd = 17 * 60;
        public bool AppsEnabled = false;
        public string Apps = "";                 // nazwy procesow oddzielone ';'
        public bool BatteryPause = false;
        public int BatteryThreshold = 0;         // 0 = zawsze na baterii, inaczej ponizej X%

        // Czy bateria blokuje podtrzymywanie. percent < 0 lub > 100 = nieznany poziom.
        public static bool BatteryBlocks(bool enabled, int threshold, bool hasBattery, bool onBattery, int percent)
        {
            if (!enabled || !hasBattery || !onBattery) return false;
            if (threshold <= 0) return true;
            return percent >= 0 && percent <= 100 && percent < threshold;
        }

        public static string FormatTime(int minutes)
        {
            return string.Format("{0:00}:{1:00}", minutes / 60, minutes % 60);
        }

        static bool TryParseTime(string v, out int minutes)
        {
            minutes = 0;
            string[] p = v.Split(':');
            int hh, mm;
            if (p.Length != 2 || !int.TryParse(p[0], out hh) || !int.TryParse(p[1], out mm)) return false;
            if (hh < 0 || hh > 23 || mm < 0 || mm > 59) return false;
            minutes = hh * 60 + mm;
            return true;
        }

        // "POWERPNT.exe; Teams\nvlc" -> { "POWERPNT", "Teams", "vlc" }
        public static string[] ParseApps(string text)
        {
            List<string> list = new List<string>();
            foreach (string part in (text ?? "").Split(new char[] { ';', ',', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string n = part.Trim();
                if (n.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) n = n.Substring(0, n.Length - 4);
                if (n.Length > 0 && !list.Exists(delegate(string x) { return string.Equals(x, n, StringComparison.OrdinalIgnoreCase); }))
                    list.Add(n);
            }
            return list.ToArray();
        }

        // Czy chwila 'now' miesci sie w harmonogramie. Gdy koniec <= start, okno trwa
        // przez polnoc i nalezy do dnia, w ktorym sie zaczelo (np. pt 22:00 - sob 06:00).
        public bool IsInSchedule(DateTime now)
        {
            int t = now.Hour * 60 + now.Minute;
            int day = ((int)now.DayOfWeek + 6) % 7; // 0 = poniedzialek
            int prev = (day + 6) % 7;
            if (ScheduleStart < ScheduleEnd)
                return DayOn(day) && t >= ScheduleStart && t < ScheduleEnd;
            if (ScheduleStart == ScheduleEnd)
                return DayOn(day);
            return (DayOn(day) && t >= ScheduleStart) || (DayOn(prev) && t < ScheduleEnd);
        }

        public bool DayOn(int day)
        {
            return ScheduleDays != null && day < ScheduleDays.Length && ScheduleDays[day] == '1';
        }

        static string Dir()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AA");
        }

        // Tryb przenosny: settings.ini obok AA.exe ma pierwszenstwo przed %AppData%\AA.
        public static string PortablePath()
        {
            return Path.Combine(Path.GetDirectoryName(Application.ExecutablePath), "settings.ini");
        }

        public static bool IsPortable()
        {
            try { return File.Exists(PortablePath()); }
            catch { return false; }
        }

        public static string FilePath()
        {
            return IsPortable() ? PortablePath() : Path.Combine(Dir(), "settings.ini");
        }

        // Wlacza/wylacza tryb przenosny (tworzy/usuwa settings.ini obok exe);
        // zawartosc zapisuje potem zwykle Save(). Zwraca komunikat bledu albo null.
        public static string SetPortable(bool on)
        {
            try
            {
                if (on && !IsPortable()) File.WriteAllText(PortablePath(), "");
                if (!on && IsPortable()) File.Delete(PortablePath());
                return null;
            }
            catch (Exception ex) { return ex.Message; }
        }

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
                        Keys hm, hk;
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
                            case "CheckUpdates": s.CheckUpdates = ParseBool(v, s.CheckUpdates); break;
                            case "AutoUpdate": s.AutoUpdate = ParseBool(v, s.AutoUpdate); break;
                            case "HotkeyEnabled": s.HotkeyEnabled = ParseBool(v, s.HotkeyEnabled); break;
                            case "Hotkey":
                                if (HotkeyText.TryParse(v, out hm, out hk)) s.Hotkey = HotkeyText.Format(hm, hk);
                                break;
                            case "ScheduleEnabled": s.ScheduleEnabled = ParseBool(v, s.ScheduleEnabled); break;
                            case "ScheduleDays":
                                if (Regex.IsMatch(v, "^[01]{7}$")) s.ScheduleDays = v;
                                break;
                            case "ScheduleStart":
                                if (TryParseTime(v, out n)) s.ScheduleStart = n;
                                break;
                            case "ScheduleEnd":
                                if (TryParseTime(v, out n)) s.ScheduleEnd = n;
                                break;
                            case "AppsEnabled": s.AppsEnabled = ParseBool(v, s.AppsEnabled); break;
                            case "Apps": s.Apps = string.Join(";", ParseApps(v)); break;
                            case "BatteryPause": s.BatteryPause = ParseBool(v, s.BatteryPause); break;
                            case "BatteryThreshold":
                                if (int.TryParse(v, out n) && n >= 0 && n < 100) s.BatteryThreshold = n;
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
                string path = FilePath();
                string dir = Path.GetDirectoryName(path);
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
                sb.AppendLine("CheckUpdates=" + CheckUpdates);
                sb.AppendLine("AutoUpdate=" + AutoUpdate);
                sb.AppendLine("HotkeyEnabled=" + HotkeyEnabled);
                sb.AppendLine("Hotkey=" + Hotkey);
                sb.AppendLine("ScheduleEnabled=" + ScheduleEnabled);
                sb.AppendLine("ScheduleDays=" + ScheduleDays);
                sb.AppendLine("ScheduleStart=" + FormatTime(ScheduleStart));
                sb.AppendLine("ScheduleEnd=" + FormatTime(ScheduleEnd));
                sb.AppendLine("AppsEnabled=" + AppsEnabled);
                sb.AppendLine("Apps=" + Apps);
                sb.AppendLine("BatteryPause=" + BatteryPause);
                sb.AppendLine("BatteryThreshold=" + BatteryThreshold);
                File.WriteAllText(path, sb.ToString());
            }
            catch { }
        }

        public Settings Clone() { return (Settings)this.MemberwiseClone(); }

        // Argumenty wiersza polecen nadpisuja ustawienia na starcie:
        //   -Mode Display|Input|Both  -IntervalSeconds <n>  -Active | -Inactive
        // Akceptowane prefiksy: -, --, /; wielkosc liter bez znaczenia.
        // Zwraca wymuszony stan startowy (-Active/-Inactive) albo null; StartActive sie nie zmienia.
        public bool? ApplyArgs(string[] args)
        {
            bool? forced = null;
            if (args == null) return forced;
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
                    case "active":
                        forced = true;
                        break;
                    case "inactive":
                        forced = false;
                        break;
                }
            }
            return forced;
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
        Timer timer;       // interwal podtrzymywania (IntervalSeconds)
        Timer clockTimer;  // co 15 s: auto-wylaczenie, podpowiedz, aktualizacje
        HotkeyWindow hotkey;
        Random rng = new Random();

        bool active;
        DateTime activationTime;
        DateTime? untilTime;       // "wylacz o godzinie" - tylko do konca sesji, nie zapisywane

        DateTime nextUpdateCheck;
        bool updateCheckRunning, updateInstalling;
        string notifiedVersion;    // zeby nie pokazywac dymka o tej samej wersji wiele razy
        Action balloonAction;      // co zrobic po kliknieciu w dymek (null = nic)
        bool oldExePending;        // AA.exe.old po aktualizacji - do usuniecia

        // Automatyka reaguje tylko na ZMIANY (start/koniec harmonogramu, start/zamkniecie
        // programu, koniec pauzy), wiec reczne przelaczenie obowiazuje do nastepnej zmiany.
        DateTime? pauseUntil;      // "Wstrzymaj na X" - nic nie wlacza AA przed tym czasem
        bool automationReady;      // false = przy najblizszym sprawdzeniu ustaw stan wg harmonogramu
        bool lastInSchedule, lastAppRunning;
        bool appHolding;           // AA wlaczone przez program z listy
        bool activeBeforeApp;      // stan do przywrocenia po zamknieciu programu
        string triggerApp;         // dzialajacy program z listy (do podpowiedzi)

        // Bateria nie zmienia stanu "Aktywne" - tylko wstrzymuje podtrzymywanie (pomaranczowa kropka).
        bool batteryBlocked;
        string batteryText = "?";  // np. "18%" - do podpowiedzi i dymkow

        Icon iconActive, iconInactive, iconBattery;
        ToolStripMenuItem miToggle, miPauseMenu, miResume, miModeMenu, miAutoOffMenu, miAutoOffCustom;
        ToolStripMenuItem miSchedule, miApps, miAutoStart, miIdle, miJitter;
        ToolStripMenuItem miLanguageMenu, miSettings, miUpdates, miAbout, miExit;
        int[] autoOffValues = new int[] { 0, 30, 60, 120, 240 };
        int[] pauseValues = new int[] { 15, 30, 60, 120 };

        // otwarte okna (zeby nie otwierac drugiej kopii tego samego okna)
        SettingsForm settingsForm;
        AboutForm aboutForm;
        AutoOffForm autoOffForm;

        public TrayContext(Settings loaded, string[] args)
        {
            settings = loaded;
            bool? forced = settings.ApplyArgs(args);
            active = forced ?? settings.StartActive;

            iconActive   = MakeDot(Color.FromArgb(40, 180, 70));
            iconInactive = MakeDot(Color.FromArgb(150, 150, 150));
            iconBattery  = MakeDot(Color.FromArgb(235, 150, 30));

            BuildMenu();

            notify = new NotifyIcon();
            notify.ContextMenuStrip = BuildContextMenu();
            notify.Visible = true;
            notify.DoubleClick += delegate { SetActiveManual(!active); };
            notify.BalloonTipClicked += delegate
            {
                Action a = balloonAction;
                balloonAction = null;
                if (a != null) a();
            };

            timer = new Timer();
            timer.Tick += delegate { OnTick(); };
            ApplyTimerBase();
            timer.Start();

            clockTimer = new Timer();
            clockTimer.Interval = 15000;
            clockTimer.Tick += delegate { OnClock(); };
            clockTimer.Start();
            nextUpdateCheck = DateTime.Now.AddSeconds(30);

            hotkey = new HotkeyWindow();
            hotkey.Pressed += delegate
            {
                SetActiveManual(!active);
                ShowBalloon(active ? L.S("BalloonOn") : L.S("BalloonOff"), ToolTipIcon.None, null, 1500);
            };

            if (active) { activationTime = DateTime.Now; }
            // Stan wymuszony argumentem (np. po aktualizacji) ma pierwszenstwo przed harmonogramem.
            automationReady = forced.HasValue;
            lastInSchedule = settings.ScheduleEnabled && settings.IsInSchedule(DateTime.Now);
            ApplyTexts();
            ApplyHotkey(true);
            UpdateBattery(false);
            UpdateUi();
            DoKeepAwake(true);
            EvaluateAutomation();

            oldExePending = File.Exists(OldExePath());
            DeleteOldExe();
            if (Array.Exists(args, delegate(string a) { return a.TrimStart('-', '/').ToLowerInvariant() == "updated"; }))
                ShowBalloon(string.Format(L.S("UpdateDone"), AppInfo.Version), ToolTipIcon.Info, null, 5000);
        }

        // ---- budowa menu ----
        ContextMenuStrip menu;

        void BuildMenu()
        {
            miToggle = new ToolStripMenuItem();
            miToggle.CheckOnClick = true;
            miToggle.Click += delegate { SetActiveManual(miToggle.Checked); };

            miPauseMenu = new ToolStripMenuItem();
            foreach (int m in pauseValues)
            {
                ToolStripMenuItem item = new ToolStripMenuItem();
                item.Tag = m;
                item.Click += delegate(object s, EventArgs e) { PauseFor((int)((ToolStripMenuItem)s).Tag); };
                miPauseMenu.DropDownItems.Add(item);
            }
            miPauseMenu.DropDownItems.Add(new ToolStripSeparator());
            miResume = new ToolStripMenuItem();
            miResume.Click += delegate { SetActiveManual(true); };
            miPauseMenu.DropDownItems.Add(miResume);

            miSchedule = new ToolStripMenuItem();
            miSchedule.CheckOnClick = true;
            miSchedule.Click += delegate
            {
                settings.ScheduleEnabled = miSchedule.Checked;
                settings.Save();
                automationReady = false; // od razu ustaw stan wg harmonogramu
                EvaluateAutomation();
                UpdateUi();
            };

            miApps = new ToolStripMenuItem();
            miApps.CheckOnClick = true;
            miApps.Click += delegate
            {
                if (miApps.Checked && Settings.ParseApps(settings.Apps).Length == 0)
                {
                    // pusta lista - najpierw trzeba wybrac programy
                    miApps.Checked = false;
                    OpenSettings(1);
                    return;
                }
                settings.AppsEnabled = miApps.Checked;
                settings.Save();
                EvaluateAutomation();
                UpdateUi();
            };

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
                    SetAutoOffMinutes((int)((ToolStripMenuItem)s).Tag);
                };
                miAutoOffMenu.DropDownItems.Add(item);
            }
            miAutoOffMenu.DropDownItems.Add(new ToolStripSeparator());
            miAutoOffCustom = new ToolStripMenuItem();
            miAutoOffCustom.Click += delegate { OpenAutoOff(); };
            miAutoOffMenu.DropDownItems.Add(miAutoOffCustom);

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
                if (!ok) ShowBalloon(L.S("AutoStartFail"), ToolTipIcon.Warning, null, 4000);
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

            miUpdates = new ToolStripMenuItem();
            miUpdates.Click += delegate { CheckForUpdates(true); };

            miAbout = new ToolStripMenuItem();
            miAbout.Click += delegate { OpenAbout(); };

            miExit = new ToolStripMenuItem();
            miExit.Click += delegate { ExitApp(); };
        }

        ContextMenuStrip BuildContextMenu()
        {
            menu = new ContextMenuStrip();
            menu.Items.Add(miToggle);
            menu.Items.Add(miPauseMenu);
            menu.Items.Add(miModeMenu);
            menu.Items.Add(miAutoOffMenu);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(miSchedule);
            menu.Items.Add(miApps);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(miIdle);
            menu.Items.Add(miJitter);
            menu.Items.Add(miAutoStart);
            menu.Items.Add(miLanguageMenu);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(miSettings);
            menu.Items.Add(miUpdates);
            menu.Items.Add(miAbout);
            menu.Items.Add(miExit);
            return menu;
        }

        // Ustawia teksty menu w biezacym jezyku (wywolywane tez po zmianie jezyka).
        void ApplyTexts()
        {
            miToggle.Text = L.S("MenuActive");
            foreach (ToolStripItem it in miPauseMenu.DropDownItems)
                if (it.Tag is int) it.Text = L.AutoOffLabel((int)it.Tag);
            miResume.Text = L.S("MenuResume");
            miApps.Text = L.S("MenuApps");
            miModeMenu.Text = L.S("MenuMode");
            miAutoOffMenu.Text = L.S("MenuAutoOff");
            foreach (ToolStripItem it in miAutoOffMenu.DropDownItems)
                if (it.Tag is int) it.Text = L.AutoOffLabel((int)it.Tag);
            miIdle.Text = L.S("MenuIdle");
            miJitter.Text = L.S("MenuJitter");
            miAutoStart.Text = L.S("MenuAutoStart");
            miLanguageMenu.Text = L.S("MenuLanguage");
            foreach (ToolStripMenuItem it in miLanguageMenu.DropDownItems)
                it.Text = L.LanguageName((string)it.Tag);
            miSettings.Text = L.S("MenuSettings");
            miUpdates.Text = L.S("MenuUpdates");
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

        void ShowBalloon(string text, ToolTipIcon icon, Action onClick, int ms)
        {
            balloonAction = onClick;
            notify.ShowBalloonTip(ms, "AA", text, icon);
        }

        // ---- skrot klawiszowy ----
        void ApplyHotkey(bool reportFailure)
        {
            hotkey.Unregister();
            miToggle.ShortcutKeyDisplayString = "";
            Keys mods, key;
            if (!settings.HotkeyEnabled || !HotkeyText.TryParse(settings.Hotkey, out mods, out key)) return;
            if (hotkey.Register(mods, key))
                miToggle.ShortcutKeyDisplayString = settings.Hotkey;
            else if (reportFailure)
                ShowBalloon(string.Format(L.S("HotkeyBusy"), settings.Hotkey), ToolTipIcon.Warning, null, 5000);
        }

        // ---- auto-wylaczenie ----
        DateTime? OffDeadline()
        {
            if (untilTime.HasValue) return untilTime.Value;
            if (settings.AutoOffMinutes > 0) return activationTime.AddMinutes(settings.AutoOffMinutes);
            return null;
        }

        void SetAutoOffMinutes(int minutes)
        {
            untilTime = null;
            settings.AutoOffMinutes = minutes;
            settings.Save();
            if (active) activationTime = DateTime.Now;
            UpdateUi();
        }

        void OpenAutoOff()
        {
            if (autoOffForm != null) { autoOffForm.Activate(); return; }
            using (autoOffForm = new AutoOffForm(settings.AutoOffMinutes, untilTime))
            {
                if (autoOffForm.ShowDialog() == DialogResult.OK)
                {
                    DateTime? until = autoOffForm.UntilTime;
                    if (until.HasValue)
                    {
                        // "do godziny" zaklada, ze AA ma dzialac do tego czasu
                        if (!active || pauseUntil.HasValue) SetActiveManual(true);
                        untilTime = until;
                        UpdateUi();
                    }
                    else
                    {
                        SetAutoOffMinutes(autoOffForm.Minutes);
                    }
                }
            }
            autoOffForm = null;
        }

        // ---- aktualizacje ----
        void CheckForUpdates(bool manual)
        {
            if (updateCheckRunning) return;
            updateCheckRunning = true;
            try
            {
                ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072; // TLS 1.2
                WebClient wc = new WebClient();
                wc.Encoding = Encoding.UTF8;
                wc.Headers[HttpRequestHeader.UserAgent] = "AA-AlwaysAwake/" + AppInfo.Version;
                wc.Headers[HttpRequestHeader.Accept] = "application/vnd.github+json";
                wc.DownloadStringCompleted += delegate(object s, DownloadStringCompletedEventArgs e)
                {
                    updateCheckRunning = false;
                    ReleaseInfo info = (e.Error == null && !e.Cancelled) ? Updates.Parse(e.Result) : null;
                    wc.Dispose();
                    OnUpdateResult(info, manual);
                };
                wc.DownloadStringAsync(new Uri(AppInfo.LatestApiUrl));
            }
            catch
            {
                updateCheckRunning = false;
                OnUpdateResult(null, manual);
            }
        }

        void OnUpdateResult(ReleaseInfo info, bool manual)
        {
            if (info == null)
            {
                if (manual)
                    MessageBox.Show(L.S("UpdateError"), AppInfo.Name, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (!Updates.IsNewer(info.Version, AppInfo.Version))
            {
                if (manual)
                    MessageBox.Show(string.Format(L.S("UpdateNone"), AppInfo.Version), AppInfo.Name,
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (manual)
            {
                if (AskUpdate(info)) InstallUpdate(info, true);
            }
            else if (settings.AutoUpdate && !untilTime.HasValue && !pauseUntil.HasValue &&
                     settingsForm == null && aboutForm == null && autoOffForm == null)
            {
                // instalacja w tle; pomijana, gdy uzytkownik ma otwarte okno, "do godziny" albo pauze
                // (te stany nie przetrwalyby restartu)
                InstallUpdate(info, false);
            }
            else if (notifiedVersion != info.Version)
            {
                notifiedVersion = info.Version;
                ShowBalloon(string.Format(L.S("UpdateBalloon"), info.Version), ToolTipIcon.Info,
                    delegate { if (AskUpdate(info)) InstallUpdate(info, true); }, 10000);
            }
        }

        bool AskUpdate(ReleaseInfo info)
        {
            return MessageBox.Show(string.Format(L.S("UpdateAsk"), info.Version, AppInfo.Version), AppInfo.Name,
                MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes;
        }

        static string OldExePath() { return Application.ExecutablePath + ".old"; }

        void DeleteOldExe()
        {
            if (!oldExePending) return;
            try { File.Delete(OldExePath()); } catch { }
            oldExePending = File.Exists(OldExePath());
        }

        // Pobiera AA.exe z release'u, sprawdza SHA-256, podmienia plik i uruchamia nowa wersje.
        // Dzialajacego exe nie mozna nadpisac, ale mozna go przemianowac na AA.exe.old.
        void InstallUpdate(ReleaseInfo info, bool interactive)
        {
            if (updateInstalling) return;
            if (info.DownloadUrl == null || info.Sha256 == null)
            {
                UpdateFailed("release without AA.exe / SHA-256", interactive);
                return;
            }
            updateInstalling = true;
            string exe = Application.ExecutablePath;
            string tmp = exe + ".new";
            if (interactive)
                ShowBalloon(string.Format(L.S("UpdateDownloading"), info.Version), ToolTipIcon.Info, null, 3000);
            try
            {
                ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072; // TLS 1.2
                WebClient wc = new WebClient();
                wc.Headers[HttpRequestHeader.UserAgent] = "AA-AlwaysAwake/" + AppInfo.Version;
                wc.DownloadFileCompleted += delegate(object s, System.ComponentModel.AsyncCompletedEventArgs e)
                {
                    wc.Dispose();
                    string error = null;
                    try
                    {
                        if (e.Error != null) throw e.Error;
                        if (e.Cancelled) throw new Exception("cancelled");
                        if (Updates.FileSha256(tmp) != info.Sha256) throw new Exception("SHA-256 mismatch");
                        string old = OldExePath();
                        if (File.Exists(old)) File.Delete(old);
                        File.Move(exe, old);
                        try { File.Move(tmp, exe); }
                        catch { File.Move(old, exe); throw; }
                    }
                    catch (Exception ex) { error = ex.Message; }

                    if (error == null)
                    {
                        try { Process.Start(exe, "--updated " + (active ? "-Active" : "-Inactive")); }
                        catch (Exception ex) { error = ex.Message; }
                    }
                    if (error != null)
                    {
                        try { if (File.Exists(tmp)) File.Delete(tmp); } catch { }
                        updateInstalling = false;
                        UpdateFailed(error, interactive);
                        return;
                    }
                    ExitApp(); // nowa wersja czeka na zwolnienie mutexu
                };
                wc.DownloadFileAsync(new Uri(info.DownloadUrl), tmp);
            }
            catch (Exception ex)
            {
                updateInstalling = false;
                UpdateFailed(ex.Message, interactive);
            }
        }

        void UpdateFailed(string reason, bool interactive)
        {
            if (interactive)
            {
                if (MessageBox.Show(string.Format(L.S("UpdateFailed"), reason), AppInfo.Name,
                        MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
                    AppInfo.OpenUrl(AppInfo.ReleasesUrl);
            }
            else
            {
                ShowBalloon(L.S("UpdateFailedBalloon"), ToolTipIcon.Warning,
                    delegate { AppInfo.OpenUrl(AppInfo.ReleasesUrl); }, 10000);
            }
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
            DoKeepAwake(false);
            timer.Interval = JitterMs();
        }

        void OnClock()
        {
            if (active)
            {
                DateTime? deadline = OffDeadline();
                if (deadline.HasValue && DateTime.Now >= deadline.Value)
                {
                    SetActiveManual(false); // jak reczne wylaczenie - program z listy go nie cofnie
                    ShowBalloon(L.S("BalloonAutoOff"), ToolTipIcon.Info, null, 5000);
                }
            }
            EvaluateAutomation();
            UpdateBattery(true);
            UpdateUi();
            DeleteOldExe(); // zaraz po aktualizacji stary plik moze byc jeszcze zablokowany
            if (settings.CheckUpdates && DateTime.Now >= nextUpdateCheck)
            {
                nextUpdateCheck = DateTime.Now.AddHours(24);
                CheckForUpdates(false);
            }
        }

        // Sprawdza zasilanie; przy zmianie blokady zdejmuje/przywraca podtrzymywanie.
        void UpdateBattery(bool showBalloon)
        {
            PowerStatus ps = SystemInformation.PowerStatus;
            bool hasBattery = (ps.BatteryChargeStatus & BatteryChargeStatus.NoSystemBattery) == 0 &&
                              ps.BatteryChargeStatus != BatteryChargeStatus.Unknown;
            bool onBattery = ps.PowerLineStatus == PowerLineStatus.Offline;
            int percent = (int)Math.Round(ps.BatteryLifePercent * 100);
            batteryText = percent >= 0 && percent <= 100 ? percent + "%" : "?";

            bool blocked = Settings.BatteryBlocks(settings.BatteryPause, settings.BatteryThreshold,
                hasBattery, onBattery, percent);
            if (blocked == batteryBlocked) return;
            batteryBlocked = blocked;
            if (blocked)
            {
                ClearKeepAwake();
                if (showBalloon && active)
                    ShowBalloon(string.Format(L.S("BalloonBattery"), batteryText), ToolTipIcon.Info, null, 5000);
            }
            else
            {
                DoKeepAwake(true);
                if (showBalloon && active)
                    ShowBalloon(L.S("BalloonPower"), ToolTipIcon.Info, null, 4000);
            }
            UpdateUi();
        }

        void DoKeepAwake(bool immediate)
        {
            if (!active || batteryBlocked) return;
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

        // Przelaczenie przez uzytkownika (menu, dwuklik, skrot): konczy pauze i "przejmuje"
        // stan od programu z listy - automatyka zareaguje dopiero na nastepna zmiane.
        void SetActiveManual(bool on)
        {
            pauseUntil = null;
            appHolding = false;
            SetActive(on);
        }

        void PauseFor(int minutes)
        {
            appHolding = false;
            SetActive(false);
            pauseUntil = DateTime.Now.AddMinutes(minutes);
            UpdateUi();
        }

        // ---- automatyka: harmonogram, programy, koniec pauzy ----
        void EvaluateAutomation()
        {
            DateTime now = DateTime.Now;
            bool inSchedule = settings.ScheduleEnabled && settings.IsInSchedule(now);
            string app = settings.AppsEnabled ? FindRunningApp() : null;
            bool appRunning = app != null;
            triggerApp = app;

            if (pauseUntil.HasValue)
            {
                if (now < pauseUntil.Value)
                {
                    lastInSchedule = inSchedule; lastAppRunning = appRunning;
                    return;
                }
                // koniec pauzy: wlacz, chyba ze harmonogram mowi "poza godzinami" i nic nie trzyma AA
                pauseUntil = null;
                automationReady = true;
                activeBeforeApp = settings.ScheduleEnabled ? inSchedule : true;
                appHolding = appRunning;
                bool resume = appRunning || activeBeforeApp;
                SetActive(resume);
                if (resume) ShowBalloon(L.S("BalloonPauseEnd"), ToolTipIcon.Info, null, 4000);
                lastInSchedule = inSchedule; lastAppRunning = appRunning;
                return;
            }

            if (!automationReady)
            {
                // start programu albo wlaczenie harmonogramu: stan wg biezacej godziny
                automationReady = true;
                if (settings.ScheduleEnabled && !appHolding && active != inSchedule) SetActive(inSchedule);
            }
            else if (settings.ScheduleEnabled && inSchedule != lastInSchedule)
            {
                // poczatek / koniec harmonogramu; gdy dziala program z listy, decyzja po jego zamknieciu
                if (appHolding) activeBeforeApp = inSchedule;
                else SetActive(inSchedule);
            }
            lastInSchedule = inSchedule;

            if (appRunning && !lastAppRunning)
            {
                activeBeforeApp = active;
                appHolding = true;
                if (!active) SetActive(true);
            }
            else if (!appRunning && lastAppRunning && appHolding)
            {
                appHolding = false;
                if (active != activeBeforeApp) SetActive(activeBeforeApp);
            }
            lastAppRunning = appRunning;
        }

        // Pierwszy dzialajacy program z listy (nazwa procesu) albo null.
        string FindRunningApp()
        {
            string[] names = Settings.ParseApps(settings.Apps);
            if (names.Length == 0) return null;
            HashSet<string> running = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (Process p in Process.GetProcesses())
            {
                try { running.Add(p.ProcessName); } catch { }
                p.Dispose();
            }
            foreach (string n in names)
                if (running.Contains(n)) return n;
            return null;
        }

        void SetActive(bool on)
        {
            active = on;
            if (on)
            {
                activationTime = DateTime.Now;
                if (untilTime.HasValue && untilTime.Value <= activationTime) untilTime = null;
                DoKeepAwake(true);
            }
            else
            {
                untilTime = null; // "do godziny" dotyczy tylko biezacej aktywacji
                ClearKeepAwake();
            }
            UpdateUi();
        }

        void UpdateUi()
        {
            notify.Icon = !active ? iconInactive : batteryBlocked ? iconBattery : iconActive;
            UpdateTooltip();
            miToggle.Checked = active;
            miPauseMenu.Text = pauseUntil.HasValue
                ? string.Format(L.S("MenuPausedUntil"), pauseUntil.Value.ToString("HH:mm"))
                : L.S("MenuPause");
            miResume.Visible = pauseUntil.HasValue;
            miSchedule.Text = string.Format(L.S("MenuSchedule"),
                Settings.FormatTime(settings.ScheduleStart) + "–" + Settings.FormatTime(settings.ScheduleEnd));
            miSchedule.Checked = settings.ScheduleEnabled;
            miApps.Checked = settings.AppsEnabled;
            miIdle.Checked = settings.IdleAware;
            miJitter.Checked = settings.Jitter;
            miAutoStart.Checked = AutoStart.IsEnabled();

            foreach (ToolStripMenuItem it in miModeMenu.DropDownItems)
                it.Checked = ((string)it.Tag == settings.Mode);

            bool preset = false;
            foreach (ToolStripItem it in miAutoOffMenu.DropDownItems)
            {
                ToolStripMenuItem mi = it as ToolStripMenuItem;
                if (mi == null || !(mi.Tag is int)) continue;
                mi.Checked = !untilTime.HasValue && (int)mi.Tag == settings.AutoOffMinutes;
                if (mi.Checked) preset = true;
            }
            miAutoOffCustom.Checked = !preset;
            if (untilTime.HasValue)
                miAutoOffCustom.Text = string.Format(L.S("AutoOffUntil"), untilTime.Value.ToString("HH:mm"));
            else if (!preset)
                miAutoOffCustom.Text = string.Format(L.S("AutoOffCustom"), L.AutoOffLabel(settings.AutoOffMinutes));
            else
                miAutoOffCustom.Text = L.S("MenuAutoOffCustom");

            foreach (ToolStripMenuItem it in miLanguageMenu.DropDownItems)
                it.Checked = ((string)it.Tag == settings.Language);
        }

        // NotifyIcon.Text ma limit 63 znakow (.NET Framework rzuca wyjatek powyzej).
        void UpdateTooltip()
        {
            string text;
            if (active && batteryBlocked)
            {
                text = string.Format(L.S("TipBattery"), batteryText);
            }
            else if (active)
            {
                text = string.Format(L.S("TipActive"), settings.Mode);
                if (untilTime.HasValue)
                {
                    text += string.Format(L.S("TipUntil"), untilTime.Value.ToString("HH:mm"));
                }
                else if (settings.AutoOffMinutes > 0)
                {
                    TimeSpan left = activationTime.AddMinutes(settings.AutoOffMinutes) - DateTime.Now;
                    int mins = (int)Math.Ceiling(Math.Max(0, left.TotalMinutes));
                    text += string.Format(L.S("TipOffIn"),
                        string.Format("{0}:{1:00}", mins / 60, mins % 60));
                }
                else if (appHolding && triggerApp != null)
                {
                    text += string.Format(L.S("TipApp"), triggerApp);
                }
                else if (settings.ScheduleEnabled && lastInSchedule)
                {
                    text += string.Format(L.S("TipSchedule"), Settings.FormatTime(settings.ScheduleEnd));
                }
            }
            else if (pauseUntil.HasValue)
            {
                text = string.Format(L.S("TipPausedUntil"), pauseUntil.Value.ToString("HH:mm"));
            }
            else
            {
                text = string.Format(L.S("TipPaused"), settings.Mode);
            }
            if (text.Length > 63) text = text.Substring(0, 63);
            notify.Text = text;
        }

        void OpenSettings() { OpenSettings(0); }

        void OpenSettings(int tab)
        {
            if (settingsForm != null) { settingsForm.Activate(); return; }
            // skrot nieaktywny podczas edycji - inaczej nie da sie go wpisac w pole
            hotkey.Unregister();
            using (settingsForm = new SettingsForm(settings.Clone(), AutoStart.IsEnabled(), tab))
            {
                if (settingsForm.ShowDialog() == DialogResult.OK)
                {
                    bool scheduleChanged = settings.ScheduleEnabled != settingsForm.Result.ScheduleEnabled ||
                        settings.ScheduleDays != settingsForm.Result.ScheduleDays ||
                        settings.ScheduleStart != settingsForm.Result.ScheduleStart ||
                        settings.ScheduleEnd != settingsForm.Result.ScheduleEnd;
                    settings = settingsForm.Result;
                    if (settingsForm.PortableEnabled != Settings.IsPortable())
                    {
                        string err = Settings.SetPortable(settingsForm.PortableEnabled);
                        if (err != null)
                            ShowBalloon(string.Format(L.S("PortableFail"), err), ToolTipIcon.Warning, null, 6000);
                    }
                    settings.Save(); // trafia do pliku obok exe albo do %AppData%, zaleznie od trybu
                    AutoStart.Set(settingsForm.AutoStartEnabled);
                    L.Init(settings.Language);
                    ApplyTexts();
                    ApplyTimerBase();
                    if (active) activationTime = DateTime.Now;
                    ClearKeepAwake();
                    if (scheduleChanged) automationReady = false; // nowy harmonogram obowiazuje od razu
                    EvaluateAutomation();
                    UpdateBattery(false);
                    UpdateUi();
                    DoKeepAwake(true);
                }
            }
            settingsForm = null;
            ApplyHotkey(true);
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
            clockTimer.Stop();
            hotkey.Dispose();
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
    // Pole do wpisania skrotu: kliknij i nacisnij kombinacje klawiszy.
    // Backspace/Delete czysci pole.
    // ---------------------------------------------------------------
    public class HotkeyBox : TextBox
    {
        public Keys Mods = Keys.None;
        public Keys Key = Keys.None;
        public event EventHandler HotkeyChanged;

        public HotkeyBox()
        {
            ReadOnly = true;
            BackColor = SystemColors.Window;
            ShortcutsEnabled = false;
        }

        public string Value { get { return HotkeyText.Format(Mods, Key); } }

        public void SetHotkey(Keys mods, Keys key)
        {
            Mods = mods; Key = key;
            Text = key == Keys.None ? L.S("HotkeyNone") : HotkeyText.Format(mods, key);
            if (HotkeyChanged != null) HotkeyChanged(this, EventArgs.Empty);
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            Keys key = keyData & Keys.KeyCode;
            Keys mods = keyData & Keys.Modifiers;

            // Tab / Enter / Esc bez modyfikatorow dzialaja normalnie w oknie
            if (mods == Keys.None && (key == Keys.Tab || key == Keys.Enter || key == Keys.Escape))
                return base.ProcessCmdKey(ref msg, keyData);

            if (mods == Keys.None && (key == Keys.Back || key == Keys.Delete))
            {
                SetHotkey(Keys.None, Keys.None);
                return true;
            }

            if (HotkeyText.IsModifierKey(key))
            {
                // podglad w trakcie wciskania, np. "Ctrl+Alt+..."
                Text = ((mods & Keys.Control) != 0 ? "Ctrl+" : "") +
                       ((mods & Keys.Alt) != 0 ? "Alt+" : "") +
                       ((mods & Keys.Shift) != 0 ? "Shift+" : "") + "...";
                return true;
            }

            if (HotkeyText.IsValid(mods, key)) SetHotkey(mods, key);
            else System.Media.SystemSounds.Beep.Play();
            return true;
        }

        protected override void OnLostFocus(EventArgs e)
        {
            base.OnLostFocus(e);
            SetHotkey(Mods, Key); // przywroc tekst, jesli zostal sam podglad "Ctrl+..."
        }
    }

    // ---------------------------------------------------------------
    // Okno Ustawienia: zakladki "Ogolne" i "Automatyka"
    // ---------------------------------------------------------------
    public class SettingsForm : Form
    {
        public Settings Result;
        public bool AutoStartEnabled;
        public bool PortableEnabled;

        ComboBox cmbMode, cmbAutoOff, cmbLanguage, cmbBattery;
        CheckBox chkPortable, chkBattery;
        int[] batteryValues = new int[] { 0, 50, 30, 20, 10 };
        NumericUpDown numInterval, numIdle;
        CheckBox chkJitter, chkIdle, chkStartActive, chkAutoStart, chkUpdates, chkAutoUpdate, chkHotkey;
        CheckBox chkSchedule, chkApps;
        CheckBox[] chkDays = new CheckBox[7];
        DateTimePicker dtFrom, dtTo;
        TextBox txtApps;
        Button btnPickApps;
        HotkeyBox hotkeyBox;
        Label hotkeyNote;
        List<int> autoOffValues = new List<int>(new int[] { 0, 30, 60, 120, 240 });

        const int x1 = 16, x2 = 210, w2 = 180, h = 26;
        const int pageWidth = x2 + w2 + x1;
        Control host; // biezaca zakladka, do ktorej trafiaja kontrolki

        public SettingsForm(Settings s, bool autoStart, int tab)
        {
            Result = s;
            AutoStartEnabled = autoStart;

            Text = L.S("SettingsTitle");
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterScreen;
            MaximizeBox = false; MinimizeBox = false;

            TabControl tabs = new TabControl();
            TabPage pageGeneral = new TabPage(L.S("TabGeneral"));
            TabPage pageAuto = new TabPage(L.S("TabAutomation"));
            tabs.TabPages.Add(pageGeneral);
            tabs.TabPages.Add(pageAuto);

            host = pageGeneral;
            int yGeneral = BuildGeneral(s, autoStart);
            host = pageAuto;
            int yAuto = BuildAutomation(s);

            int pageHeight = Math.Max(yGeneral, yAuto) + 8;
            tabs.SetBounds(8, 8, pageWidth + 8, pageHeight + 26);
            Controls.Add(tabs);
            tabs.SelectedIndex = Math.Max(0, Math.Min(tab, 1));

            int y = tabs.Bottom + 12;
            Button ok = new Button();
            ok.Text = L.S("Ok"); ok.DialogResult = DialogResult.OK;
            ok.SetBounds(tabs.Right - 190, y, 90, 28);
            ok.Click += delegate { ReadBack(); };
            Controls.Add(ok);

            Button cancel = new Button();
            cancel.Text = L.S("Cancel"); cancel.DialogResult = DialogResult.Cancel;
            cancel.SetBounds(tabs.Right - 90, y, 90, 28);
            Controls.Add(cancel);

            AcceptButton = ok; CancelButton = cancel;
            ClientSize = new Size(tabs.Right + 8, y + 28 + 12);
        }

        // ---- zakladka Ogolne; zwraca wysokosc zawartosci ----
        int BuildGeneral(Settings s, bool autoStart)
        {
            int y = 14;

            AddLabel(L.S("LblMode"), y);
            cmbMode = new ComboBox();
            cmbMode.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbMode.Items.AddRange(new object[] { "Display", "Input", "Both" });
            cmbMode.SelectedItem = s.Mode;
            cmbMode.SetBounds(x2, y - 3, w2, h); host.Controls.Add(cmbMode); y += h + 2;

            AddHint(L.S("ModeHint"), x1, ref y, 20);

            AddLabel(L.S("LblInterval"), y);
            numInterval = new NumericUpDown();
            numInterval.Minimum = 5; numInterval.Maximum = 3600;
            numInterval.Value = Clamp(s.IntervalSeconds, 5, 3600);
            numInterval.SetBounds(x2, y - 3, w2, h); host.Controls.Add(numInterval); y += h + 6;

            chkJitter = AddCheck(L.S("ChkJitter"), s.Jitter, x1, ref y);
            y += 4;
            chkIdle = AddCheck(L.S("ChkIdle"), s.IdleAware, x1, ref y);

            AddLabel(L.S("LblIdle"), y);
            numIdle = new NumericUpDown();
            numIdle.Minimum = 10; numIdle.Maximum = 3600;
            numIdle.Value = Clamp(s.IdleThresholdSeconds, 10, 3600);
            numIdle.SetBounds(x2, y - 3, w2, h); host.Controls.Add(numIdle); y += h + 6;

            AddLabel(L.S("LblAutoOff"), y);
            cmbAutoOff = new ComboBox();
            cmbAutoOff.DropDownStyle = ComboBoxStyle.DropDownList;
            if (!autoOffValues.Contains(s.AutoOffMinutes)) autoOffValues.Add(s.AutoOffMinutes); // wlasny czas
            foreach (int m in autoOffValues) cmbAutoOff.Items.Add(L.AutoOffLabel(m));
            cmbAutoOff.SelectedIndex = autoOffValues.IndexOf(s.AutoOffMinutes);
            cmbAutoOff.SetBounds(x2, y - 3, w2, h); host.Controls.Add(cmbAutoOff); y += h + 10;

            chkStartActive = AddCheck(L.S("ChkStartActive"), s.StartActive, x1, ref y);
            chkAutoStart = AddCheck(L.S("ChkAutoStart"), autoStart, x1, ref y);
            chkPortable = AddCheck(L.S("ChkPortable"), Settings.IsPortable(), x1, ref y);
            chkUpdates = AddCheck(L.S("ChkUpdates"), s.CheckUpdates, x1, ref y);
            chkAutoUpdate = AddCheck(L.S("ChkAutoUpdate"), s.AutoUpdate, x1 + 18, ref y);
            chkAutoUpdate.Enabled = chkUpdates.Checked;
            chkUpdates.CheckedChanged += delegate { chkAutoUpdate.Enabled = chkUpdates.Checked; };
            y += 8;

            chkHotkey = new CheckBox();
            chkHotkey.Text = L.S("ChkHotkey");
            chkHotkey.Checked = s.HotkeyEnabled;
            chkHotkey.SetBounds(x1, y - 2, x2 - x1 - 8, h);
            host.Controls.Add(chkHotkey);

            hotkeyBox = new HotkeyBox();
            hotkeyBox.SetBounds(x2, y - 3, w2, h);
            host.Controls.Add(hotkeyBox);
            y += h + 2;

            hotkeyNote = new Label();
            hotkeyNote.SetBounds(x1, y, pageWidth - 2 * x1, 32);
            host.Controls.Add(hotkeyNote);
            y += 38;

            hotkeyBox.HotkeyChanged += delegate { UpdateHotkeyNote(); };
            chkHotkey.CheckedChanged += delegate { hotkeyBox.Enabled = chkHotkey.Checked; UpdateHotkeyNote(); };
            Keys hm, hk;
            if (HotkeyText.TryParse(s.Hotkey, out hm, out hk)) hotkeyBox.SetHotkey(hm, hk);
            else hotkeyBox.SetHotkey(Keys.None, Keys.None);
            hotkeyBox.Enabled = chkHotkey.Checked;

            AddLabel(L.S("LblLanguage"), y);
            cmbLanguage = new ComboBox();
            cmbLanguage.DropDownStyle = ComboBoxStyle.DropDownList;
            foreach (string code in L.Codes) cmbLanguage.Items.Add(L.LanguageName(code));
            cmbLanguage.SelectedIndex = Math.Max(0, Array.IndexOf(L.Codes, s.Language));
            cmbLanguage.SetBounds(x2, y - 3, w2, h); host.Controls.Add(cmbLanguage); y += h;

            return y;
        }

        // ---- zakladka Automatyka; zwraca wysokosc zawartosci ----
        int BuildAutomation(Settings s)
        {
            int y = 14;
            const int indent = x1 + 18;

            chkSchedule = AddCheck(L.S("ChkSchedule"), s.ScheduleEnabled, x1, ref y);
            y += 2;

            string[] dayNames = L.S("Days").Split(',');
            for (int i = 0; i < 7; i++)
            {
                chkDays[i] = new CheckBox();
                chkDays[i].Text = dayNames[i];
                chkDays[i].Checked = s.DayOn(i);
                chkDays[i].SetBounds(indent + i * 52, y, 50, h);
                host.Controls.Add(chkDays[i]);
            }
            y += h + 6;

            Label lblFrom = new Label();
            lblFrom.Text = L.S("LblFrom");
            lblFrom.SetBounds(indent, y + 3, 40, 20);
            host.Controls.Add(lblFrom);
            dtFrom = MakeTimePicker(s.ScheduleStart);
            dtFrom.SetBounds(indent + 42, y, 80, h);
            host.Controls.Add(dtFrom);

            Label lblTo = new Label();
            lblTo.Text = L.S("LblTo");
            lblTo.SetBounds(indent + 150, y + 3, 40, 20);
            host.Controls.Add(lblTo);
            dtTo = MakeTimePicker(s.ScheduleEnd);
            dtTo.SetBounds(indent + 192, y, 80, h);
            host.Controls.Add(dtTo);
            y += h + 6;

            AddHint(L.S("ScheduleHint"), indent, ref y, 34);
            y += 10;

            chkApps = AddCheck(L.S("ChkApps"), s.AppsEnabled, x1, ref y);
            y += 2;

            txtApps = new TextBox();
            txtApps.Multiline = true;
            txtApps.ScrollBars = ScrollBars.Vertical;
            txtApps.AcceptsReturn = true;
            txtApps.Text = string.Join("\r\n", Settings.ParseApps(s.Apps));
            txtApps.SetBounds(indent, y, pageWidth - indent - x1, 90);
            host.Controls.Add(txtApps);
            y += 96;

            btnPickApps = new Button();
            btnPickApps.Text = L.S("BtnPickApps");
            btnPickApps.SetBounds(indent, y, 200, 28);
            btnPickApps.Click += delegate { PickApps(); };
            host.Controls.Add(btnPickApps);
            y += 34;

            AddHint(L.S("AppsHint"), indent, ref y, 48);
            y += 4;

            chkBattery = AddCheck(L.S("ChkBattery"), s.BatteryPause, x1, ref y);
            y += 2;
            Label lblWhen = new Label();
            lblWhen.Text = L.S("LblBatteryWhen");
            lblWhen.SetBounds(indent, y + 3, 80, 20);
            host.Controls.Add(lblWhen);
            cmbBattery = new ComboBox();
            cmbBattery.DropDownStyle = ComboBoxStyle.DropDownList;
            foreach (int v in batteryValues)
                cmbBattery.Items.Add(v == 0 ? L.S("BatteryAlways") : string.Format(L.S("BatteryBelow"), v));
            cmbBattery.SelectedIndex = Math.Max(0, Array.IndexOf(batteryValues, s.BatteryThreshold));
            cmbBattery.SetBounds(indent + 82, y, 200, h);
            host.Controls.Add(cmbBattery);
            y += h + 6;
            AddHint(L.S("BatteryHint"), indent, ref y, 34);

            chkSchedule.CheckedChanged += delegate { UpdateAutomationEnabled(); };
            chkApps.CheckedChanged += delegate { UpdateAutomationEnabled(); };
            chkBattery.CheckedChanged += delegate { UpdateAutomationEnabled(); };
            UpdateAutomationEnabled();
            return y;
        }

        static DateTimePicker MakeTimePicker(int minutes)
        {
            DateTimePicker dt = new DateTimePicker();
            dt.Format = DateTimePickerFormat.Custom;
            dt.CustomFormat = "HH:mm";
            dt.ShowUpDown = true;
            dt.Value = DateTime.Today.AddMinutes(minutes);
            return dt;
        }

        void UpdateAutomationEnabled()
        {
            foreach (CheckBox c in chkDays) c.Enabled = chkSchedule.Checked;
            dtFrom.Enabled = dtTo.Enabled = chkSchedule.Checked;
            txtApps.Enabled = btnPickApps.Enabled = chkApps.Checked;
            cmbBattery.Enabled = chkBattery.Checked;
        }

        void PickApps()
        {
            using (AppPickerForm f = new AppPickerForm())
            {
                if (f.ShowDialog(this) != DialogResult.OK || f.Selected.Count == 0) return;
                List<string> all = new List<string>(Settings.ParseApps(txtApps.Text));
                all.AddRange(f.Selected);
                txtApps.Text = string.Join("\r\n", Settings.ParseApps(string.Join(";", all.ToArray())));
            }
        }

        void UpdateHotkeyNote()
        {
            if (chkHotkey.Checked && HotkeyText.ConflictsWithAltGr(hotkeyBox.Mods, hotkeyBox.Key))
            {
                hotkeyNote.ForeColor = Color.Firebrick;
                hotkeyNote.Text = string.Format(L.S("HotkeyAltGr"), hotkeyBox.Value, HotkeyText.KeyName(hotkeyBox.Key));
            }
            else
            {
                hotkeyNote.ForeColor = Color.Gray;
                hotkeyNote.Text = L.S("HotkeyHint");
            }
        }

        void AddLabel(string text, int y)
        {
            Label l = new Label();
            l.Text = text; l.SetBounds(x1, y, x2 - x1 - 8, 22);
            host.Controls.Add(l);
        }

        void AddHint(string text, int x, ref int y, int height)
        {
            Label l = new Label();
            l.Text = text;
            l.ForeColor = Color.Gray;
            l.SetBounds(x, y, pageWidth - x - x1 + 8, height);
            host.Controls.Add(l);
            y += height + 6;
        }

        CheckBox AddCheck(string text, bool value, int x, ref int y)
        {
            CheckBox c = new CheckBox();
            c.Text = text;
            c.Checked = value;
            c.SetBounds(x, y, pageWidth - x - x1 + 8, h);
            host.Controls.Add(c);
            y += h;
            return c;
        }

        static int Clamp(int v, int lo, int hi)
        {
            if (v < lo) return lo;
            if (v > hi) return hi;
            return v;
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
            Result.CheckUpdates = chkUpdates.Checked;
            Result.AutoUpdate = chkAutoUpdate.Checked;
            Result.HotkeyEnabled = chkHotkey.Checked && hotkeyBox.Key != Keys.None;
            if (hotkeyBox.Key != Keys.None) Result.Hotkey = hotkeyBox.Value;
            Result.Language = L.Codes[cmbLanguage.SelectedIndex];

            Result.ScheduleEnabled = chkSchedule.Checked;
            StringBuilder days = new StringBuilder();
            foreach (CheckBox c in chkDays) days.Append(c.Checked ? '1' : '0');
            Result.ScheduleDays = days.ToString();
            Result.ScheduleStart = dtFrom.Value.Hour * 60 + dtFrom.Value.Minute;
            Result.ScheduleEnd = dtTo.Value.Hour * 60 + dtTo.Value.Minute;
            Result.Apps = string.Join(";", Settings.ParseApps(txtApps.Text));
            Result.AppsEnabled = chkApps.Checked && Result.Apps.Length > 0;
            Result.BatteryPause = chkBattery.Checked;
            Result.BatteryThreshold = batteryValues[cmbBattery.SelectedIndex];

            AutoStartEnabled = chkAutoStart.Checked;
            PortableEnabled = chkPortable.Checked;
        }
    }

    // ---------------------------------------------------------------
    // Wybor programow z listy uruchomionych (procesy z oknami).
    // ---------------------------------------------------------------
    public class AppPickerForm : Form
    {
        public List<string> Selected = new List<string>();
        ListBox list;

        public AppPickerForm()
        {
            Text = L.S("PickTitle");
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false; MinimizeBox = false;
            ShowInTaskbar = false;

            Label hint = new Label();
            hint.Text = L.S("PickHint");
            hint.SetBounds(12, 12, 360, 20);
            Controls.Add(hint);

            list = new ListBox();
            list.SelectionMode = SelectionMode.MultiExtended;
            list.IntegralHeight = false;
            list.SetBounds(12, 36, 360, 260);
            list.DoubleClick += delegate { if (list.SelectedItems.Count > 0) { ReadBack(); DialogResult = DialogResult.OK; } };
            Controls.Add(list);

            // nazwa procesu + tytul okna, bez duplikatow i bez samego AA
            SortedDictionary<string, string> apps = new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            int self = Process.GetCurrentProcess().Id;
            foreach (Process p in Process.GetProcesses())
            {
                try
                {
                    if (p.Id != self && p.MainWindowHandle != IntPtr.Zero && p.MainWindowTitle.Length > 0 &&
                        !apps.ContainsKey(p.ProcessName))
                        apps[p.ProcessName] = p.MainWindowTitle;
                }
                catch { }
                p.Dispose();
            }
            foreach (KeyValuePair<string, string> kv in apps)
            {
                string title = kv.Value.Length > 40 ? kv.Value.Substring(0, 40) + "..." : kv.Value;
                list.Items.Add(new PickItem(kv.Key, kv.Key + "   (" + title + ")"));
            }

            Button ok = new Button();
            ok.Text = L.S("Add"); ok.DialogResult = DialogResult.OK;
            ok.SetBounds(372 - 190, 308, 90, 28);
            ok.Click += delegate { ReadBack(); };
            Controls.Add(ok);

            Button cancel = new Button();
            cancel.Text = L.S("Cancel"); cancel.DialogResult = DialogResult.Cancel;
            cancel.SetBounds(372 - 90, 308, 90, 28);
            Controls.Add(cancel);

            AcceptButton = ok; CancelButton = cancel;
            ClientSize = new Size(384, 348);
        }

        void ReadBack()
        {
            Selected.Clear();
            foreach (PickItem it in list.SelectedItems) Selected.Add(it.Name);
        }

        class PickItem
        {
            public string Name, Display;
            public PickItem(string name, string display) { Name = name; Display = display; }
            public override string ToString() { return Display; }
        }
    }

    // ---------------------------------------------------------------
    // Okno wlasnego czasu auto-wylaczenia: "po N min" albo "o godzinie".
    // ---------------------------------------------------------------
    public class AutoOffForm : Form
    {
        public int Minutes;
        public DateTime? UntilTime;

        RadioButton rbAfter, rbAt;
        NumericUpDown numMinutes;
        DateTimePicker dtAt;

        public AutoOffForm(int currentMinutes, DateTime? currentUntil)
        {
            Text = L.S("AutoOffTitle");
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterScreen;
            MaximizeBox = false; MinimizeBox = false;

            const int x1 = 16, x2 = 210, w2 = 110, h = 26;
            int y = 18;

            rbAfter = new RadioButton();
            rbAfter.Text = L.S("RbAfter");
            rbAfter.SetBounds(x1, y, x2 - x1 - 8, h);
            Controls.Add(rbAfter);
            numMinutes = new NumericUpDown();
            numMinutes.Minimum = 1; numMinutes.Maximum = 1440;
            numMinutes.Value = currentMinutes > 0 ? Math.Min(currentMinutes, 1440) : 90;
            numMinutes.SetBounds(x2, y, w2, h);
            Controls.Add(numMinutes);
            y += h + 10;

            rbAt = new RadioButton();
            rbAt.Text = L.S("RbAt");
            rbAt.SetBounds(x1, y, x2 - x1 - 8, h);
            Controls.Add(rbAt);
            dtAt = new DateTimePicker();
            dtAt.Format = DateTimePickerFormat.Custom;
            dtAt.CustomFormat = "HH:mm";
            dtAt.ShowUpDown = true;
            DateTime suggest = DateTime.Now.AddHours(1);
            dtAt.Value = currentUntil.HasValue ? currentUntil.Value
                : new DateTime(suggest.Year, suggest.Month, suggest.Day, suggest.Hour, 0, 0);
            dtAt.SetBounds(x2, y, w2, h);
            Controls.Add(dtAt);
            y += h + 2;

            Label hint = new Label();
            hint.Text = L.S("AtHint");
            hint.ForeColor = Color.Gray;
            hint.SetBounds(x1 + 18, y, x2 + w2 - x1 - 18, 20);
            Controls.Add(hint);
            y += 34;

            rbAt.Checked = currentUntil.HasValue;
            rbAfter.Checked = !currentUntil.HasValue;
            // klikniecie w pole wybiera odpowiadajaca mu opcje
            numMinutes.Enter += delegate { rbAfter.Checked = true; };
            dtAt.Enter += delegate { rbAt.Checked = true; };

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

        void ReadBack()
        {
            if (rbAt.Checked)
            {
                DateTime now = DateTime.Now;
                DateTime t = new DateTime(now.Year, now.Month, now.Day, dtAt.Value.Hour, dtAt.Value.Minute, 0);
                if (t <= now) t = t.AddDays(1);
                UntilTime = t;
            }
            else
            {
                UntilTime = null;
                Minutes = (int)numMinutes.Value;
            }
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
            AddRow(L.S("AboutAuthor"), AppInfo.Author, null, ref y);
            AddRow(L.S("AboutWebsite"), "galeziok.pl", AppInfo.WebsiteUrl, ref y);
            AddRow(L.S("AboutLicense"), L.S("AboutLicenseText"), null, ref y);
            AddRow(L.S("AboutSource"), "github.com/gynsior/always-awake", AppInfo.RepoUrl, ref y);
            AddRow(L.S("AboutSettings"), Settings.FilePath() + (Settings.IsPortable() ? L.S("AboutPortable") : ""), null, ref y);
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
                v.LinkClicked += delegate { AppInfo.OpenUrl(url); };
                Controls.Add(v);
            }
            y += 24;
        }
    }
}
