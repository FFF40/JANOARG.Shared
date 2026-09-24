using System.Collections.Generic;
using System.Globalization;
using JANOARG.Shared.Data.ChartInfo;
using UnityEditor;

namespace JANOARG.Shared.Data.Files.Editor
{
    [CustomEditor(typeof(JACImporter))]
    internal class JACImporterEditor : TextBackedImporterEditor
    {
        // Only these fields are editable; everything else in the .jac is left byte-for-byte untouched.
        // A full decode->encode is avoided because: (1) Storyboard.Add uses an unstable sort, so
        // same-offset timestamps can be permuted (semantically neutral across IDs, but still a diff);
        // (2) the decoder rejects newer sections such as [EXTRAS], so some charts cannot be decoded;
        // (3) unknown keys would be silently dropped by a re-encode.
        private struct JacMetadata
        {
            public string DifficultyName;
            public string DifficultyLevel;
            public int    DifficultyIndex;
            public float  ChartConstant;
            public string CharterName;
            public string AltCharterName;
        }

        private JacMetadata _meta;
        private JacMetadata _originalMeta;
        private bool        _loaded;

        private SerializedObject _serialized;
        private bool             _showData;

        private static readonly HashSet<string> EditableFields = new()
        {
            "DifficultyName", "DifficultyLevel", "DifficultyIndex", "ChartConstant", "CharterName", "AltCharterName"
        };

        protected override bool Ready => _loaded;
        protected override string NotReadyMessage => "This file has no [METADATA] section, or could not be read.";
        protected override string SingleTargetMessage => "Select a single .jac file to edit its metadata.";

        protected override bool HasChanges() => _loaded && !MetaEquals(_meta, _originalMeta);

        protected override void OnLoaded()
        {
            _meta = default;
            _originalMeta = default;
            _loaded = false;

            if (Text == null) return;

            var lines = new List<string>(Text.Split('\n'));
            if (!TryFindSection(lines, "[METADATA]", out int start, out int end)) return;

            _originalMeta = ReadMetadata(lines, start, end);
            _meta = _originalMeta;
            _loaded = true;
        }

        protected override string Build() => BuildText(_meta);

        protected override void DrawInspector()
        {
            EditorGUILayout.HelpBox("Only the metadata below is editable. The rest of the chart is shown read-only and the .jac is otherwise left untouched.", MessageType.Info);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Metadata", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;

            _meta.DifficultyName = EditorGUILayout.TextField("Difficulty Name", _meta.DifficultyName);
            _meta.DifficultyLevel = EditorGUILayout.TextField("Difficulty Level", _meta.DifficultyLevel);
            _meta.DifficultyIndex = EditorGUILayout.IntField("Difficulty Index", _meta.DifficultyIndex);
            _meta.ChartConstant = EditorGUILayout.FloatField("Chart Constant", _meta.ChartConstant);
            _meta.CharterName = EditorGUILayout.TextField("Charter Name", _meta.CharterName);
            _meta.AltCharterName = EditorGUILayout.TextField("Alt Charter Name", _meta.AltCharterName);

            EditorGUI.indentLevel--;

            DrawChartData();
        }

        protected override void OnBeforeDisable()
        {
            _serialized?.Dispose();
            _serialized = null;
        }

        private void DrawChartData()
        {
            ExternalChart chart = AssetDatabase.LoadMainAssetAtPath(AssetPath) as ExternalChart;
            if (chart == null) return;

            if (_serialized == null || _serialized.targetObject != chart)
                _serialized = new SerializedObject(chart);

            _serialized.Update();

            SerializedProperty data = _serialized.FindProperty("Data");
            if (data == null) return;

            EditorGUILayout.Space();
            _showData = EditorGUILayout.BeginFoldoutHeaderGroup(_showData, "Chart Data (read-only)");
            EditorGUILayout.EndFoldoutHeaderGroup();
            if (!_showData) return;

            EditorGUI.indentLevel++;

            // Unity's property fields stay browsable under a DisabledScope (foldouts are special-cased
            // to still expand while disabled), while every value field, size field and array control is
            // non-editable. So we get the native inspector look and read-only behaviour for free.
            using (new EditorGUI.DisabledScope(true))
            {
                SerializedProperty end = data.GetEndProperty();
                SerializedProperty child = data.Copy();
                bool enterChildren = true;

                while (child.NextVisible(enterChildren) && !SerializedProperty.EqualContents(child, end))
                {
                    enterChildren = false;

                    if (EditableFields.Contains(child.name)) continue;

                    EditorGUILayout.PropertyField(child, true);
                }
            }

            EditorGUI.indentLevel--;
        }

        private string BuildText(JacMetadata meta)
        {
            var lines = new List<string>(Text.Split('\n'));
            if (!TryFindSection(lines, "[METADATA]", out int start, out int end)) return Text;

            Replace(lines, start, end, "Index", meta.DifficultyIndex.ToString(CultureInfo.InvariantCulture),
                meta.DifficultyIndex != _originalMeta.DifficultyIndex);
            Replace(lines, start, end, "Name", meta.DifficultyName,
                meta.DifficultyName != _originalMeta.DifficultyName);
            Replace(lines, start, end, "Charter", meta.CharterName,
                meta.CharterName != _originalMeta.CharterName);
            Replace(lines, start, end, "Level", meta.DifficultyLevel,
                meta.DifficultyLevel != _originalMeta.DifficultyLevel);
            Replace(lines, start, end, "Constant", meta.ChartConstant.ToString(CultureInfo.InvariantCulture),
                meta.ChartConstant != _originalMeta.ChartConstant);

            if (meta.AltCharterName != _originalMeta.AltCharterName)
                SetAltCharter(lines, meta.AltCharterName);

            return string.Join("\n", lines);
        }

        private static void Replace(List<string> lines, int start, int end, string key, string value, bool changed)
        {
            if (!changed) return;

            for (int i = start; i < end && i < lines.Count; i++)
            {
                int separator = lines[i].IndexOf(": ", System.StringComparison.InvariantCulture);
                if (separator < 0 || lines[i][..separator].Trim() != key) continue;

                lines[i] = ReplaceValue(lines[i], separator, value);
                return;
            }
        }

        private static void SetAltCharter(List<string> lines, string value)
        {
            if (!TryFindSection(lines, "[METADATA]", out int start, out int end)) return;

            for (int i = start; i < end && i < lines.Count; i++)
            {
                int separator = lines[i].IndexOf(": ", System.StringComparison.InvariantCulture);
                if (separator < 0 || lines[i][..separator].Trim() != "Alt Charter") continue;

                if (string.IsNullOrEmpty(value))
                {
                    lines.RemoveAt(i);
                    return;
                }

                lines[i] = ReplaceValue(lines[i], separator, value);
                return;
            }

            if (string.IsNullOrEmpty(value)) return;

            // Insert directly after the Charter line, matching the encoder's ordering.
            int insertAt = end;

            for (int i = start; i < end && i < lines.Count; i++)
            {
                int separator = lines[i].IndexOf(": ", System.StringComparison.InvariantCulture);
                if (separator >= 0 && lines[i][..separator].Trim() == "Charter")
                {
                    insertAt = i + 1;
                    break;
                }
            }

            lines.Insert(insertAt, "Alt Charter:  " + value);
        }

        private static JacMetadata ReadMetadata(List<string> lines, int start, int end)
        {
            var meta = new JacMetadata
            {
                DifficultyName = "Normal",
                DifficultyLevel = "6",
                DifficultyIndex = 1,
                ChartConstant = 6,
                CharterName = "",
                AltCharterName = ""
            };

            for (int i = start; i < end && i < lines.Count; i++)
            {
                string line = lines[i].Trim();
                int separator = line.IndexOf(": ", System.StringComparison.InvariantCulture);
                if (separator < 0) continue;

                string key = line[..separator].Trim();
                string value = line[(separator + 2)..].Trim();

                switch (key)
                {
                    case "Index":
                        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out meta.DifficultyIndex);
                        break;
                    case "Name":
                        meta.DifficultyName = value;
                        break;
                    case "Charter":
                        meta.CharterName = value;
                        break;
                    case "Alt Charter":
                        meta.AltCharterName = value;
                        break;
                    case "Level":
                        meta.DifficultyLevel = value;
                        break;
                    case "Constant":
                        float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out meta.ChartConstant);
                        break;
                }
            }

            return meta;
        }

        private static bool MetaEquals(JacMetadata a, JacMetadata b)
        {
            return a.DifficultyName == b.DifficultyName
                && a.DifficultyLevel == b.DifficultyLevel
                && a.DifficultyIndex == b.DifficultyIndex
                && a.ChartConstant == b.ChartConstant
                && a.CharterName == b.CharterName
                && a.AltCharterName == b.AltCharterName;
        }
    }
}
