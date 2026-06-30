# Lokale FFmpeg-Abhängigkeit

Lege für lokale Builds eine kompatible Datei hier ab:

```text
Tools\ffmpeg.exe
```

Die EXE wird absichtlich nicht in Git gespeichert. Beim Veröffentlichen kopiert MSBuild sie in den Ausgabeordner und `scripts\build-release.cmd` protokolliert ihre Versions- und Konfigurationsdaten.

Vor einer öffentlichen Weitergabe müssen Lizenz und Quellcodepflichten des konkret verwendeten FFmpeg-Builds geprüft werden. Siehe `THIRD_PARTY_NOTICES.md`.
