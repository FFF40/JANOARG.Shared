using System;
using System.Globalization;
using JANOARG.Shared.Data.ChartInfo;
using UnityEngine;

namespace JANOARG.Shared.Data.Files
{
    public class JACEncoder
    {
        public const int FORMAT_VERSION = 1;
        public const int INDENT_SIZE    = 2;

        public static string Encode(Chart chart)
        {
            string EncodeAllLaneGroup()
            {
                string laneGroupStr = string.Empty;
                
                foreach (LaneGroup group in chart.Groups) 
                    laneGroupStr += EncodeLaneGroup(group);

                return laneGroupStr;
            }
            
            string EncodeAllLaneStyle()
            {
                string laneStyleStr = string.Empty;
                
                foreach (LaneStyle style in chart.Palette.LaneStyles) 
                    laneStyleStr += EncodeLaneStyle(style);

                return laneStyleStr;
            }
            
            string EncodeAllHitStyle()
            {
                string hitStyleStr = string.Empty;
                
                foreach (HitStyle style in chart.Palette.HitStyles) 
                    hitStyleStr += EncodeHitStyle(style);

                return hitStyleStr;
            }
            
            string EncodeAllLane()
            {
                string laneStr = string.Empty;
                
                foreach (Lane lane in chart.Lanes) 
                    laneStr += EncodeLane(lane);

                return laneStr;
            }

            string InsertAltCharter() => 
                !string.IsNullOrWhiteSpace(chart.AltCharterName) ? $"\nAlt Charter:  {chart.AltCharterName}" : "";
            
            // Raw interpolation only in C#11 (fuck you Unity, I MEAN IT.)
            var str = $@"JANOARG Chart Format
github.com/FFF40/JANOARG

[VERSION]
{FORMAT_VERSION}

[METADATA]
Index: {chart.DifficultyIndex.ToString(CultureInfo.InvariantCulture)}
Name: {chart.DifficultyName}
Charter: {chart.CharterName}{InsertAltCharter()}
Level: {chart.DifficultyLevel}
Constant: {chart.ChartConstant.ToString(CultureInfo.InvariantCulture)}

[CAMERA]
Pivot: {EncodeVector(chart.Camera.CameraPivot)}
Rotation: {EncodeVector(chart.Camera.CameraRotation)}
Distance: {chart.Camera.PivotDistance.ToString(CultureInfo.InvariantCulture)}{EncodeStoryboard(chart.Camera)}

[GROUPS]
{EncodeAllLaneGroup()}

[PALLETE]
Background: {EncodeColor(chart.Palette.BackgroundColor)}
Interface: {EncodeColor(chart.Palette.InterfaceColor)}{EncodeStoryboard(chart.Palette)}
{EncodeAllLaneStyle()}
{EncodeAllHitStyle()}

[OBJECTS]
{EncodeAllLane()}";

            return str;
        }

        public static string EncodeStoryboard(Storyboardable storyboard, int depth = 0)
        {
            return EncodeStoryboard(storyboard.Storyboard, depth);
        }

        public static string EncodeStoryboard(Storyboard storyboard, int depth = 0)
        {
            var str = "";
            string indent = new(' ', depth);
            
            // Shorten the line 
            CultureInfo ic = CultureInfo.InvariantCulture;
            
            string PerStoryboardEncode(Timestamp timestamp) => 
                $"\n{indent}$ {timestamp.ID} {timestamp.Offset.ToString(ic)} {timestamp.Duration.ToString(ic)} {timestamp.Target.ToString(ic)} {FetchTimestamFrom(timestamp)} {EncodeEase(timestamp.Easing)}";
            string FetchTimestamFrom(Timestamp timestamp) => 
                float.IsFinite(timestamp.From) ? timestamp.From.ToString(ic) : "_";

            foreach (Timestamp timestamp in storyboard.Timestamps)
                       str += PerStoryboardEncode(timestamp);

            return str;
        }

        public static string EncodeLaneGroup(LaneGroup group, int depth = 0)
        {
            string indent = new(' ', depth);
            string indent2 = new(' ', depth + INDENT_SIZE);

            string parentGroupEncode =
                !string.IsNullOrEmpty(group.Group)
                    ? $"\n{indent2}Group: {group.Group}"
                    : "";

            string str = $"{indent}+ Group {EncodeVector(group.Position)} {EncodeVector(group.Rotation)}\n{indent2}Name: {group.Name}{parentGroupEncode}{EncodeStoryboard(group, depth + INDENT_SIZE)}\n";

            return str;
        }

        public static string EncodeLaneStyle(LaneStyle style, int depth = 0)
        {
            string indent = new(' ', depth);
            string indent2 = new(' ', depth + INDENT_SIZE);
            
            string lanePath = style.LaneMaterial;
            string judgePath = style.JudgeMaterial;
            
            string laneStyleName =
                !string.IsNullOrEmpty(style.Name)
                    ? $"\n{indent2}Name: {style.Name}"
                    : string.Empty;
            
            string laneStyleMaterial =
                !string.IsNullOrEmpty(lanePath) && lanePath != "Default"
                    ? $"\n{indent2}Lane Material: {lanePath}"
                    : string.Empty;
            
            string laneStyleColorTarget =
                !string.IsNullOrEmpty(style.LaneColorTarget) && style.LaneColorTarget != "_Color"
                    ? $"\n{indent2}Lane Target: {style.LaneColorTarget}"
                    : string.Empty;
            
            string laneStyleJudgeMaterial =
                !string.IsNullOrEmpty(judgePath) && judgePath != "Default"
                    ? $"\n{indent2}Judge Material: {judgePath}"
                    : string.Empty;
            
            string laneStyleJudgeColorTarget =
                !string.IsNullOrEmpty(style.JudgeColorTarget) && style.JudgeColorTarget != "_Color"
                    ? $"\n{indent2}Judge Target: {style.JudgeColorTarget}"
                    : string.Empty;

            string laneStyleDatas = $"{laneStyleMaterial}{laneStyleColorTarget}{laneStyleJudgeMaterial}{laneStyleJudgeColorTarget}";
            
            string str = 
                $"\n{indent}+ LaneStyle {EncodeColor(style.LaneColor)} {EncodeColor(style.JudgeColor)}{laneStyleName}{laneStyleDatas}{EncodeStoryboard(style, depth + INDENT_SIZE)}";
            
            return str;
        }

        public static string EncodeHitStyle(HitStyle style, int depth = 0)
        {
            string indent = new(' ', depth);
            string indent2 = new(' ', depth + INDENT_SIZE);
            
            string mainPath = style.MainMaterial;
            string holdPath = style.HoldTailMaterial;
            
            string hitStyleName =
                !string.IsNullOrEmpty(style.Name)
                    ? $"\n{indent2}Name: {style.Name}"
                    : string.Empty;
            
            string hitMaterial =
                !string.IsNullOrEmpty(mainPath) && mainPath != "Default"
                    ? $"\n{indent2}Main Material: {mainPath}"
                    : string.Empty;
            
            string hitTarget =
                !string.IsNullOrEmpty(style.MainColorTarget) && style.MainColorTarget != "_Color"
                    ? $"\n{indent2}Main Target: {style.MainColorTarget}"
                    : string.Empty;
            
            string holdMaterial =
                !string.IsNullOrEmpty(holdPath) && holdPath != "Default"
                    ? $"\n{indent2}Hold Tail Material: {holdPath}"
                    : string.Empty;
            
            string holdTarget =
                !string.IsNullOrEmpty(style.HoldTailColorTarget) && style.HoldTailColorTarget != "_Color"
                    ? $"\n{indent2}Hold Tail Target: {style.HoldTailColorTarget}"
                    : string.Empty;
            
            string hitStyleDatas = $"{hitMaterial}{hitTarget}{holdMaterial}{holdTarget}";

            string hitStyleColors = $"{EncodeColor(style.HoldTailColor)} {EncodeColor(style.NormalColor)} {EncodeColor(style.CatchColor)}";
            
            string str = $"\n{indent}+ HitStyle {hitStyleColors}{hitStyleName}{hitStyleDatas}{EncodeStoryboard(style, depth + INDENT_SIZE)}";
            
            return str;
        }

        public static string EncodeLane(Lane lane, int depth = 0)
        {
            string indent = new(' ', depth);
            string indent2 = new(' ', depth + INDENT_SIZE);
            
            string laneName  = !string.IsNullOrEmpty(lane.Name)?  $"\n{indent2}Name: {lane.Name}" : string.Empty;
            string laneGroup = !string.IsNullOrEmpty(lane.Group)? $"\n{indent2}Group: {lane.Group}" : string.Empty;

            string laneInfo = $"{laneName}{laneGroup}";
            string laneStyleIndex = lane.StyleIndex.ToString(CultureInfo.InvariantCulture);

            string EncodeAllLaneStep()
            {
                string laneStepStr = String.Empty;
                
                foreach (LaneStep step in lane.LaneSteps) 
                    laneStepStr += EncodeLaneStep(step, depth + INDENT_SIZE);
                
                return laneStepStr;
            }
            
            string EncodeAllHitObject()
            {
                string hitObjectStr = String.Empty;
                
                foreach (HitObject hit in lane.Objects) 
                    hitObjectStr += EncodeHitObject(hit, depth + INDENT_SIZE);
                
                return hitObjectStr;
            }
            
            string laneData = $"{EncodeAllLaneStep()}{EncodeAllHitObject()}";
            
            string str  = $"\n{indent}+ Lane {EncodeVector(lane.Position)} {EncodeVector(lane.Rotation)} {laneStyleIndex}{laneInfo}{EncodeStoryboard(lane, depth + INDENT_SIZE)}{laneData}";
            
            return str;
        }

        public static string EncodeLaneStep(LaneStep step, int depth = 0)
        {
            string indent = new(' ', depth);
            string indent2 = new(' ', depth + INDENT_SIZE);

            string offset = step.Offset.ToString(CultureInfo.InvariantCulture);

            string laneStepStartPointData = $"{EncodeVector(step.StartPointPosition)} {EncodeEase(step.StartEaseX)} {EncodeEase(step.StartEaseY)}",
                   laneStepEndPointData   = $"{EncodeVector(step.EndPointPosition)} {EncodeEase(step.EndEaseX)} {EncodeEase(step.EndEaseY)}";
            
            string speed = step.Speed.ToString(CultureInfo.InvariantCulture);
            
            string str = $"\n{indent}+ LaneStep {offset} {laneStepStartPointData} {laneStepEndPointData} {speed}{EncodeStoryboard(step, depth + INDENT_SIZE)}";

            return str;
        }

        public static string EncodeHitObject(HitObject hit, int depth = 0)
        {
            string indent = new(' ', depth);
            string indent2 = new(' ', depth + INDENT_SIZE);

            string hitObjectValues =
                $"{hit.Offset.ToString(CultureInfo.InvariantCulture)} {hit.Position.ToString(CultureInfo.InvariantCulture)} {hit.Length.ToString(CultureInfo.InvariantCulture)} {hit.HoldLength.ToString(CultureInfo.InvariantCulture)}";
            
            string flickValue = float.IsFinite(hit.FlickDirection)
                ? hit.FlickDirection.ToString(CultureInfo.InvariantCulture)
                : string.Empty;
            
            string flickFlag =
                hit.Flickable
                    ? "F" + flickValue
                    : "N";
            
            string styleIndex = hit.StyleIndex.ToString(CultureInfo.InvariantCulture);
            
            string str = $"\n{indent}+ Hit {hit.Type} {hitObjectValues} {flickFlag} {styleIndex} {EncodeStoryboard(hit, depth + INDENT_SIZE)}";
            return str;
        }

        public static string EncodeEase(IEaseDirective ease)
        {
            if (ease is BasicEaseDirective basicEaseDirective)
            {
                if (basicEaseDirective.Function == EaseFunction.Linear) 
                    return "Linear";

                return $"{basicEaseDirective.Function}/{basicEaseDirective.Mode}";
            }

            if (ease is CubicBezierEaseDirective cubicBezierEaseDirective)
            {
                string firstPointBezier  = $"{cubicBezierEaseDirective.Point1.x.ToString(CultureInfo.InvariantCulture)};{cubicBezierEaseDirective.Point1.y.ToString(CultureInfo.InvariantCulture)}";
                string secondPointBezier = $"{cubicBezierEaseDirective.Point2.x.ToString(CultureInfo.InvariantCulture)};{cubicBezierEaseDirective.Point2.y.ToString(CultureInfo.InvariantCulture)}";

                return $"Bezier/{firstPointBezier};{secondPointBezier}";
            }

            throw new Exception("Unknown ease directive " + ease.GetType());
        }

        public static string EncodeVector(Vector2 vec)
        {
            return vec.x.ToString(CultureInfo.InvariantCulture) + 
                   " " + 
                   vec.y.ToString(CultureInfo.InvariantCulture);
        }

        public static string EncodeVector(Vector3 vec)
        {
            return vec.x.ToString(CultureInfo.InvariantCulture) + 
                   " " + 
                   vec.y.ToString(CultureInfo.InvariantCulture) + " " + 
                   vec.z.ToString(CultureInfo.InvariantCulture);
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