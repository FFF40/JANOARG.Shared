using System;
using System.Globalization;
using JANOARG.Shared.Data.ChartInfo;
using UnityEngine;

namespace JANOARG.Shared.Data.Files
{
    public class JAPSDecoder
    {
        public const int FORMAT_VERSION = 2;
        public const int INDENT_SIZE    = 2;

        public static PlayableSong Decode(string str)
        {
            PlayableSong decodingSong = new();

            decodingSong.Timing.Stops.Clear();

            var mode = "";

            object currentObject = null;

            string[] lines = str.Split("\n");
            var index = 0;

            try
            {
                foreach (string l in lines)
                {
                    
                    string line = l.TrimStart();

                    bool isInSection = line.StartsWith("[") && line.EndsWith("]");
                    bool isObjectToken = line.StartsWith("+");
                    bool isMetadata = line.Contains(": ");
                    
                    index++;

                    if (isInSection)
                    {
                        mode = line[1..^1];

                        switch (mode)
                        {
                            case "VERSION":
                                currentObject = "version";

                                break;
                            case "METADATA":
                            case "RESOURCES":
                                currentObject = decodingSong;

                                break;
                            case "COVER":
                                currentObject = decodingSong.Cover;

                                break;
                            case "COLORS":
                                currentObject = decodingSong;

                                break;
                            case "TIMING":
                                currentObject = decodingSong.Timing;

                                break;
                            case "CHARTS":
                                currentObject = decodingSong.Charts;

                                break;
                            default:
                                throw new Exception("The specified mode " + mode + " is not a valid mode.");
                        }
                    }
                    else if (isObjectToken)
                    {
                        string[] tokens = line.Split(' ');

                        string objectType = tokens[1];
                        
                        if (tokens.Length < 2)
                            throw new Exception("Object token expected but not found.");

                        switch (objectType)
                        {
                            case "Layer":
                            {
                                if (tokens.Length < 6)
                                    throw new Exception("Not enough tokens (minimum 6, got " + tokens.Length + ").");
                                
                                CoverLayer layer = new()
                                {
                                    Scale = ParseFloat(tokens[2]),
                                    Position = new Vector2(ParseFloat(tokens[3]), ParseFloat(tokens[4])),
                                    ParallaxFactor = ParseFloat(tokens[5])
                                };

                                decodingSong.Cover.Layers.Add(layer);
                                currentObject = layer;

                                break;
                            }
                            case "BPM":
                            {
                                if (tokens.Length < 6)
                                    throw new Exception("Not enough tokens (minimum 6, got " + tokens.Length + ").");
                                
                                BPMStop stop = new(ParseFloat(tokens[3]), ParseFloat(tokens[2]))
                                {
                                    Signature = ParseInt(tokens[4]),
                                    Significant = tokens[5] == "S"
                                };

                                decodingSong.Timing.Stops.Add(stop);
                                currentObject = stop;

                                break;
                            }
                            case "Chart":
                            {
                                ExternalChartMeta chart = new();

                                decodingSong.Charts.Add(chart);
                                currentObject = chart;

                                break;
                            }
                            default:
                                throw new Exception("The specified object " + objectType + " is not a valid object.");
                        }
                    }
                    else if (isMetadata)
                    {
                        int pos = line.IndexOf(": ", StringComparison.InvariantCulture);
                        string key = line[..pos];
                        string value = line[(pos + 2)..];

                        switch (currentObject)
                        {
                            case PlayableSong song:
                            {
                                switch (key)
                                {
                                    case "Name":
                                        song.SongName = value;

                                        break;
                                    case "Alt Name":
                                        song.AltSongName = value;

                                        break;
                                    case "Artist":
                                        song.SongArtist = value;

                                        break;
                                    case "Alt Artist":
                                        song.AltSongArtist = value;

                                        break;
                                    case "Genre":
                                        song.Genre = value;

                                        break;
                                    case "Location":
                                        song.Location = value;

                                        break;
                                    case "Preview Range":
                                        song.PreviewRange = ParseVector(value);

                                        break;
                                    case "Clip":
                                        song.ClipPath = value;

                                        break;
                                    case "Background":
                                        song.BackgroundColor = ParseColor(value);

                                        break;
                                    case "Interface":
                                        song.InterfaceColor = ParseColor(value);

                                        break;
                                }

                                break;
                            }
                            case Cover cover when key == "Artist":
                                cover.ArtistName = value;

                                break;
                            case Cover cover when key == "Alt Artist":
                                cover.AltArtistName = value;

                                break;
                            case Cover cover when key == "Background":
                                cover.BackgroundColor = ParseColor(value);

                                break;
                            case Cover cover when key == "Icon":
                                cover.IconTarget = value;

                                break;
                            case Cover cover when key == "Icon Center":
                                cover.IconCenter = ParseVector(value);

                                break;
                            case Cover cover:
                            {
                                if (key == "Icon Size") cover.IconSize = ParseFloat(value);

                                break;
                            }
                            case CoverLayer layer:
                            {
                                if (key == "Target") layer.Target = value;

                                break;
                            }
                            case ExternalChartMeta chart when key == "Target":
                                chart.Target = value;

                                break;
                            case ExternalChartMeta chart when key == "Index":
                                chart.DifficultyIndex = ParseInt(value);

                                break;
                            case ExternalChartMeta chart when key == "Name":
                                chart.DifficultyName = value;

                                break;
                            case ExternalChartMeta chart when key == "Charter":
                                chart.CharterName = value;

                                break;
                            case ExternalChartMeta chart when key == "Level":
                                chart.DifficultyLevel = value;

                                break;
                            case ExternalChartMeta chart:
                            {
                                if (key == "Constant") chart.ChartConstant = ParseFloat(value);

                                break;
                            }
                        }
                    }
                    else if (currentObject is CoverLayer layer)
                    {
                        if (line == "Tiling") 
                            layer.Tiling = true;
                    }
                    else if (currentObject?.ToString() == "version")
                    {
                        if (string.IsNullOrWhiteSpace(line)) 
                            continue;
                        
                        if (!int.TryParse(line, out int version)) 
                            continue;

                        if (version > FORMAT_VERSION)
                            throw new Exception("Chart version is newer than the supported format version. Please open this chart using a newer version of the Chartmaker.");
                    }
                }
            }
            catch (Exception e)
            {
                throw new Exception($"An error occurred while trying to decode line {index}:\nContent: {lines[index - 1]}\nException: {e}");
            }

            return decodingSong;
        }

        private static T ParseEnum<T>(string str) where T : Enum
        {
            return (T)Enum.Parse(typeof(T), str);
        }

        private static void ParseEasing(string str, out EaseFunction easing, out EaseMode easeMode)
        {
            string[] tokens = str.Split('/');

            if (tokens.Length == 2)
            {
                easing = (EaseFunction)Enum.Parse(typeof(EaseFunction), tokens[0]);
                easeMode = (EaseMode)Enum.Parse(typeof(EaseMode), tokens[1]);
            }
            else
            {
                throw new ArgumentException("The specified string is not in a valid Easing format");
            }
        }

        private static int ParseInt(string number)
        {
            return int.Parse(number, CultureInfo.InvariantCulture);
        }

        private static float ParseFloat(string number)
        {
            return float.Parse(number, CultureInfo.InvariantCulture);
        }

        private static float ParseTime(string number)
        {
            return float.Parse(number, CultureInfo.InvariantCulture);
        }

        private static Vector3 ParseVector(string str)
        {
            string[] tokens = str.Split(' ');

            return new Vector3(
                ParseFloat(tokens[0]), ParseFloat(tokens[1]),
                tokens.Length < 3 ? 0 : ParseFloat(tokens[2]));
        }

        private static Color ParseColor(string str)
        {
            string[] tokens = str.Split(' ');

            return new Color(
                ParseFloat(tokens[0]), ParseFloat(tokens[1]), ParseFloat(tokens[2]),
                ParseFloat(tokens[3]));
        }
    }
}