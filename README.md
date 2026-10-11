# AA — Always Awake

Lekkie narzędzie w zasobniku systemowym (system tray) dla Windows, które utrzymuje
komputer „aktywnym”, aby nie wygasił ekranu ani nie przeszedł w stan bezczynności.
Jeden samodzielny plik `.exe`, bez instalacji i bez zależności poza .NET Framework 4
(obecnym w każdym Windows 10/11).

## Funkcje

- **Ikona w zasobniku** — zielona = aktywne, szara = wstrzymane. Dwuklik przełącza.
  Podpowiedź po najechaniu pokazuje tryb i czas do auto-wyłączenia.
- **Trzy tryby pracy:**
  - `Display` — oficjalne API `SetThreadExecutionState` (zapobiega wygaszeniu/uśpieniu).
  - `Input` — wysyła „cichy” klawisz **F15** (nie przesuwa kursora, nic nie wpisuje).
  - `Both` — oba naraz (domyślnie).
- **Tryb bezczynności (idle-aware)** — klawisz wysyłany tylko wtedy, gdy faktycznie
  jesteś bezczynny przez zadany czas, więc program nie przeszkadza podczas pracy.
- **Auto-wyłącznik** — po 30 min / 1 / 2 / 4 godz., po własnej liczbie minut albo
  o wybranej godzinie (np. „do 17:00”) program sam się wstrzymuje.
- **Skrót klawiszowy** — domyślnie `Ctrl+Alt+K` włącza/wyłącza AA z dowolnego miejsca.
  Można go zmienić lub wyłączyć w ustawieniach (ostrzeżenie, gdy koliduje z AltGr,
  np. `Ctrl+Alt+A` = „ą”).
- **Sprawdzanie aktualizacji** — raz dziennie (i na żądanie z menu) program sprawdza
  GitHub Releases i pokazuje dymek, gdy jest nowsza wersja. Można wyłączyć w ustawieniach.
- **Jitter interwału** — opcjonalne losowe wahanie odstępu (±10%).
- **Zapamiętywanie ustawień** — `%AppData%\AA\settings.ini`.
- **Autostart** — skrót w folderze Startup użytkownika (bez rejestru, bez admina).
- **Pojedyncza instancja** — mutex pilnuje, by działała tylko jedna kopia (przy próbie
  uruchomienia drugiej pojawia się komunikat).
- **Trzy języki** — polski, angielski i niemiecki; domyślnie według języka Windows,
  zmiana w menu „Język (Language)” lub w ustawieniach, bez restartu.
- **Okno „O programie”** — wersja, autor, licencja i link do repozytorium.

## Pobieranie

Gotowy `AA.exe` jest dostępny w zakładce **Releases**.

Plik nie jest podpisany cyfrowo, więc przy pierwszym uruchomieniu Windows SmartScreen
może pokazać ostrzeżenie „System Windows ochronił ten komputer”. Kliknij
**Więcej informacji → Uruchom mimo to**.

## Budowanie

Wymagany tylko wbudowany w Windows kompilator C# (`csc.exe`, .NET Framework 4).
W folderze z `AA.cs` i `AA.ico` uruchom w PowerShell:

```powershell
& "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /codepage:65001 /target:winexe /out:AA.exe /win32icon:AA.ico /reference:System.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll AA.cs
```

### Automatyczne budowanie (GitHub Actions)

Każdy push na `main` buduje `AA.exe` na GitHubie (plik do pobrania w zakładce
**Actions** jako artefakt). Nowe wydanie: podnieś `AppInfo.Version` w `AA.cs`
(oraz `AssemblyVersion`), zrób commit i wypchnij tag, np.:

```powershell
git tag v2.3
git push origin v2.3
```

GitHub sam zbuduje `AA.exe` i utworzy release. Jeśli tag nie zgadza się z wersją
w kodzie, budowanie zakończy się błędem.

## Użycie

Uruchom `AA.exe` — pojawi się kropka w zasobniku.

- **Prawy klik** → menu: Aktywne, Tryb, Auto-wyłącz po, Tryb bezczynności,
  Jitter, Uruchamiaj przy starcie, Język, Ustawienia, Sprawdź aktualizacje,
  O programie, Zakończ.
- **Ctrl+Alt+K** (domyślnie) → szybkie włącz/wyłącz z klawiatury.
- **Dwuklik lewym** → szybkie włącz/wyłącz.

Argumenty wiersza poleceń (opcjonalne, nadpisują zapisane ustawienia na starcie;
wielkość liter bez znaczenia, działają też prefiksy `--` i `/`):

```
AA.exe -Mode Display|Input|Both  -IntervalSeconds <n>  -Inactive
```

- `-Mode` — tryb pracy,
- `-IntervalSeconds` — co ile sekund odświeżać,
- `-Inactive` — start w stanie wstrzymanym.

Przykład skrótu: `AA.exe -Mode Display -Inactive`.

## Licencja

MIT — patrz plik `LICENSE`.
