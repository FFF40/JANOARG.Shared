using System.IO;
using JANOARG.Shared.Data.ChartInfo;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace JANOARG.Shared.Data.Files.Editor
{
    [ScriptedImporter(2, "japs", 1000)]
    public class JAPSImporter : ScriptedImporter
    {
        public override void OnImportAsset(AssetImportContext ctx)
        {
            PlayableSong song = JAPSDecoder.Decode(File.ReadAllText(ctx.assetPath));

            var ext = ScriptableObject.CreateInstance<ExternalPlayableSong>();
            ext.Data = song;

            string clipPath = Path.Combine(
                Path.GetDirectoryName(ctx.assetPath) ?? string.Empty,
                song.ClipPath ?? string.Empty);

            // Without this the artifact is not rebuilt when the clip changes, moves, or is imported
            // after this asset - leaving song.Clip null in the bake even though the file is correct.
            ctx.DependsOnSourceAsset(clipPath);

            song.Clip = AssetDatabase.LoadAssetAtPath<AudioClip>(clipPath);

            ctx.AddObjectToAsset("main obj", ext);
            ctx.SetMainObject(ext);
        }
    }
}