# Release-Checkliste

## Repository

- [ ] `git status` ist sauber
- [ ] Versionsnummer in `MinimalSoundEditor.csproj` stimmt
- [ ] `CHANGELOG.md` ist aktualisiert
- [ ] keine privaten Pfade, Testdateien, Zugangsdaten oder großen Binärdateien sind eingecheckt
- [ ] `Tools\ffmpeg.exe` wird von Git ignoriert

## Build

- [ ] `scripts\build-release.cmd 1.0.0` läuft ohne Fehler
- [ ] portable ZIP startet auf einem Rechner ohne installiertes .NET SDK
- [ ] Installer installiert und deinstalliert sauber
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

## Lizenzen

- [ ] `FFMPEG_BUILD_INFO.txt` im Release prüfen
- [ ] sicherstellen, dass der FFmpeg-Build kein `--enable-nonfree` enthält
- [ ] korrespondierenden FFmpeg-Quellcode beziehungsweise ausreichenden Quellcodezugang als Release-Asset bereitstellen
- [ ] `LICENSE`, `THIRD_PARTY_NOTICES.md` und Drittanbieter-Lizenztexte sind im Installer und in der ZIP enthalten

## GitHub

- [ ] Release-Commit erstellen
- [ ] Tag `v1.0.0` erstellen
- [ ] Repository zu GitHub pushen
- [ ] GitHub Release aus dem Tag erstellen
- [ ] Installer und portable ZIP hochladen
- [ ] Release Notes aus `CHANGELOG.md` übernehmen
