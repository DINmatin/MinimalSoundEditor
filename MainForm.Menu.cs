using NAudio.Wave;
using System;
using System.Windows.Forms;

namespace MinimalSoundEditor
{
    public partial class MainForm
    {
        private ToolStripMenuItem? _miPlaybackPlay;
        private ToolStripMenuItem? _miPlaybackLoop;
        private ToolStripMenuItem? _miPlaybackAutoFollow;
        private ToolStripMenuItem? _miVideoPreview;
        private ToolStripMenuItem? _miUndo;
        private ToolStripMenuItem? _miDeleteSelection;
        private ToolStripMenuItem? _miCopy;
        private ToolStripMenuItem? _miPasteInsert;
        private ToolStripMenuItem? _miPasteOverwrite;
        private ToolStripMenuItem? _miRecordingStudio;

        /// <summary>
        /// Replaces the icon toolbar with a compact, text-only menu.
        /// The old controls remain hidden as internal compatibility controls so the
        /// existing editing/playback code can keep using their state and handlers.
        /// </summary>
        private void BuildMenuOnlyUi()
        {
            _topPanel.Visible = false;
            _topPanel.Height = 0;

            _menuStrip.Items.Clear();
            _menuStrip.ShowItemToolTips = true;

            dateiToolStripMenuItem = new ToolStripMenuItem("Datei");
            var editMenu = new ToolStripMenuItem("Bearbeiten");
            var playbackMenu = new ToolStripMenuItem("Wiedergabe");
            var recordingMenu = new ToolStripMenuItem("Aufnahme");
            ansichtToolStripMenuItem = new ToolStripMenuItem("Ansicht");
            var helpMenu = new ToolStripMenuItem("Hilfe");

            miFileOpen = CreateTextMenuItem("Öffnen...", Keys.Control | Keys.O, BtnOpen_Click);
            miFileExportSel = CreateTextMenuItem("Export...", Keys.Control | Keys.E, ExportCommand);
            miFileExit = CreateTextMenuItem("Beenden", Keys.Control | Keys.Q, miFileExit_Click);

            var videoPreviewItem = CreateTextMenuItem(
                "Video-Vorschau öffnen",
                Keys.None,
                _btnVideoPreview_Click);
            _miVideoPreview = videoPreviewItem;

            var batchItem = CreateTextMenuItem(
                "Stapelverarbeitung...",
                Keys.None,
                BtnBatch_Click);

            dateiToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[]
            {
                miFileOpen,
                new ToolStripSeparator(),
                miFileExportSel,
                new ToolStripSeparator(),
                videoPreviewItem,
                batchItem,
                new ToolStripSeparator(),
                miFileExit
            });

            _miUndo = CreateTextMenuItem("Rückgängig", Keys.Control | Keys.Z, BtnUndo_Click);
            _miDeleteSelection = CreateTextMenuItem("Auswahl löschen", Keys.Delete, BtnDeleteSelection_Click);
            _miCopy = CreateTextMenuItem("Auswahl kopieren", Keys.Control | Keys.C, (_, _) => CopySelection());
            _miPasteInsert = CreateTextMenuItem("Einfügen", Keys.Control | Keys.V, (_, _) => PasteInsert());
            _miPasteOverwrite = CreateTextMenuItem(
                "Überschreibend einfügen",
                Keys.Control | Keys.Shift | Keys.V,
                (_, _) => PasteOverwrite());

            var selectAllItem = CreateTextMenuItem("Alles auswählen", Keys.Control | Keys.A, (_, _) => SelectAll());
            var normalizeItem = CreateTextMenuItem("Normalisieren", Keys.Control | Keys.N, _btnNormalize_Click);
            var compressItem = CreateTextMenuItem("Komprimieren", Keys.None, _btnCompress_Click);
            var trimItem = CreateTextMenuItem("Stille am Anfang/Ende trimmen", Keys.None, _btnTrim_Click);
            var fadeInItem = CreateTextMenuItem("Fade In", Keys.None, _btnFadeIn_Click);
            var fadeOutItem = CreateTextMenuItem("Fade Out", Keys.None, _btnFadeOut_Click);

            var silenceMenu = new ToolStripMenuItem("Stille");
            silenceMenu.DropDownItems.AddRange(new ToolStripItem[]
            {
                CreateTextMenuItem("Auswahl stummschalten", Keys.None, (_, _) => MuteSelection()),
                CreateTextMenuItem("Vor Auswahl einfügen", Keys.None, (_, _) => InsertSilenceBeforeSelection()),
                CreateTextMenuItem("Nach Auswahl einfügen", Keys.None, (_, _) => InsertSilenceAfterSelection())
            });

            editMenu.DropDownItems.AddRange(new ToolStripItem[]
            {
                _miUndo,
                new ToolStripSeparator(),
                _miDeleteSelection,
                _miCopy,
                _miPasteInsert,
                _miPasteOverwrite,
                selectAllItem,
                new ToolStripSeparator(),
                normalizeItem,
                compressItem,
                trimItem,
                fadeInItem,
                fadeOutItem,
                silenceMenu
            });

            _miPlaybackPlay = CreateTextMenuItem("Wiedergabe starten", Keys.Space, (_, _) => TogglePlayStop());
            var stopItem = CreateTextMenuItem("Wiedergabe stoppen", Keys.None, BtnStop_Click);
            _miPlaybackLoop = CreateTextMenuItem("Loop", Keys.L, (_, _) => _chkLoop.Checked = !_chkLoop.Checked);
            _miPlaybackLoop.CheckOnClick = false;
            _miPlaybackAutoFollow = CreateTextMenuItem(
                "Playhead automatisch folgen",
                Keys.None,
                (_, _) => _chkAutoFollow.Checked = !_chkAutoFollow.Checked);
            _miPlaybackAutoFollow.CheckOnClick = false;

            playbackMenu.DropDownItems.AddRange(new ToolStripItem[]
            {
                _miPlaybackPlay,
                stopItem,
                new ToolStripSeparator(),
                _miPlaybackLoop,
                _miPlaybackAutoFollow,
                new ToolStripSeparator(),
                CreateTextMenuItem("Zum Clip-Anfang", Keys.NumPad0, (_, _) => JumpToStartOfFile()),
                CreateTextMenuItem("Zum Auswahl-Anfang", Keys.NumPad1, (_, _) => JumpToSelectionEdge(toStart: true)),
                CreateTextMenuItem("Zum Auswahl-Ende", Keys.NumPad2, (_, _) => JumpToSelectionEdge(toStart: false))
            });

            _miRecordingStudio = CreateTextMenuItem(
                "Mini-Studio...",
                Keys.Control | Keys.R,
                (_, _) => StartAsioRecording());
            recordingMenu.DropDownItems.Add(_miRecordingStudio);

            miViewZoomAll = CreateTextMenuItem("Alles anzeigen", Keys.Control | Keys.NumPad0, miViewZoomAll_Click);
            var zoomSelectionItem = CreateTextMenuItem("Auswahl zoomen", Keys.None, (_, _) => ZoomSelection());
            var themeItem = CreateTextMenuItem("Farben und Theme...", Keys.Control | Keys.T, BtnTheme_Click);
            ansichtToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[]
            {
                miViewZoomAll,
                zoomSelectionItem,
                new ToolStripSeparator(),
                themeItem
            });

            aboutToolStripMenuItem = CreateTextMenuItem("Über Minimal Sound Editor...", Keys.Control | Keys.I, AboutToolStripMenuItem_Click);
            helpMenu.DropDownItems.Add(aboutToolStripMenuItem);

            _menuStrip.Items.AddRange(new ToolStripItem[]
            {
                dateiToolStripMenuItem,
                editMenu,
                playbackMenu,
                recordingMenu,
                ansichtToolStripMenuItem,
                helpMenu
            });

            foreach (ToolStripMenuItem topLevel in _menuStrip.Items)
            {
                RemoveMenuImageMargins(topLevel);
                topLevel.DropDownOpening += (_, _) => UpdateMenuOnlyUi();
            }

            UpdateMenuOnlyUi();
        }

        private static ToolStripMenuItem CreateTextMenuItem(
            string text,
            Keys shortcutKeys,
            EventHandler click)
        {
            var item = new ToolStripMenuItem(text)
            {
                Image = null
            };

            if (shortcutKeys != Keys.None)
            {
                bool hasModifier =
                    (shortcutKeys & (Keys.Control | Keys.Shift | Keys.Alt)) != Keys.None;

                if (hasModifier)
                {
                    // Echte WinForms-Menü-Shortcuts nur als Tastenkombination setzen.
                    item.ShortcutKeys = shortcutKeys;
                    item.ShowShortcutKeys = true;
                }
                else
                {
                    // Einzelne Tasten werden bereits zentral in MainForm.ProcessCmdKey
                    // verarbeitet. Im Menü zeigen wir sie deshalb nur als Hinweis an.
                    item.ShortcutKeyDisplayString = shortcutKeys switch
                    {
                        Keys.Space => "Leertaste",
                        Keys.Delete => "Entf",
                        Keys.NumPad0 => "Num 0",
                        Keys.NumPad1 => "Num 1",
                        Keys.NumPad2 => "Num 2",
                        _ => shortcutKeys.ToString()
                    };
                    item.ShowShortcutKeys = true;
                }
            }

            item.Click += click;
            return item;
        }

        private static void RemoveMenuImageMargins(ToolStripMenuItem item)
        {
            if (item.DropDown is ToolStripDropDownMenu dropDown)
            {
                dropDown.ShowImageMargin = false;
                dropDown.ShowCheckMargin = true;
            }

            foreach (ToolStripItem child in item.DropDownItems)
            {
                if (child is ToolStripMenuItem childMenu)
                    RemoveMenuImageMargins(childMenu);
            }
        }

        private void UpdateMenuOnlyUi()
        {
            bool hasAudio = _currentSamples != null && _currentSamples.Length > 0;
            bool hasSelection = hasAudio && _detailView.HasActiveSelection;
            bool hasClipboard = _clipboardSamples != null && _clipboardSamples.Length > 0;
            bool isPlaying = _waveOut != null && _waveOut.PlaybackState == PlaybackState.Playing;

            if (_miUndo != null)
                _miUndo.Enabled = _undoStack.Count > 0 && !_isRecording;

            if (_miDeleteSelection != null)
                _miDeleteSelection.Enabled = hasSelection && !_isRecording;

            if (_miCopy != null)
                _miCopy.Enabled = hasSelection && !_isRecording;

            if (_miPasteInsert != null)
                _miPasteInsert.Enabled = hasClipboard && !_isRecording;

            if (_miPasteOverwrite != null)
                _miPasteOverwrite.Enabled = hasClipboard && !_isRecording;

            if (_miPlaybackPlay != null)
            {
                _miPlaybackPlay.Text = isPlaying ? "Wiedergabe pausieren" : "Wiedergabe starten";
                _miPlaybackPlay.Enabled = hasAudio && !_isRecording;
            }

            if (_miPlaybackLoop != null)
            {
                _miPlaybackLoop.Checked = _chkLoop.Checked;
                _miPlaybackLoop.Enabled = hasAudio && !_isRecording;
            }

            if (_miPlaybackAutoFollow != null)
            {
                _miPlaybackAutoFollow.Checked = _chkAutoFollow.Checked;
                _miPlaybackAutoFollow.Enabled = !_isRecording;
            }

            if (_miVideoPreview != null)
                _miVideoPreview.Enabled = _btnVideoPreview.Enabled && !_isRecording;

            if (_miRecordingStudio != null)
            {
                _miRecordingStudio.Text = _isRecording ? "Aufnahme stoppen" : "Mini-Studio...";
                _miRecordingStudio.Enabled = !_isClosing;
            }
        }

        private void UpdateMenuPlaybackText()
        {
            if (_miPlaybackPlay == null)
                return;

            bool isPlaying = _waveOut != null && _waveOut.PlaybackState == PlaybackState.Playing;
            _miPlaybackPlay.Text = isPlaying ? "Wiedergabe pausieren" : "Wiedergabe starten";
        }
    }
}
