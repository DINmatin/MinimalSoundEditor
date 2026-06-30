# Release-Checkliste

Diese Liste gilt für `v1.0.0`. Erst taggen, wenn alle technischen und rechtlichen Prüfungen abgeschlossen sind.

## Repository

- [ ] `git status` ist sauber
- [ ] `git diff --check` meldet keine Whitespace-Fehler
- [ ] Versionsnummer in `MinimalSoundEditor.csproj` ist `1.0.0`
- [ ] `CHANGELOG.md` und `RELEASE_NOTES_1.0.0.md` sind aktuell
- [ ] keine privaten Pfade, Testdateien, Zugangsdaten oder großen Binärdateien sind eingecheckt
- [ ] `Tools\ffmpeg.exe` wird von Git ignoriert
- [ ] README-Links und Hero-Bild funktionieren auf GitHub

## Build

- [ ] `dotnet build .\MinimalSoundEditor.csproj -c Release` läuft ohne Fehler
- [ ] `scripts\build-release.cmd 1.0.0` läuft ohne Fehler
- [ ] folgende Dateien wurden erzeugt:
  - [ ] `artifacts\installer\MinimalSoundEditor_Setup_1.0.0.exe`
  - [ ] `artifacts\portable\MinimalSoundEditor_Portable_1.0.0_win-x64.zip`
  - [ ] `artifacts\SHA256SUMS.txt`
- [ ] Prüfsummen passen zu den fertigen, unveränderten Release-Dateien
- [ ] portable ZIP startet auf einem Rechner ohne installiertes .NET SDK
- [ ] Installer installiert und deinstalliert sauber
- [ ] Installer verwendet `C:\Program Files\Minimal Sound Editor`
- [ ] EXE, Hauptfenster, Setup und Verknüpfungen zeigen das Anwendungssymbol
- [ ] Themes lassen sich trotz Installation unter `Program Files` speichern
- [ ] Startmenü- und optionale Desktop-Verknüpfung funktionieren

## Funktionstest

- [ ] Datei öffnen
- [ ] Auswahl bearbeiten und Undo testen
- [ ] Export mit Auswahl testen
- [ ] Export ohne Auswahl testen
- [ ] WAV, FLAC, MP3 und M4A testen
- [ ] ASIO-Mini-Studio öffnen
- [ ] Live-Pegel prüfen
- [ ] Aufnahme starten und stoppen
- [ ] MOTU M4 oder ein anderes ASIO-Gerät testen
- [ ] Stapelverarbeitung testen
- [ ] Video-/FFmpeg-Funktionen testen

## Lizenzen und FFmpeg

- [ ] `FFMPEG_BUILD_INFO.txt` im fertigen Paket prüfen
- [ ] sicherstellen, dass der FFmpeg-Build kein `--enable-nonfree` enthält
- [ ] verwendete FFmpeg-Version und Konfiguration in den Release Notes nennen
- [ ] korrespondierenden FFmpeg-Quellcode beziehungsweise ausreichenden Quellcodezugang als Release-Asset bereitstellen
- [ ] `LICENSE`, `THIRD_PARTY_NOTICES.md` und Drittanbieter-Lizenztexte sind im Installer und in der ZIP enthalten

## GitHub

- [ ] finalen Release-Commit erstellen und pushen
- [ ] annotierten Tag erstellen: `git tag -a v1.0.0 -m "Minimal Sound Editor 1.0.0"`
- [ ] Tag pushen: `git push origin v1.0.0`
- [ ] GitHub Release aus `v1.0.0` erstellen
- [ ] `RELEASE_NOTES_1.0.0.md` als Release-Text verwenden
- [ ] Installer, portable ZIP, `SHA256SUMS.txt` und erforderliches FFmpeg-Source-Asset hochladen
- [ ] Veröffentlichung von einem frischen Download noch einmal testen
