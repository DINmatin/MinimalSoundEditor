# Minimal Sound Editor 1.0.0

Erste öffentliche Version von Minimal Sound Editor: ein kompakter Windows-Audioeditor mit Wellenformbearbeitung, vereinheitlichtem Export und einem kleinen ASIO-Aufnahmestudio.

## Highlights

- Audio schneiden und Auswahlbereiche kopieren, einfügen, löschen oder stummschalten
- Normalisieren, Komprimieren, Fade-in, Fade-out und Stille trimmen
- Export der Auswahl oder – ohne Auswahl – des gesamten Clips
- Export als WAV, FLAC, MP3 und M4A
- ASIO-Mini-Studio mit Live-Pegel, Clip-Warnung und direkter Aufnahme in den Editor
- Treiber-, Eingang- und Samplerate-Auswahl; getestet mit MOTU M4
- Stapelverarbeitung und FFmpeg-gestützte Medienfunktionen
- Installer und portable Windows-x64-Version

## Downloads

- **Installer:** `MinimalSoundEditor_Setup_1.0.0.exe`
- **Portable:** `MinimalSoundEditor_Portable_1.0.0_win-x64.zip`
- **Prüfsummen:** `SHA256SUMS.txt`

Der Installer benötigt Administratorrechte und installiert nach `C:\Program Files\Minimal Sound Editor`. Die portable Version wird nur entpackt und direkt gestartet.

## Hinweise

- Windows 10 oder Windows 11, 64 Bit
- Für ASIO-Aufnahmen muss der passende ASIO-Treiber des Audio-Interfaces installiert sein.
- Der Installer ist nicht digital signiert; Windows SmartScreen kann deshalb eine Warnung anzeigen.
- Die Aufnahme erfolgt derzeit mono über den im Mini-Studio gewählten ASIO-Eingang.

## FFmpeg

Die Binary-Pakete enthalten eine separate `Tools\ffmpeg.exe` für bestimmte Export- und Medienfunktionen. Die genaue Version und Build-Konfiguration stehen in `FFMPEG_BUILD_INFO.txt`. FFmpeg und die verwendeten Bibliotheken unterliegen ihren jeweiligen Lizenzen; Lizenztexte und Quellcodehinweise sind dem Release beigefügt.

## Lizenz

Der eigene Quellcode von Minimal Sound Editor steht unter der MIT-Lizenz. Drittanbieter-Komponenten behalten ihre jeweiligen Lizenzen.
