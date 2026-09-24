using System.Collections.Generic;
using System.Globalization;
using System.IO;
using JANOARG.Shared.Data.ChartInfo;
using UnityEditor;
using UnityEngine;

namespace JANOARG.Shared.Data.Files.Editor
{
    [CustomEditor(typeof(JAPSImporter))]
    internal class JAPSImporterEditor : TextBackedImporterEditor
    {
        private struct KeyValue
        {
            public readonly string Key;
            public readonly string Value;
            public readonly bool   Changed;

            public KeyValue(string key, string value, bool changed)
            {
                Key = key;
                Value = value;
                Changed = changed;
            }
        }

        private PlayableSong _song;
        private PlayableSong _originalSong;
        private string       _baseline;

        private List<ExternalChartMeta> _blockChart  = new();
        private List<(int start, int end)> _chartBlocks = new();
        private bool _chartsEditable;

        private bool _showLayers;
        private bool _showTiming;
        private bool _showCharts;

        private SerializedObject _serialized;

        private SerializedObject GetSerialized()
        {
            ExternalPlayableSong song = AssetDatabase.LoadMainAssetAtPath(AssetPath) as ExternalPlayableSong;
            if (song == null) return null;

            if (_serialized == null || _serialized.targetObject != song)
                _serialized = new SerializedObject(song);

            _serialized.Update();

            return _serialized;
        }

        protected override bool Ready => Text != null && _song != null;
        protected override string NotReadyMessage => "Could not decode this file. Fix the .japs content and reimport.";
        protected override string SingleTargetMessage => "Select a single .japs file to edit its metadata.";

        // Dirty is derived from the data, not from GUI events: a file is modified only when its
        // encoded content differs from the content it was loaded from. This keeps foldouts,
        // hover, focus changes, etc. from being treated as edits.
        protected override bool HasChanges() => _song != null && EncodeCurrent() != _baseline;

        protected override string Build() => BuildText();

        protected override void DrawInspector()
        {
            EditorGUILayout.HelpBox("Edits are written back to the .japs text file when you Apply (or Save when prompted). Lines the editor doesn't manage are left untouched.", MessageType.Info);

            DrawMetadata();
            DrawResources();
            DrawCover();
            DrawColors();
            DrawTiming();
            DrawCharts();
        }

        protected override void OnBeforeDisable()
        {
            _serialized?.Dispose();
            _serialized = null;
        }

        private string EncodeCurrent() => JAPSEncoder.Encode(_song, _song.ClipPath ?? string.Empty);

        protected override void OnLoaded()
        {
            _song = null;
            _originalSong = null;
            _baseline = null;
            _blockChart = new List<ExternalChartMeta>();
            _chartBlocks = new List<(int start, int end)>();
            _chartsEditable = false;

            if (Text == null) return;

            try
            {
                _song = JAPSDecoder.Decode(Text);
                _originalSong = JAPSDecoder.Decode(Text);
                _baseline = JAPSEncoder.Encode(_originalSong, _originalSong.ClipPath ?? string.Empty);

                BuildChartBlockIndex();
            }
            catch (System.Exception e)
            {
                _song = null;
                _originalSong = null;
                Debug.LogException(e);
            }
        }

        private void BuildChartBlockIndex()
        {
            List<string> lines = new(Text.Split('\n'));

            if (!TryFindSection(lines, "[CHARTS]", out int start, out int end)) return;

            int i = start;

            while (i < end && i < lines.Count)
            {
                if (!IsChartStart(lines[i]))
                {
                    i++;
                    continue;
                }

                int blockStart = i;
                i++;

                while (i < end && i < lines.Count && !IsChartStart(lines[i])) i++;

                _chartBlocks.Add((blockStart, i));
            }

            _chartsEditable = _chartBlocks.Count == _song.Charts.Count;

            for (int k = 0; k < _chartBlocks.Count && k < _song.Charts.Count; k++)
                _blockChart.Add(_song.Charts[k]);
        }

        private static bool IsChartStart(string line)
        {
            string trimmed = line.Trim();
            if (!trimmed.StartsWith("+")) return false;

            string[] tokens = trimmed.Split(' ', System.StringSplitOptions.RemoveEmptyEntries);

            return tokens.Length >= 2 && tokens[1] == "Chart";
        }

        // ---------------------------------------------------------------------
        // Surgical writer: reproduce the original file, replacing only the lines
        // whose managed value changed. Unknown sections, keys and formatting survive.
        // ---------------------------------------------------------------------

        private string BuildText()
        {
            List<string> lines = new(Text.Split('\n'));

            var sections = new List<(int start, int end, string header)>();

            if (TryFindSection(lines, "[METADATA]", out int ms, out int me)) sections.Add((ms, me, "[METADATA]"));
            if (TryFindSection(lines, "[RESOURCES]", out int rs, out int re)) sections.Add((rs, re, "[RESOURCES]"));
            if (TryFindSection(lines, "[COVER]", out int cs, out int ce)) sections.Add((cs, ce, "[COVER]"));
            if (TryFindSection(lines, "[COLORS]", out int os, out int oe)) sections.Add((os, oe, "[COLORS]"));
            if (TryFindSection(lines, "[CHARTS]", out int hs, out int he)) sections.Add((hs, he, "[CHARTS]"));

            sections.Sort((a, b) => a.start.CompareTo(b.start));

            List<string> result = new(lines.Count + 8);
            int pos = 0;

            foreach ((int start, int end, string header) in sections)
            {
                for (int i = pos; i < start && i < lines.Count; i++)
                    result.Add(lines[i]);

                if (header == "[CHARTS]")
                    ProcessCharts(result, lines, start, end);
                else
                    ProcessScalarSection(result, lines, header, start, end);

                pos = end;
            }

            for (int i = pos; i < lines.Count; i++)
                result.Add(lines[i]);

            return string.Join("\n", result);
        }

        private void ProcessScalarSection(List<string> result, List<string> lines, string header, int start, int end)
        {
            KeyValue[] entries = GetScalarEntries(header);

            var present = new HashSet<string>();

            for (int i = start; i < end && i < lines.Count; i++)
                if (TryTopLevelKey(lines[i], out string key, out _) && HasEntry(entries, key))
                    present.Add(key);

            var missing = new List<KeyValue>();
            foreach (KeyValue entry in entries)
                if (entry.Changed && !present.Contains(entry.Key))
                    missing.Add(entry);

            bool inserted = false;

            for (int i = start; i < end && i < lines.Count; i++)
            {
                string line = lines[i];

                // Insert a changed-but-absent key before this section's sub-objects (e.g. cover layers).
                if (!inserted && missing.Count > 0 && line.TrimStart().StartsWith("+"))
                {
                    foreach (KeyValue entry in missing)
                        result.Add(entry.Key + ": " + entry.Value);

                    inserted = true;
                }

                if (TryTopLevelKey(line, out string key, out int separator) && TryGetEntry(entries, key, out KeyValue current))
                {
                    result.Add(current.Changed ? ReplaceValue(line, separator, current.Value) : line);
                    continue;
                }

                result.Add(line);
            }

            if (!inserted)
                foreach (KeyValue entry in missing)
                    result.Add(entry.Key + ": " + entry.Value);
        }

        private void ProcessCharts(List<string> result, List<string> lines, int start, int end)
        {
            if (!_chartsEditable)
            {
                for (int i = start; i < end && i < lines.Count; i++)
                    result.Add(lines[i]);

                return;
            }

            int blockIndex = 0;

            for (int i = start; i < end && i < lines.Count;)
            {
                if (blockIndex < _chartBlocks.Count && _chartBlocks[blockIndex].start == i)
                {
                    (int blockStart, int blockEnd) = _chartBlocks[blockIndex];

                    if (_song.Charts.Contains(_blockChart[blockIndex]))
                        AppendChartBlock(result, lines, blockStart, blockEnd, _blockChart[blockIndex], _originalSong.Charts[blockIndex]);

                    i = blockEnd;
                    blockIndex++;
                }
                else
                {
                    result.Add(lines[i]);
                    i++;
                }
            }

            foreach (ExternalChartMeta chart in _song.Charts)
                if (!_blockChart.Contains(chart))
                    AppendNewChart(result, chart);
        }

        private static void AppendChartBlock(List<string> result, List<string> lines, int start, int end, ExternalChartMeta current, ExternalChartMeta original)
        {
            KeyValue[] entries =
            {
                new("Target", current.Target ?? "", !SameString(current.Target, original.Target)),
                new("Index", current.DifficultyIndex.ToString(CultureInfo.InvariantCulture), current.DifficultyIndex != original.DifficultyIndex),
                new("Name", current.DifficultyName ?? "", !SameString(current.DifficultyName, original.DifficultyName)),
                new("Charter", current.CharterName ?? "", !SameString(current.CharterName, original.CharterName)),
                new("Level", current.DifficultyLevel ?? "", !SameString(current.DifficultyLevel, original.DifficultyLevel)),
                new("Constant", FormatFloat(current.ChartConstant), current.ChartConstant != original.ChartConstant),
            };

            var present = new HashSet<string>();

            for (int i = start; i < end && i < lines.Count; i++)
                if (TryKey(lines[i], out string key, out _) && HasEntry(entries, key))
                    present.Add(key);

            for (int i = start; i < end && i < lines.Count; i++)
            {
                string line = lines[i];

                if (TryKey(line, out string key, out int separator) && TryGetEntry(entries, key, out KeyValue current_entry))
                {
                    result.Add(current_entry.Changed ? ReplaceValue(line, separator, current_entry.Value) : line);
                    continue;
                }

                result.Add(line);
            }

            foreach (KeyValue entry in entries)
                if (entry.Changed && !present.Contains(entry.Key))
                    result.Add("  " + entry.Key + ": " + entry.Value);
        }

        private static void AppendNewChart(List<string> result, ExternalChartMeta chart)
        {
            result.Add("+ Chart");
            result.Add("  Target: " + (chart.Target ?? ""));
            result.Add("  Index: " + chart.DifficultyIndex.ToString(CultureInfo.InvariantCulture));
            result.Add("  Name: " + (chart.DifficultyName ?? ""));
            result.Add("  Charter: " + (chart.CharterName ?? ""));
            result.Add("  Level: " + (chart.DifficultyLevel ?? ""));
            result.Add("  Constant: " + FormatFloat(chart.ChartConstant));
        }

        private KeyValue[] GetScalarEntries(string header)
        {
            switch (header)
            {
                case "[METADATA]":
                    return new[]
                    {
                        new KeyValue("Name", _song.SongName, !SameString(_song.SongName, _originalSong.SongName)),
                        new KeyValue("Alt Name", _song.AltSongName, !SameString(_song.AltSongName, _originalSong.AltSongName)),
                        new KeyValue("Artist", _song.SongArtist, !SameString(_song.SongArtist, _originalSong.SongArtist)),
                        new KeyValue("Alt Artist", _song.AltSongArtist, !SameString(_song.AltSongArtist, _originalSong.AltSongArtist)),
                        new KeyValue("Genre", _song.Genre, !SameString(_song.Genre, _originalSong.Genre)),
                        new KeyValue("Location", _song.Location, !SameString(_song.Location, _originalSong.Location)),
                        new KeyValue("Preview Range", FormatVector2(_song.PreviewRange), !SameVector2(_song.PreviewRange, _originalSong.PreviewRange)),
                    };

                case "[RESOURCES]":
                    return new[]
                    {
                        new KeyValue("Clip", _song.ClipPath ?? "", !SameString(_song.ClipPath, _originalSong.ClipPath)),
                    };

                case "[COVER]":
                    return new[]
                    {
                        new KeyValue("Artist", _song.Cover.ArtistName, !SameString(_song.Cover.ArtistName, _originalSong.Cover.ArtistName)),
                        new KeyValue("Alt Artist", _song.Cover.AltArtistName, !SameString(_song.Cover.AltArtistName, _originalSong.Cover.AltArtistName)),
                        new KeyValue("Background", FormatColor(_song.Cover.BackgroundColor), !SameColor(_song.Cover.BackgroundColor, _originalSong.Cover.BackgroundColor)),
                        new KeyValue("Icon", _song.Cover.IconTarget, !SameString(_song.Cover.IconTarget, _originalSong.Cover.IconTarget)),
                        new KeyValue("Icon Center", FormatVector2(_song.Cover.IconCenter), !SameVector2(_song.Cover.IconCenter, _originalSong.Cover.IconCenter)),
                        new KeyValue("Icon Size", FormatFloat(_song.Cover.IconSize), _song.Cover.IconSize != _originalSong.Cover.IconSize),
                    };

                case "[COLORS]":
                    return new[]
                    {
                        new KeyValue("Background", FormatColor(_song.BackgroundColor), !SameColor(_song.BackgroundColor, _originalSong.BackgroundColor)),
                        new KeyValue("Interface", FormatColor(_song.InterfaceColor), !SameColor(_song.InterfaceColor, _originalSong.InterfaceColor)),
                    };
            }

            return System.Array.Empty<KeyValue>();
        }

        private static bool TryTopLevelKey(string line, out string key, out int separator)
        {
            key = null;
            separator = -1;

            if (line.Length == 0 || char.IsWhiteSpace(line[0])) return false;

            int colon = line.IndexOf(": ", System.StringComparison.InvariantCulture);
            if (colon <= 0) return false;

            key = line[..colon].Trim();
            separator = colon;

            return key.Length > 0;
        }

        private static bool TryKey(string line, out string key, out int separator)
        {
            key = null;
            separator = -1;

            int colon = line.IndexOf(':');
            if (colon <= 0) return false;

            key = line[..colon].Trim();
            separator = colon;

            return key.Length > 0;
        }

        private static bool HasEntry(KeyValue[] entries, string key)
        {
            foreach (KeyValue entry in entries)
                if (entry.Key == key)
                    return true;

            return false;
        }

        private static bool TryGetEntry(KeyValue[] entries, string key, out KeyValue entry)
        {
            foreach (KeyValue candidate in entries)
            {
                if (candidate.Key != key) continue;

                entry = candidate;
                return true;
            }

            entry = default;
            return false;
        }

        private static string FormatFloat(float value) => value.ToString(CultureInfo.InvariantCulture);

        private static string FormatVector2(Vector2 value) => FormatFloat(value.x) + " " + FormatFloat(value.y);

        private static string FormatColor(Color value) =>
            FormatFloat(value.r) + " " + FormatFloat(value.g) + " " + FormatFloat(value.b) + " " + FormatFloat(value.a);

        private static bool SameString(string a, string b) => (a ?? "") == (b ?? "");

        private static bool SameVector2(Vector2 a, Vector2 b) => a.x == b.x && a.y == b.y;

        private static bool SameColor(Color a, Color b) => a.r == b.r && a.g == b.g && a.b == b.b && a.a == b.a;

        // ---------------------------------------------------------------------
        // GUI
        // ---------------------------------------------------------------------

        private static void Section(string title)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
        }

        private static void EndSection()
        {
            EditorGUI.indentLevel--;
        }

        private void DrawMetadata()
        {
            Section("Metadata");
            _song.SongName = EditorGUILayout.TextField("Name", _song.SongName);
            _song.AltSongName = EditorGUILayout.TextField("Alt Name", _song.AltSongName);
            _song.SongArtist = EditorGUILayout.TextField("Artist", _song.SongArtist);
            _song.AltSongArtist = EditorGUILayout.TextField("Alt Artist", _song.AltSongArtist);
            _song.Genre = EditorGUILayout.TextField("Genre", _song.Genre);
            _song.Location = EditorGUILayout.TextField("Location", _song.Location);
            _song.PreviewRange = EditorGUILayout.Vector2Field("Preview Range", _song.PreviewRange);
            EndSection();
        }

        private static readonly string[] AudioExtensions =
        {
            ".wav", ".mp3", ".ogg", ".aif", ".aiff", ".flac", ".mod", ".it", ".s3m", ".xm"
        };

        private static readonly string[] ImageExtensions =
        {
            ".png", ".jpg", ".jpeg"
        };

        private void DrawResources()
        {
            Section("Resources");

            string[] clips = GetFilesWithExtensions(Path.GetDirectoryName(AssetPath), AudioExtensions);
            _song.ClipPath = DrawTargetPopup("Clip", _song.ClipPath, clips);

            EndSection();
        }

        private void DrawCover()
        {
            Section("Cover");

            Cover cover = _song.Cover;
            cover.ArtistName = EditorGUILayout.TextField("Artist", cover.ArtistName);
            cover.AltArtistName = EditorGUILayout.TextField("Alt Artist", cover.AltArtistName);
            cover.BackgroundColor = EditorGUILayout.ColorField("Background", cover.BackgroundColor);

            string[] icons = GetFilesWithExtensions(Path.GetDirectoryName(AssetPath), ImageExtensions);
            cover.IconTarget = DrawTargetPopup("Icon", cover.IconTarget, icons);

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.Vector2Field("Icon Center", cover.IconCenter);
                EditorGUILayout.FloatField("Icon Size", cover.IconSize);
            }

            DrawLayers(cover.Layers);

            EndSection();
        }

        private void DrawLayers(List<CoverLayer> layers)
        {
            _showLayers = EditorGUILayout.BeginFoldoutHeaderGroup(_showLayers, $"Layers ({layers.Count})");
            EditorGUILayout.EndFoldoutHeaderGroup();
            if (!_showLayers) return;

            SerializedProperty property = GetSerialized()?.FindProperty("Data.Cover.Layers");
            if (property == null) return;

            EditorGUI.indentLevel++;
            using (new EditorGUI.DisabledScope(true))
                for (int i = 0; i < property.arraySize; i++)
                    EditorGUILayout.PropertyField(property.GetArrayElementAtIndex(i), true);
            EditorGUI.indentLevel--;
        }

        private void DrawColors()
        {
            Section("Colors");
            _song.BackgroundColor = EditorGUILayout.ColorField("Background", _song.BackgroundColor);
            _song.InterfaceColor = EditorGUILayout.ColorField("Interface", _song.InterfaceColor);
            EndSection();
        }

        private void DrawTiming()
        {
            int count = _song.Timing.Stops.Count;

            EditorGUILayout.Space();
            _showTiming = EditorGUILayout.BeginFoldoutHeaderGroup(_showTiming, $"Timing — {count} BPM stop{(count == 1 ? "" : "s")} (read-only)");
            EditorGUILayout.EndFoldoutHeaderGroup();
            if (!_showTiming) return;

            SerializedProperty property = GetSerialized()?.FindProperty("Data.Timing.Stops");
            if (property == null) return;

            EditorGUI.indentLevel++;
            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.PropertyField(property, true);
            EditorGUI.indentLevel--;
        }

        private void DrawCharts()
        {
            List<ExternalChartMeta> charts = _song.Charts;
            string[] targets = GetChartTargets();

            EditorGUILayout.Space();
            _showCharts = EditorGUILayout.BeginFoldoutHeaderGroup(_showCharts, $"Charts ({charts.Count})");
            EditorGUILayout.EndFoldoutHeaderGroup();
            if (!_showCharts) return;

            EditorGUI.indentLevel++;

            if (!_chartsEditable)
                EditorGUILayout.HelpBox("This file's charts couldn't be matched to their text blocks, so they're display-only.", MessageType.Warning);

            int remove = -1;

            using (new EditorGUI.DisabledScope(!_chartsEditable))
            {
                for (int i = 0; i < charts.Count; i++)
                {
                    using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                    {
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            EditorGUILayout.LabelField($"Chart {i}", EditorStyles.boldLabel);
                            GUILayout.FlexibleSpace();
                            if (GUILayout.Button("Remove", GUILayout.Width(70))) remove = i;
                        }

                        ExternalChartMeta chart = charts[i];
                        chart.Target = DrawTargetPopup("Target", chart.Target, targets);
                        chart.DifficultyName = EditorGUILayout.TextField("Name", chart.DifficultyName);
                        chart.DifficultyLevel = EditorGUILayout.TextField("Level", chart.DifficultyLevel);
                        chart.DifficultyIndex = EditorGUILayout.IntField("Index", chart.DifficultyIndex);
                        chart.ChartConstant = EditorGUILayout.FloatField("Constant", chart.ChartConstant);
                        chart.CharterName = EditorGUILayout.TextField("Charter", chart.CharterName);
                    }
                }

                if (remove >= 0)
                    charts.RemoveAt(remove);

                if (GUILayout.Button("Add Chart"))
                    charts.Add(new ExternalChartMeta { Target = targets.Length > 0 ? targets[0] : "" });
            }

            EditorGUI.indentLevel--;
        }

        private string[] GetChartTargets()
        {
            string[] files = GetFilesWithExtensions(Path.GetDirectoryName(AssetPath), ".jac");

            var targets = new List<string>(files.Length);
            foreach (string file in files)
                targets.Add(Path.GetFileNameWithoutExtension(file));

            return targets.ToArray();
        }

        private static string[] GetFilesWithExtensions(string folder, params string[] extensions)
        {
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder)) return new string[0];

            var files = new List<string>();

            foreach (string file in Directory.GetFiles(folder, "*", SearchOption.TopDirectoryOnly))
            {
                string extension = Path.GetExtension(file);

                foreach (string candidate in extensions)
                {
                    if (!string.Equals(extension, candidate, System.StringComparison.OrdinalIgnoreCase)) continue;

                    files.Add(Path.GetFileName(file));
                    break;
                }
            }

            files.Sort(System.StringComparer.OrdinalIgnoreCase);

            return files.ToArray();
        }

        private static string DrawTargetPopup(string label, string current, string[] options)
        {
            var list = new List<string> { "(none)" };
            list.AddRange(options);

            int index = 0;

            if (!string.IsNullOrEmpty(current))
            {
                int found = System.Array.IndexOf(options, current);

                if (found >= 0)
                {
                    index = found + 1;
                }
                else
                {
                    // Preserve a value that doesn't resolve to a file beside this asset.
                    list.Add(current);
                    index = list.Count - 1;
                }
            }

            int next = EditorGUILayout.Popup(label, index, list.ToArray());

            return next <= 0 || next >= list.Count ? "" : list[next];
        }
    }
}
