# MinimalSoundEditor

Ein kompakter Audio-Editor für Windows, geschrieben in C# mit .NET WinForms.
Der Schwerpunkt liegt auf schnellem Schneiden, einfachen Bearbeitungen und einer kleinen ASIO-Aufnahmelösung ohne überladene DAW-Oberfläche.

## Funktionen

- übersichtliche Wellenform mit Detail- und Gesamtansicht
- Auswahl, Löschen, Kopieren, Einfügen und überschreibendes Einfügen
- Normalisieren, Komprimieren, Fade-in, Fade-out und Stille trimmen
- Stille einfügen oder ausgewählte Bereiche stummschalten
- Wiedergabe, Loop, Auto-Follow und Zoom auf Auswahl
- einheitlicher **Export**: Auswahl exportieren; ohne Auswahl den gesamten Clip
- Export als WAV, FLAC, MP3 oder M4A
- ASIO-„Mini-Studio“ mit Treiber-, Eingang- und Samplerate-Auswahl
- Live-Eingangspegel, Clip-Warnung und Mono-Aufnahme direkt in den Editor
- Stapelverarbeitung
- Video-Vorschau und FFmpeg-gestützte Medienfunktionen

Die ASIO-Aufnahme wurde mit einer **MOTU M4** getestet. Andere ASIO-Geräte sollten ebenfalls funktionieren, sofern ein passender Windows-ASIO-Treiber installiert ist.

## Systemanforderungen

- Windows 10 oder Windows 11, 64 Bit
- für ASIO-Aufnahmen: installierter ASIO-Treiber des Audio-Interfaces
- für einen Build aus dem Quellcode: .NET 8 SDK
- für den Installer-Build: Inno Setup 6

Der veröffentlichte Installer und die portable ZIP werden self-contained gebaut. Auf dem Zielrechner muss daher kein separates .NET Runtime-Paket installiert sein.

## Installation

Für normale Nutzer sind zwei Pakete vorgesehen:

- `MinimalSoundEditor_Setup_1.0.0.exe` – Installer
- `MinimalSoundEditor_Portable_1.0.0_win-x64.zip` – portable Version

Nach der Installation startet das Programm über das Startmenü. In der portablen Version genügt ein Doppelklick auf `MinimalSoundEditor.exe`.

## Bedienung

1. **Datei → Öffnen…** lädt eine Audiodatei.
2. Einen Bereich in der Wellenform markieren und über **Bearbeiten** verändern.
3. **Datei → Export…** exportiert die Auswahl. Ohne aktive Auswahl wird der gesamte Clip exportiert.
4. **Aufnahme → Mini-Studio…** öffnet die ASIO-Aufnahme mit Live-Pegel.

Wichtige Tastenkürzel:

| Funktion | Kürzel |
|---|---|
| Öffnen | `Ctrl+O` |
| Export | `Ctrl+E` |
| Rückgängig | `Ctrl+Z` |
| Alles auswählen | `Ctrl+A` |
| Mini-Studio | `Ctrl+R` |
| Wiedergabe/Pause | `Leertaste` |
| Loop | `L` |

## Aus dem Quellcode bauen

Repository klonen und in den Projektordner wechseln:

```cmd
git clone <REPOSITORY-URL>
cd MinimalSoundEditor
```

FFmpeg wird wegen seiner Größe und separaten Lizenz **nicht** im Git-Repository gespeichert. Eine passende `ffmpeg.exe` muss lokal hier liegen:

```text
Tools\ffmpeg.exe
```

Danach:

```cmd
dotnet restore
dotnet build .\MinimalSoundEditor.csproj
.\bin\Debug\net8.0-windows\MinimalSoundEditor.exe
```

## Release und Installer bauen

Inno Setup 6 lässt sich beispielsweise mit Winget installieren:

```cmd
winget install --id JRSoftware.InnoSetup -e
```

Danach im Repository-Stamm:

```cmd
scripts\build-release.cmd 1.0.0
```

Die Ergebnisse liegen anschließend unter:

```text
artifacts\installer\MinimalSoundEditor_Setup_1.0.0.exe
artifacts\portable\MinimalSoundEditor_Portable_1.0.0_win-x64.zip
```

Das Build-Skript protokolliert zusätzlich die tatsächlich verwendete FFmpeg-Version im Release-Paket.

## FFmpeg-Hinweis

Einige Export- und Medienfunktionen starten `Tools\ffmpeg.exe` als separates Kommandozeilenprogramm. FFmpeg ist nicht Teil des Quellcode-Repositories. Release-Ersteller müssen die Lizenz des konkret verwendeten Builds prüfen und die zugehörigen Lizenz- und Quellcodepflichten erfüllen. Siehe [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).

## Lizenz

Der eigene Quellcode von Minimal Sound Editor steht unter der [MIT-Lizenz](LICENSE).
Drittanbieter-Komponenten behalten ihre jeweiligen Lizenzen.
