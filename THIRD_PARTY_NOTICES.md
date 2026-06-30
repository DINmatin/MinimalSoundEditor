# Drittanbieter-Hinweise

Minimal Sound Editor verwendet oder verteilt die folgenden Drittanbieter-Komponenten.

## NAudio 2.2.1

- Projekt: NAudio
- Autor: Mark Heath und Mitwirkende
- Lizenz: MIT
- Verwendung: Audio-Decoding, Wiedergabe, Verarbeitung und ASIO-Aufnahme
- Projektseite: https://github.com/naudio/NAudio
- Lizenztext: `third-party-licenses/NAudio-MIT.txt`

## FFmpeg

Minimal Sound Editor kann `Tools\ffmpeg.exe` als **separates Kommandozeilenprogramm** starten. FFmpeg ist nicht im Git-Repository enthalten.

Der Lizenzstatus hängt vom konkret beigelegten Build ab. FFmpeg ist grundsätzlich LGPL-lizenziert; Builds mit aktivierten GPL-Komponenten stehen als Gesamtbinary unter der GPL. Ein Build mit `--enable-nonfree` darf nicht ohne Weiteres weiterverteilt werden.

Für das Release muss deshalb die Ausgabe von

```cmd
Tools\ffmpeg.exe -version
```

geprüft werden. Das Release-Skript legt diese Ausgabe als `FFMPEG_BUILD_INFO.txt` in das veröffentlichte Paket.

Beigefügte Lizenztexte:

- `third-party-licenses/FFmpeg-LGPL-2.1.txt`
- `third-party-licenses/FFmpeg-GPL-3.0.txt`

Offizielle Informationen:

- https://ffmpeg.org/legal.html
- https://ffmpeg.org/download.html

### Pflicht vor einer öffentlichen Veröffentlichung

Der Release-Ersteller muss für den tatsächlich verteilten FFmpeg-Build die dazugehörigen Lizenzbedingungen erfüllen und den korrespondierenden Quellcode beziehungsweise einen rechtlich ausreichenden Quellcodezugang bereitstellen. Diese Datei ist ein technischer Hinweis und keine Rechtsberatung.
