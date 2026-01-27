using System.Globalization;
using JANOARG.Shared.Data.ChartInfo;
using UnityEngine;

namespace JANOARG.Shared.Data.Files
{
    public class JAPSEncoder
    {
        public const int FORMAT_VERSION = 2;
        public const int INDENT_SIZE    = 2;

        public static string Encode(PlayableSong song, string clipName)
        {
            string InsertAltSongArtist() => !string.IsNullOrWhiteSpace(song.AltSongArtist) ? $"\nAlt Artist: {song.AltSongArtist}" : string.Empty;
            string InsertAltCoverArtist() => !string.IsNullOrWhiteSpace(song.Cover.AltArtistName) ? $"\nAlt Artist: {song.Cover.AltArtistName}" : string.Empty;
            
            string EncodeAllCoverLayers()
            {
                string result = string.Empty;
                foreach (CoverLayer layer in song.Cover.Layers)
                    result += EncodeCoverLayer(layer);
                return result;
            }
            
            string EncodeAllBPMStops()
            {
                string result = string.Empty;
                foreach (BPMStop stop in song.Timing.Stops)
                    result += EncodeBPMStop(stop);
                return result;
            }
            
            string EncodeAllExternalChartMetas()
            {
                string result = string.Empty;
                foreach (ExternalChartMeta chart in song.Charts)
                    result += EncodeExternalChartMeta(chart);
                return result;
            }
            
            string str = $@"JANOARG Playable Song Format
github.com/FFF40/JANOARG

[VERSION]
{FORMAT_VERSION}

[METADATA]
Name: {song.SongName}
Artist: {song.SongArtist}{InsertAltSongArtist()}
Genre: {song.Genre}
Location: {song.Location}
Preview Range: {EncodeVector(song.PreviewRange)}

[RESOURCES]
Clip: {clipName}

[COVER]
Artist: {song.Cover.ArtistName} {InsertAltCoverArtist()}
Background: {EncodeColor(song.Cover.BackgroundColor)}
Icon: {song.Cover.IconTarget}
Icon Center: {EncodeVector(song.Cover.IconCenter)}
Icon Size: {song.Cover.IconSize.ToString(CultureInfo.InvariantCulture)}
{EncodeAllCoverLayers()}

[COLORS]
Background: {EncodeColor(song.BackgroundColor)}
Interface: {EncodeColor(song.InterfaceColor)}

[TIMING]
{EncodeAllBPMStops()}

[CHARTS]
{EncodeAllExternalChartMetas()}";

            return str;
        }

        public static string EncodeCoverLayer(CoverLayer layer, int depth = 0)
        {
            string indent = new(' ', depth);
            string indent2 = new(' ', depth + INDENT_SIZE);

            string tilingFlag = layer.Tiling ? $"\n{indent2}Tiling" : string.Empty;
            
            string str = $"{indent}+ Layer {layer.Scale.ToString(CultureInfo.InvariantCulture)} {EncodeVector(layer.Position)} {layer.ParallaxFactor.ToString(CultureInfo.InvariantCulture)}\n{indent2}Target: {layer.Target}{tilingFlag}\n";

            return str;
        }

        public static string EncodeBPMStop(BPMStop stop, int depth = 0)
        {
            string indent = new(' ', depth);
            string indent2 = new(' ', depth + INDENT_SIZE);

            string significantFlag = stop.Significant ? "S" : "_";
            
            string str =
                $"{indent}+ BPM {stop.Offset.ToString(CultureInfo.InvariantCulture)} {stop.BPM.ToString(CultureInfo.InvariantCulture)} {stop.Signature.ToString(CultureInfo.InvariantCulture)} {significantFlag}\n";

            return str;
        }

        public static string EncodeExternalChartMeta(ExternalChartMeta chart, int depth = 0)
        {
            string indent = new(' ', depth);
            string indent2 = new(' ', depth + INDENT_SIZE);

            string str = $@" {indent}+ Chart
{indent2}Target: {chart.Target}
{indent2}Index: {chart.DifficultyIndex.ToString(CultureInfo.InvariantCulture)}
{indent2}Name: {chart.DifficultyName}
{indent2}Charter: {chart.CharterName}
{indent2}Level: {chart.DifficultyLevel}
{indent2}Constant: {chart.ChartConstant.ToString(CultureInfo.InvariantCulture)}
";

            return str;
        }

        public static string EncodeVector(Vector2 vec)
        {
            return vec.x.ToString(CultureInfo.InvariantCulture) + 
                   " " + 
                   vec.y.ToString(CultureInfo.InvariantCulture);
        }

        public static string EncodeColor(Color col)
        {
            return col.r.ToString(CultureInfo.InvariantCulture) +
                   " " +
                   col.g.ToString(CultureInfo.InvariantCulture) +
                   " " +
                   col.b.ToString(CultureInfo.InvariantCulture) +
                   " " +
                   col.a.ToString(CultureInfo.InvariantCulture);
        }
    }
}