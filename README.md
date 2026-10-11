# AA — Always Awake

Lekkie narzędzie w zasobniku systemowym (system tray) dla Windows, które utrzymuje
komputer „aktywnym”, aby nie wygasił ekranu ani nie przeszedł w stan bezczynności.
Jeden samodzielny plik `.exe`, bez instalacji i bez zależności poza .NET Framework 4
(obecnym w każdym Windows 10/11).

## Funkcje

- **Ikona w zasobniku** — zielona = aktywne, szara = wstrzymane. Dwuklik przełącza.
- **Trzy tryby pracy:**
  - `Display` — oficjalne API `SetThreadExecutionState` (zapobiega wygaszeniu/uśpieniu).
  - `Input` — wysyła „cichy” klawisz **F15** (nie przesuwa kursora, nic nie wpisuje).
  - `Both` — oba naraz (domyślnie).
- **Tryb bezczynności (idle-aware)** — klawisz wysyłany tylko wtedy, gdy faktycznie
  jesteś bezczynny przez zadany czas, więc program nie przeszkadza podczas pracy.
- **Auto-wyłącznik** — po 30 min / 1 / 2 / 4 godz. program sam się wstrzymuje.
- **Jitter interwału** — opcjonalne losowe wahanie odstępu (±10%).
- **Zapamiętywanie ustawień** — `%AppData%\AA\settings.ini`.
- **Autostart** — skrót w folderze Startup użytkownika (bez rejestru, bez admina).
- **Pojedyncza instancja** — mutex pilnuje, by działała tylko jedna kopia.

## Pobieranie

Gotowy `AA.exe` jest dostępny w zakładce **Releases**.

## Budowanie

Wymagany tylko wbudowany w Windows kompilator C# (`csc.exe`, .NET Framework 4).
W folderze z `AA.cs` uruchom w PowerShell:

```powershell
& "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /target:winexe /out:AA.exe /reference:System.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll AA.cs
```

## Użycie

Uruchom `AA.exe` — pojawi się kropka w zasobniku.

- **Prawy klik** → menu: Aktywne, Tryb, Auto-wyłącz po, Tryb bezczynności,
  Jitter, Uruchamiaj przy starcie, Ustawienia / O programie, Zakończ.
- **Dwuklik lewym** → szybkie włącz/wyłącz.

Argumenty wiersza poleceń (opcjonalne, nadpisują zapisane ustawienia na starcie):

```
AA.exe -Mode Display|Input|Both  -IntervalSeconds <n>  -Inactive
```

## Licencja

MIT — patrz plik `LICENSE`.
