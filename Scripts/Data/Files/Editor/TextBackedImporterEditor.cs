using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.AssetImporters;

namespace JANOARG.Shared.Data.Files.Editor
{
    /// <summary>
    /// Base for editors that treat the source text file as the model and splice edits back into it,
    /// instead of re-encoding the whole document. Handles the AssetImporterEditor lifecycle:
    /// load on enable, dirty detection, Apply (the Save button), Discard and the leave-inspector
    /// behaviour where Unity auto-applies (or prompts) when <see cref="HasModified"/> is true.
    /// </summary>
    internal abstract class TextBackedImporterEditor : ScriptedImporterEditor
    {
        /// <summary>The current on-disk text, or null when the file can't be read.</summary>
        protected string Text { get; private set; }

        // The imported object is a regenerated artifact, so don't show its default inspector.
        public override bool showImportedObject => false;

        protected string AssetPath => (target as AssetImporter)?.assetPath;

        protected abstract bool Ready { get; }
        protected virtual string NotReadyMessage => "Could not read this file.";
        protected virtual string SingleTargetMessage => "Select a single asset to edit.";

        /// <summary>True when the in-memory model differs from what was loaded.</summary>
        protected abstract bool HasChanges();

        /// <summary>Rebuild the in-memory model from <see cref="Text"/> (may be null).</summary>
        protected abstract void OnLoaded();

        /// <summary>Produce the updated text from the current in-memory model.</summary>
        protected abstract string Build();

        /// <summary>Draw the editor's body (the base draws the guard boxes and Apply/Revert footer).</summary>
        protected abstract void DrawInspector();

        public override void OnEnable()
        {
            base.OnEnable();
            if (Text == null) ReloadFromDisk();
        }

        public override void OnDisable()
        {
            OnBeforeDisable();
            base.OnDisable(); // may call SaveChanges() -> Apply() when HasModified() is true
        }

        protected virtual void OnBeforeDisable() { }

        public override bool HasModified() => Ready && HasChanges();

        protected override void Apply()
        {
            base.Apply();
            WriteBack();
        }

        public override void DiscardChanges()
        {
            base.DiscardChanges();
            ReloadFromDisk();
            Repaint();
        }

        public override void OnInspectorGUI()
        {
            if (targets.Length != 1)
            {
                EditorGUILayout.HelpBox(SingleTargetMessage, MessageType.Info);
                ApplyRevertGUI();
                return;
            }

            if (!Ready)
            {
                EditorGUILayout.HelpBox(NotReadyMessage, MessageType.Error);
                ApplyRevertGUI();
                return;
            }

            DrawInspector();
            ApplyRevertGUI();
        }

        protected void ReloadFromDisk()
        {
            Text = null;

            string path = AssetPath;
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
                Text = File.ReadAllText(path);

            OnLoaded();
        }

        /// <summary>Bounds of a <c>[SECTION]</c>'s content: header line index + 1 .. next header (exclusive).</summary>
        protected static bool TryFindSection(List<string> lines, string header, out int start, out int end)
        {
            start = -1;
            end = -1;

            for (int i = 0; i < lines.Count; i++)
            {
                if (lines[i].Trim() != header) continue;

                start = i + 1;
                break;
            }

            if (start < 0) return false;

            end = lines.Count;

            for (int i = start; i < lines.Count; i++)
            {
                string trimmed = lines[i].Trim();
                if (trimmed.StartsWith("[") && trimmed.EndsWith("]"))
                {
                    end = i;
                    break;
                }
            }

            return true;
        }

        /// <summary>Replace a <c>Key: value</c> line's value, preserving everything before the colon.</summary>
        protected static string ReplaceValue(string line, int separator, string value)
        {
            return line[..separator] + ": " + value;
        }

        private void WriteBack()
        {
            if (!Ready || !HasChanges())
            {
                hasUnsavedChanges = false;
                return;
            }

            string path = AssetPath;
            if (string.IsNullOrEmpty(path)) return;

            string updated = Build();

            // Nothing semantically changed -> leave the file byte-for-byte untouched.
            if (!File.Exists(path) || File.ReadAllText(path) != updated)
                File.WriteAllText(path, updated);

            ReloadFromDisk();
            hasUnsavedChanges = false;
        }
    }
}
