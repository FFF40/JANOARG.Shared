using System;
using System.Globalization;
using JANOARG.Shared.Data.ChartInfo;
using UnityEngine;

namespace JANOARG.Shared.Data.Files
{
    public class JACDecoder
    {
        public const int FORMAT_VERSION = 2;
        public const int INDENT_SIZE    = 2;

        public static Chart Decode(string str)
        {
            Chart chart = new();

            chart.Palette.LaneStyles.Clear();
            chart.Palette.HitStyles.Clear();

            var mode = "";

            Lane currentLane = null;
            object currentObject = null;
            Storyboard currentStoryboard = null;

            string[] lines = str.Split("\n");
            var index = 0;

            try
            {
                foreach (string l in lines)
                {
                    string line = l.TrimStart();
                    
                    bool isInSection = line.StartsWith("[") && line.EndsWith("]");
                    bool isStoryboardToken = line.StartsWith("$");
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
                                currentObject = chart;

                                break;
                            case "CAMERA":
                                currentObject = chart.Camera;
                                currentStoryboard = chart.Camera.Storyboard;

                                break;
                            case "PALLETE":
                                currentObject = chart.Palette;
                                currentStoryboard = chart.Palette.Storyboard;

                                break;
                            case "GROUPS":
                                currentObject = chart.Groups;
                                currentStoryboard = null;

                                break;
                            case "OBJECTS":
                                currentObject = chart.Lanes;
                                currentStoryboard = null;

                                break;
                            default:
                                throw new Exception("The specified mode " + mode + " is not a valid mode.");
                        }
                    }
                    else if (isStoryboardToken)
                    {
                        string[] tokens = line.Split(' ');

                        if (tokens.Length >= 6)
                        {
                            Timestamp ts = new()
                            {
                                ID = (TimestampIDs)Enum.Parse(typeof(TimestampIDs), tokens[1]),
                                Offset = ParseTime(tokens[2]),
                                Duration = ParseFloat(tokens[3]),
                                Target = ParseFloat(tokens[4]),
                                From = tokens[5] == "_" ? float.NaN : ParseFloat(tokens[5]),
                                Easing = ParseEasing(tokens[6])
                            };

                            currentStoryboard.Add(ts);
                        }
                        else
                        {
                            throw new Exception("Not enough tokens (minimum 6, got " + tokens.Length + ").");
                        }
                    }
                    else if (isObjectToken)
                    {
                        string[] tokens = line.Split(' ');

                        if (tokens.Length < 2) throw new Exception("Object token expected but not found.");

                        switch (tokens[1])
                        {
                            case "Group":
                            {
                                if (tokens.Length < 8)
                                    throw new Exception("Not enough tokens (minimum 8, got " + tokens.Length + ").");
                                
                                LaneGroup group = new()
                                {
                                    Position = new Vector3(ParseFloat(tokens[2]), ParseFloat(tokens[3]), ParseFloat(tokens[4])),
                                    Rotation = new Vector3(ParseFloat(tokens[5]), ParseFloat(tokens[6]), ParseFloat(tokens[7]))
                                };

                                currentObject = group;
                                currentStoryboard = group.Storyboard;
                                chart.Groups.Add(group);

                                break;
                            }
                            case "LaneStyle":
                            {
                                if (tokens.Length < 10)
                                    throw new Exception("Not enough tokens (minimum 10, got " + tokens.Length + ").");
                                
                                LaneStyle style = new()
                                {
                                    LaneColor = new Color(
                                        ParseFloat(tokens[2]), ParseFloat(tokens[3]),
                                        ParseFloat(tokens[4]), ParseFloat(tokens[5])),
                                    JudgeColor = new Color(
                                        ParseFloat(tokens[6]), ParseFloat(tokens[7]),
                                        ParseFloat(tokens[8]), ParseFloat(tokens[9]))
                                };

                                currentObject = style;
                                currentStoryboard = style.Storyboard;
                                chart.Palette.LaneStyles.Add(style);

                                break;
                            }
                            case "HitStyle":
                            {
                                if (tokens.Length < 14)
                                    throw new Exception("Not enough tokens (minimum 14, got " + tokens.Length + ").");
                                
                                HitStyle style = new()
                                {
                                    HoldTailColor = new Color(
                                        ParseFloat(tokens[2]), ParseFloat(tokens[3]),
                                        ParseFloat(tokens[4]), ParseFloat(tokens[5])),
                                    NormalColor = new Color(
                                        ParseFloat(tokens[6]), ParseFloat(tokens[7]),
                                        ParseFloat(tokens[8]), ParseFloat(tokens[9])),
                                    CatchColor = new Color(
                                        ParseFloat(tokens[10]), ParseFloat(tokens[11]),
                                        ParseFloat(tokens[12]), ParseFloat(tokens[13]))
                                };

                                currentObject = style;
                                currentStoryboard = style.Storyboard;
                                chart.Palette.HitStyles.Add(style);

                                break;
                            }
                            case "Lane":
                            {
                                if (tokens.Length < 9)
                                    throw new Exception("Not enough tokens (minimum 9, got " + tokens.Length + ").");
                                
                                Lane lane = new()
                                {
                                    Position = new Vector3(
                                        ParseFloat(tokens[2]), ParseFloat(tokens[3]),
                                        ParseFloat(tokens[4])),
                                    Rotation = new Vector3(
                                        ParseFloat(tokens[5]), ParseFloat(tokens[6]),
                                        ParseFloat(tokens[7])),
                                    StyleIndex = ParseInt(tokens[8])
                                };

                                currentObject = currentLane = lane;
                                currentStoryboard = lane.Storyboard;
                                chart.Lanes.Add(lane);

                                break;
                            }
                            case "LaneStep":
                            {
                                if (tokens.Length < 12)
                                    throw new Exception("Not enough tokens (minimum 12, got " + tokens.Length + ").");
                                
                                LaneStep step = new()
                                {
                                    Offset = ParseTime(tokens[2]),
                                    StartPointPosition =
                                        new Vector2(ParseFloat(tokens[3]), ParseFloat(tokens[4])),
                                    StartEaseX = ParseEasing(tokens[5]),
                                    StartEaseY = ParseEasing(tokens[6]),
                                    EndPointPosition = new Vector2(ParseFloat(tokens[7]), ParseFloat(tokens[8])),
                                    EndEaseX = ParseEasing(tokens[9]),
                                    EndEaseY = ParseEasing(tokens[10]),
                                    Speed = ParseFloat(tokens[11])
                                };

                                currentObject = step;
                                currentStoryboard = step.Storyboard;
                                currentLane.LaneSteps.Add(step);

                                break;
                            }
                            case "Hit":
                            {
                                if (tokens.Length < 10)
                                    throw new Exception("Not enough tokens (minimum 9, got " + tokens.Length + ").");
                                
                                HitObject hit = new()
                                {
                                    Type = ParseEnum<HitObject.HitType>(tokens[2]),
                                    Offset = ParseTime(tokens[3]),
                                    Position = ParseFloat(tokens[4]),
                                    Length = ParseFloat(tokens[5]),
                                    HoldLength = ParseFloat(tokens[6]),
                                    Flickable = tokens[7][0] == 'F',
                                    FlickDirection = tokens[7].Length > 1 ? ParseFloat(tokens[7][1..]) : float.NaN,
                                    StyleIndex = ParseInt(tokens[8]),
                                    IsFake = tokens[9] == "_"
                                };

                                currentObject = hit;
                                currentStoryboard = hit.Storyboard;
                                currentLane.Objects.Add(hit);

                                break;
                            }
                            default:
                                throw new Exception("The specified object " + tokens[1] + " is not a valid object.");
                        }
                    }
                    else if (isMetadata)
                    {
                        int pos = line.IndexOf(": ", StringComparison.InvariantCulture);
                        string key = line[..pos];
                        string value = line[(pos + 2)..].Trim();

                        switch (currentObject)
                        {
                            case Chart currentChart:
                                switch (key)
                                {
                                    case "Index":
                                        currentChart.DifficultyIndex = ParseInt(value);
                                        break;
                                    case "Name":
                                        currentChart.DifficultyName = value;
                                        break;
                                    case "Charter":
                                        currentChart.CharterName = value;
                                        break;
                                    case "Alt Charter":
                                        currentChart.AltCharterName = value;
                                        break;
                                    case "Level":
                                        currentChart.DifficultyLevel = value;
                                        break;
                                    case "Constant":
                                        currentChart.ChartConstant = ParseFloat(value);
                                        break;
                                }
                                break;
                            
                            case CameraController camera:
                                switch (key)
                                {
                                    case "Pivot":
                                        camera.CameraPivot = ParseVector(value);
                                        break;
                                    case "Rotation":
                                        camera.CameraRotation = ParseVector(value);
                                        break;
                                    case "Distance":
                                        camera.PivotDistance = ParseFloat(value);
                                        break;
                                }
                                break;
                            
                            case Palette palette:
                                switch (key)
                                {
                                    case "Background":
                                        palette.BackgroundColor = ParseColor(value);
                                        break;
                                    case "Interface":
                                        palette.InterfaceColor = ParseColor(value);
                                        break;
                                }
                                break;
                            
                            case LaneGroup group:
                                switch (key)
                                {
                                    case "Name":
                                        group.Name = value;
                                        break;
                                    case "Group":
                                        group.Group = value;
                                        break;
                                }
                                break;
                            
                            case LaneStyle laneStyle:
                                switch (key)
                                {
                                    case "Name":
                                        laneStyle.Name = value;
                                        break;
                                    case "Lane Material":
                                        laneStyle.LaneMaterial = value;
                                        break;
                                    case "Lane Target":
                                        laneStyle.LaneColorTarget = value;
                                        break;
                                    case "Judge Material":
                                        laneStyle.JudgeMaterial = value;
                                        break;
                                    case "Judge Target":
                                        laneStyle.JudgeColorTarget = value;
                                        break;
                                }
                                break;
                            
                            case HitStyle hitStyle:
                                switch (key)
                                {
                                    case "Name":
                                        hitStyle.Name = value;
                                        break;
                                    case "Main Material":
                                        hitStyle.MainMaterial = value;
                                        break;
                                    case "Main Target":
                                        hitStyle.MainColorTarget = value;
                                        break;
                                    case "Hold Tail Material":
                                        hitStyle.HoldTailMaterial = value;
                                        break;
                                    case "Hold Tail Target":
                                        hitStyle.HoldTailColorTarget = value;
                                        break;
                                }
                                break;
                            
                            case Lane lane:
                                switch (key)
                                {
                                    case "Name":
                                        lane.Name = value;
                                        break;
                                    case "Group":
                                        lane.Group = value;
                                        break;
                                }
                                break;
                        }
                    }
                    else if (currentObject?.ToString() == "version")
                    {
                        if (string.IsNullOrWhiteSpace(line)) 
                            continue;
                        if (!int.TryParse(line, out int version))
                            continue;

                        if (version > FORMAT_VERSION)
                            throw new Exception(
                                "Chart version is newer than the supported format version. Please open this chart using a newer version of the Chartmaker.");
                    }
                }
            }
            catch (Exception e)
            {
                throw new Exception(
                    "An error occurred while trying to decode line " +
                    index +
                    ":\nContent: " +
                    lines[index - 1] +
                    "\nException: " +
                    e);
            }

            return chart;
        }

        private static T ParseEnum<T>(string str) where T : Enum
        {
            return (T)Enum.Parse(typeof(T), str);
        }

        private static IEaseDirective ParseEasing(string str)
        {
            Debug.Log(str);

            if (str == "Linear") return new BasicEaseDirective(EaseFunction.Linear, EaseMode.In);

            string[] tokens = str.Split('/');

            if (tokens.Length == 2)
            {
                if (tokens[0] == "Bezier")
                {
                    string[] nums = tokens[1]
                        .Split(";");

                    return new CubicBezierEaseDirective(
                        new Vector2(ParseFloat(nums[0]), ParseFloat(nums[1])),
                        new Vector2(ParseFloat(nums[2]), ParseFloat(nums[3]))
                    );
                }

                return new BasicEaseDirective(
                    (EaseFunction)Enum.Parse(typeof(EaseFunction), tokens[0]),
                    (EaseMode)Enum.Parse(typeof(EaseMode), tokens[1])
                );
            }

            throw new ArgumentException("The specified string is not in a valid Easing format");
        }

        private static int ParseInt(string number)
        {
            return int.Parse(number, CultureInfo.InvariantCulture);
        }

        private static float ParseFloat(string number)
        {
            return float.Parse(number, CultureInfo.InvariantCulture);
        }

        private static BeatPosition ParseTime(string number)
        {
            int slashPos = number.IndexOf('/');

            if (slashPos >= 0)
            {
                int bPos = number.IndexOf('b');
                int wholePart = ParseInt(number[..bPos]);
                int sign = number[0] == '-' ? -1 : 1;

                return new BeatPosition(
                    wholePart,
                    ParseInt(number[(bPos + 1)..slashPos]) * sign,
                    ParseInt(number[(slashPos + 1)..])
                );
            }

            return (BeatPosition)ParseFloat(number.Replace('b', '.'));
        }

        private static Vector3 ParseVector(string str)
        {
            string[] tokens = str.Split(' ');

            return new Vector3(ParseFloat(tokens[0]), ParseFloat(tokens[1]), ParseFloat(tokens[2]));
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