using System;
using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;
using Random = System.Random;

namespace JANOARG.Shared.Data.ChartInfo
{
    internal static class MaterialQueueUtility
    {
        private const int TransparentQueue = (int)RenderQueue.Transparent;

        public static void NormalizeTransparentQueue(Material material)
        {
            if (!material)
                return;

            if (material.GetTag("Queue", false, string.Empty) == "Transparent" &&
                material.renderQueue < TransparentQueue)
            {
                material.renderQueue = TransparentQueue;
            }
        }
    }

    public class ChartManager
    {
        public PlayableSong Song;
        public Chart        CurrentChart;

        public Dictionary<ulong, LaneGroupManager> Groups         = new();
        public List<LaneManager>                    Lanes          = new();
        public HitMeshManager                       HitMeshManager = new();
        public PalleteManager                       PalleteManager = new();
        public CameraController                     Camera;

        public float CurrentSpeed;
        public float CurrentTime;
        public int[] HitObjectsRemaining = new int[2];
        public int   FlicksRemaining;

        private readonly List<ulong> _GroupKeyScratch = new();

        // Matches the granularity the Client instruments LanePlayer/PlayerScreen at, so the
        // two profiles can be read against each other.
        static readonly ProfilerMarker sr_Groups    = new("ChartManager: Groups");
        static readonly ProfilerMarker sr_GroupPos  = new("ChartManager: Group Positions");
        static readonly ProfilerMarker sr_Lanes     = new("ChartManager: Lanes");

        public int ActiveLaneCount;
        public int ActiveHitCount;
        public int ActiveLaneVerts;
        public int ActiveLaneTris;

        public ulong HighestUuid;

        public ChartManager(PlayableSong song, Chart chart, float speed, float time, float pos)
        {
            Song = song;
            PalleteManager = new PalleteManager(this);
            CurrentChart = chart;
            CurrentSpeed = speed;
            HighestUuid = chart.HighestUuid > 0 ? chart.HighestUuid : SeedUuid();
            Update(time, pos);

            ulong SeedUuid()
            {
                ulong GenerateRandomSalt()
                {
                    byte[] bytes = new byte[8];
                    System.Security.Cryptography.RandomNumberGenerator.Fill(bytes);
                    return BitConverter.ToUInt64(bytes, 0);
                }
                // Combine metadata fields for unique seed (fancy impure prng)
                string seedString = $"{chart.DifficultyName}{chart.CharterName}{chart.DifficultyLevel}{chart.ChartConstant}{GenerateRandomSalt()}";
    
                // Hash to ulong
                using (var sha256 = System.Security.Cryptography.SHA256.Create())
                {
                    byte[] hash = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(seedString));
                    return BitConverter.ToUInt32(hash, 0);
                }
            }
        }

        /// <param name="activeMask">
        /// Optional per-lane flags: false skips that lane's update entirely. Passing null
        /// updates every lane, which is the original behaviour.
        /// </param>
        public void Update(float time, float pos, IReadOnlyList<bool> activeMask = null)
        {
            PalleteManager.Update(CurrentChart.Palette, pos);
            Camera = (CameraController)CurrentChart.Camera.GetStoryboardableObject(pos);

            // Reset in place — callers compare against the previous frame's values by copying
            // the reference out (PlayerView), so the array identity staying stable is fine.
            HitObjectsRemaining[0] = HitObjectsRemaining[1] = 0;
            FlicksRemaining = 0;
            ActiveLaneCount = ActiveHitCount = ActiveLaneVerts = ActiveLaneTris = 0;

            if (CurrentChart.HighestUuid != HighestUuid)
                CurrentChart.HighestUuid = HighestUuid;

            sr_Groups.Begin();

            for (var a = 0; a < CurrentChart.Groups.Count; a++)
            {
                LaneGroup source = CurrentChart.Groups[a];

                // Ensure the source has a UUID assigned
                if (source.UUID == 0)
                    source.UUID = HighestUuid++;

                // UUID isn't storyboarded, so the key is available without evaluating first.
                if (Groups.TryGetValue(source.UUID, out LaneGroupManager groupManager))
                {
                    if (SourcesChanged || groupManager.CurrentGroup == null)
                        groupManager.CurrentGroup = (LaneGroup)source.GetStoryboardableObject(pos);
                    else if (source.Storyboard.Timestamps.Count > 0)
                        source.UpdateStoryboardObject(pos, groupManager.CurrentGroup);

                    groupManager.Update(groupManager.CurrentGroup, pos, this);
                }
                else
                {
                    var group = (LaneGroup)source.GetStoryboardableObject(pos);

                    Groups.Add(group.UUID, groupManager = new LaneGroupManager(group, pos, this));
                }

                groupManager.IsTouched = true;
            }

            sr_Groups.End();
            sr_GroupPos.Begin();

            // Snapshot the keys (the loop removes from Groups) into a reusable list rather than
            // cloning the whole dictionary every frame.
            _GroupKeyScratch.Clear();

            foreach (ulong key in Groups.Keys)
                _GroupKeyScratch.Add(key);

            foreach (ulong key in _GroupKeyScratch)
            {
                LaneGroupManager group = Groups[key];

                if (group.IsDirty)
                    group.UpdatePosition(this);
                else if (!group.IsTouched)
                    Groups.Remove(key);
                else
                    group.IsTouched = false;
            }

            sr_GroupPos.End();
            sr_Lanes.Begin();

            for (var a = 0; a < CurrentChart.Lanes.Count; a++)
            {
                // Out-of-range is treated as active so a stale mask can never blank the view.
                bool active = activeMask == null || a >= activeMask.Count || activeMask[a];

                if (Lanes.Count <= a) Lanes.Add(new LaneManager());

                LaneManager manager = Lanes[a];

                if (!active)
                {
                    // Its cached step distances and mesh go stale while skipped, so require a
                    // rebuild rather than a diff when it comes back.
                    if (manager.IsActive) manager.NeedsFullRebuild = true;

                    manager.IsActive = false;
                    continue;
                }

                var original = CurrentChart.Lanes[a];

                // Deliberately inside the active branch: evaluating the storyboard at all is a
                // real part of the saving, not just the mesh rebuild below it.
                // NeedsFullRebuild is read here because manager.Update clears it.
                if (SourcesChanged || manager.NeedsFullRebuild || manager.Current == null)
                    manager.Current = (Lane)original.GetStoryboardableObject(pos);
                else if (original.Storyboard.Timestamps.Count > 0)
                    original.UpdateStoryboardObject(pos, manager.Current);

                manager.IsActive = true;
                manager.Update(original, manager.Current, time, pos, this);
            }

            while (Lanes.Count > CurrentChart.Lanes.Count)
            {
                Lanes[CurrentChart.Lanes.Count].Dispose();

                Lanes.RemoveAt(CurrentChart.Lanes.Count);
            }

            sr_Lanes.End();

            SourcesChanged = false;
        }

        /// <summary>
        /// Storyboarded values are written into instances that persist across frames, so the
        /// non-storyboarded fields alongside them are only as current as the last clone.
        /// Callers must raise this whenever the chart data changes.
        /// </summary>
        public void MarkSourcesChanged() => SourcesChanged = true;

        public bool SourcesChanged { get; private set; } = true;

        public void Dispose()
        {
            foreach (LaneManager lane in Lanes)
                lane.Dispose();
        }
    }


    public class PalleteManager
    {
        public  Palette      CurrentPallete;
        private ChartManager chartManager;

        public List<LaneStyleManager> LaneStyles = new();
        public List<HitStyleManager>  HitStyles  = new();

        public PalleteManager (ChartManager main) => chartManager = main;
        
        public void Update(Palette pallete, float pos)
        {
            CurrentPallete = pallete = (Palette)pallete.GetStoryboardableObject(pos);

            for (var a = 0; a < pallete.LaneStyles.Count; a++)
            {
                var style = (LaneStyle)pallete.LaneStyles[a]
                    .GetStoryboardableObject(pos);

                if (LaneStyles.Count <= a)
                    LaneStyles.Add(new LaneStyleManager(style, chartManager));
                else
                    LaneStyles[a]
                        .Update(style);
            }

            while (LaneStyles.Count > pallete.LaneStyles.Count)
            {
                LaneStyles[pallete.LaneStyles.Count]
                    .Dispose();

                LaneStyles.RemoveAt(pallete.LaneStyles.Count);
            }

            for (var a = 0; a < pallete.HitStyles.Count; a++)
            {
                var style = (HitStyle)pallete.HitStyles[a]
                    .GetStoryboardableObject(pos);

                if (HitStyles.Count <= a) HitStyles.Add(new HitStyleManager(style, chartManager));
                else
                    HitStyles[a]
                        .Update(style);
            }

            while (HitStyles.Count > pallete.HitStyles.Count)
            {
                HitStyles[pallete.HitStyles.Count]
                    .Dispose();

                HitStyles.RemoveAt(pallete.HitStyles.Count);
            }
        }
    }


    public class LaneStyleManager
    {
        public ulong Uuid;
        public Material BaseLaneMaterial;
        public Material LaneMaterial;

        public Material BaseJudgeMaterial;
        public Material JudgeMaterial;

        public LaneStyleManager(LaneStyle style, ChartManager main)
        {
            Uuid = style.UUID > 0 ? style.UUID : style.UUID = main.HighestUuid++;
            Update(style);
        }

        public void Update(LaneStyle style)
        {
            // Debug.Log(style.LaneMaterial);

            if (BaseLaneMaterial?.name != style.LaneMaterial) LaneMaterial = new Material(BaseLaneMaterial = InternalChartTool.LoadStyleMaterial("Lane", style.LaneMaterial));

            if (BaseJudgeMaterial?.name != style.JudgeMaterial) JudgeMaterial = new Material(BaseJudgeMaterial = InternalChartTool.LoadStyleMaterial("Judge", style.JudgeMaterial));

            MaterialQueueUtility.NormalizeTransparentQueue(LaneMaterial);
            MaterialQueueUtility.NormalizeTransparentQueue(JudgeMaterial);

            if (LaneMaterial) LaneMaterial.SetColor(style.LaneColorTarget, style.LaneColor);
            if (JudgeMaterial) JudgeMaterial.SetColor(style.JudgeColorTarget, style.JudgeColor);
        }

        public void Dispose()
        {
            Object.DestroyImmediate(LaneMaterial);
            Object.DestroyImmediate(JudgeMaterial);
        }
    }


    public class HitStyleManager
    {
        public ulong    Uuid;
        public Material BaseMainMaterial;
        public Material NormalMaterial;
        public Material CatchMaterial;
        
        public Material BaseHighlightMaterial;
        public Material NormalHighlightMaterial;
        public Material NormalHighlightGlowMaterial;
        public Material CatchHighlightMaterial;
        public Material CatchHighlightGlowMaterial;

        public Material BaseHoldTailMaterial;
        public Material HoldTailMaterial;

        public HitStyleManager(HitStyle style, ChartManager main)
        {
            Uuid = style.UUID > 0 ? style.UUID : style.UUID = main.HighestUuid++;
            Update(style);
        }

        public void Update(HitStyle style)
        {
            if (!BaseMainMaterial || BaseMainMaterial.name != style.MainMaterial)
            {
                NormalMaterial = new Material(BaseMainMaterial = InternalChartTool.LoadStyleMaterial("Hit", style.MainMaterial));
                CatchMaterial = new Material(BaseMainMaterial);
            }

            if (!BaseHighlightMaterial || BaseHighlightMaterial.name != style.MainMaterial)
            {
                NormalHighlightMaterial = new Material(BaseHighlightMaterial = InternalChartTool.LoadStyleMaterial("Highlight", style.MainMaterial));
                NormalHighlightGlowMaterial = new Material(BaseHighlightMaterial);
                CatchHighlightMaterial = new Material(BaseHighlightMaterial);
                CatchHighlightGlowMaterial = new Material(BaseHighlightMaterial);
            }

            if (BaseHoldTailMaterial?.name != style.HoldTailMaterial) 
                HoldTailMaterial = new Material(BaseHoldTailMaterial = InternalChartTool.LoadStyleMaterial("Hold", style.HoldTailMaterial));

            MaterialQueueUtility.NormalizeTransparentQueue(NormalMaterial);
            MaterialQueueUtility.NormalizeTransparentQueue(CatchMaterial);
            MaterialQueueUtility.NormalizeTransparentQueue(NormalHighlightMaterial);
            MaterialQueueUtility.NormalizeTransparentQueue(NormalHighlightGlowMaterial);
            MaterialQueueUtility.NormalizeTransparentQueue(CatchHighlightMaterial);
            MaterialQueueUtility.NormalizeTransparentQueue(CatchHighlightGlowMaterial);
            MaterialQueueUtility.NormalizeTransparentQueue(HoldTailMaterial);

            if (NormalMaterial)
                NormalMaterial.SetColor(style.MainColorTarget, style.NormalColor);
            
            if (CatchMaterial) 
                CatchMaterial.SetColor(style.MainColorTarget, style.CatchColor);
            
            if (HoldTailMaterial) 
                HoldTailMaterial.SetColor(style.HoldTailColorTarget, style.HoldTailColor);

            if (NormalHighlightMaterial && NormalHighlightGlowMaterial)
            {
                (Color highlight, Color glow) = InternalChartTool.CalculateSimultaneousColors(style.NormalColor);
                NormalHighlightMaterial.SetColor(style.MainColorTarget, highlight);
                NormalHighlightGlowMaterial.SetColor(style.MainColorTarget, glow);
            }

            if (CatchHighlightMaterial && CatchHighlightGlowMaterial)
            {
                (Color highlight, Color glow) = InternalChartTool.CalculateSimultaneousColors(style.CatchColor);
                CatchHighlightMaterial.SetColor(style.MainColorTarget, highlight);
                CatchHighlightGlowMaterial.SetColor(style.MainColorTarget, glow);
            }
        }

        public void Dispose()
        {
            Object.DestroyImmediate(NormalMaterial);
            Object.DestroyImmediate(CatchMaterial);
            Object.DestroyImmediate(HoldTailMaterial);
        }
    }


    public class LaneGroupManager
    {
        public ulong      Uuid;
        public LaneGroup  CurrentGroup;
        public Vector3    FinalPosition;
        public Quaternion FinalRotation;
        public bool       IsDirty;
        public bool       IsTouched;

        public LaneGroupManager(LaneGroup init, float pos, ChartManager main)
        {
            Uuid = init.UUID > 0 ? init.UUID : init.UUID = main.HighestUuid++;
            Update(init, pos, main);
        }

        public void Update(LaneGroup data, float pos, ChartManager main)
        {
            CurrentGroup = data;
            IsDirty = true;
        }

        public void Get(ref Vector3 pos, ref Quaternion rot)
        {
            pos = FinalRotation * pos + FinalPosition;
            rot = FinalRotation * rot;
        }

        public void UpdatePosition(ChartManager main, ulong originalUuid = 0)
        {
            FinalPosition = CurrentGroup.Position;
            FinalRotation = Quaternion.Euler(CurrentGroup.Rotation);
            originalUuid = originalUuid != 0 ? originalUuid : CurrentGroup.UUID;

            if (CurrentGroup.GroupUuid != 0 && main.Groups.ContainsKey(CurrentGroup.GroupUuid))
            {
                LaneGroupManager group = main.Groups[CurrentGroup.GroupUuid];

                if (originalUuid == group.CurrentGroup.UUID)
                {
                    Debug.LogError("Cyclical Lane group reference detected: " + CurrentGroup.Name);
                }
                else
                {
                    if (group.IsDirty) group.UpdatePosition(main, originalUuid);
                    FinalPosition = group.FinalRotation * FinalPosition + group.FinalPosition;
                    FinalRotation = group.FinalRotation * FinalRotation;
                }
            }

            IsDirty = false;
        }
    }


    public class LaneManager
    {
        public ulong                  Uuid;
        public Lane                   Original;
        public Lane                   Current;
        public List<LaneStepManager>  Steps       = new();
        public List<HitObjectManager> Objects     = new();
        public Mesh                   CurrentMesh = NewDynamicMesh();

        public float CurrentSpeed;
        public float CurrentDistance;

        public Vector3 StartPosLocal;
        public Vector3 EndPosLocal;

        public Vector3 StartPos;
        public Vector3 EndPos;

        public Vector3    FinalPosition;
        public Quaternion FinalRotation;

        private float _LastStepCount;

        // Vertex count the mesh currently holds, so the clear can be limited to a resize.
        private int _LastVertCount;

        // State for the static-lane skip: which step pair the playhead sat in, how far the fog
        // trim reached, and whether there is a previous build to reuse at all.
        private int  _LastSegment = -1;
        private int  _LastBuiltStep = -1;
        private bool _HasBuiltMesh;

        /// <summary>
        /// How far past the current position geometry is still worth building. Linear fog in
        /// the chart scene is opaque at 200 units; this leaves margin. HitObjectManager uses
        /// the same budget to decide whether a hit object is in range.
        /// </summary>
        public const float VisibleDistance = 250f;

        static readonly ProfilerMarker sr_Steps      = new("Lane Update: Step Loop");
        static readonly ProfilerMarker sr_Verts      = new("Lane Update: Vertex Build");
        static readonly ProfilerMarker sr_MeshUpload = new("Lane Update: Mesh Upload");
        static readonly ProfilerMarker sr_HitObjects = new("Lane Update: Hit Objects");

        // Reused across frames so a per-frame mesh rebuild allocates nothing. Both grow to a
        // high-water mark and are only ever read up to the current frame's vertex count, so
        // the tail beyond it is stale by design — never use .Length in place of that count.
        private Vector3[] _Verts = Array.Empty<Vector3>();
        private Vector2[] _Uvs   = Array.Empty<Vector2>();

        // Mirrors what RemakeMesh last wrote, so the unchanged-step-count path can re-set the
        // triangles without reading Mesh.triangles back (which allocates a fresh copy).
        // A List rather than an array because SetTriangles reads its Count, which lets the
        // capacity persist across a changing triangle count instead of reallocating.
        private readonly List<int> _Tris = new();

        // GetLanePosition runs once per in-range hit object per frame and its result is read
        // immediately, never retained, so one instance is refilled rather than allocated.
        private readonly LanePosition _Position = new();

        // Scratch for GetPartOfLane, which runs once per in-range hold note per frame.
        private readonly List<Vector3> _PartVerts = new();
        private readonly List<Vector2> _PartUvs   = new();
        private readonly List<int>     _PartTris  = new();

        /// <summary>True when this lane was updated on the last pass; false when culled.</summary>
        public bool IsActive = true;

        /// <summary>
        /// Set when the lane is skipped, so the next update rebuilds distances and mesh from
        /// scratch instead of diffing against state that stopped tracking time.
        /// </summary>
        public bool NeedsFullRebuild;

        /// <summary>
        /// Lane and hold meshes are rewritten every frame. Marking them dynamic keeps Unity
        /// from recreating the GPU buffers on each write, which forces the main thread to
        /// sync against the render thread during render queue extraction.
        /// </summary>
        static Mesh NewDynamicMesh()
        {
            var mesh = new Mesh();

            mesh.MarkDynamic();

            return mesh;
        }

        /// <summary>Creates an un-updated lane, for a slot that starts out culled.</summary>
        public LaneManager() { }

        public LaneManager(Lane original, Lane current, float time, float pos, ChartManager main)
        {
            Uuid = original.UUID > 0 ? original.UUID : original.UUID = main.HighestUuid++;
            Update(original, current, time, pos, main);
        }

        public void Update(Lane original, Lane current, float time, float pos, ChartManager main)
        {
            Original = original;
            Current = current;

            if (CurrentMesh == null)
                CurrentMesh = NewDynamicMesh();

            var stepCount = 0;
            bool force = !Mathf.Approximately(main.CurrentSpeed, CurrentSpeed);

            // A culled lane's update never runs, so it can miss the frame on which
            // main.SourcesChanged was raised. NeedsFullRebuild covers exactly that gap.
            bool resync = main.SourcesChanged || NeedsFullRebuild;

            if (NeedsFullRebuild)
            {
                // -1 can't match any real step count, so the mesh takes the RemakeMesh path.
                _LastStepCount = -1;
                NeedsFullRebuild = false;
                force = true;
            }

            float offset = float.NaN;
            CurrentSpeed = main.CurrentSpeed;

            sr_Steps.Begin();

            for (var a = 0; a < Current.LaneSteps.Count; a++)
            {
                if (Steps.Count <= a)
                    Steps.Add(new LaneStepManager());

                LaneStepManager stepManager = Steps[a];
                LaneStep source = Current.LaneSteps[a];

                // Captured before the update below overwrites CurrentStep in place.
                bool hasPrev = stepManager.CurrentStep != null;
                BeatPosition prevOffset = hasPrev ? stepManager.CurrentStep.Offset : default;
                float prevSpeed = hasPrev ? stepManager.CurrentStep.Speed : default;

                if (!hasPrev || resync)
                    stepManager.CurrentStep = (LaneStep)source.GetStoryboardableObject(pos);

                // With no timestamps every property evaluates to the source's own value, which
                // the target already holds from its last clone — so the whole evaluation is a
                // read and a write of the same number, six property types deep.
                else if (source.Storyboard.Timestamps.Count > 0)
                    source.UpdateStoryboardObject(pos, stepManager.CurrentStep);

                LaneStep step = stepManager.CurrentStep;

                if (!hasPrev || step.Offset != prevOffset)
                {
                    stepManager.Offset = main.Song.Timing.ToSeconds(step.Offset);
                    force = true;
                }

                if (!hasPrev || step.Speed != prevSpeed)
                    force = true;

                if (force)
                {
                    LaneStepManager prev = a < 1 ? new LaneStepManager() : Steps[a - 1];
                    Steps[a].Distance = prev.Distance + CurrentSpeed * step.Speed * (Steps[a].Offset - prev.Offset);
                }

            }

            while (Steps.Count > Current.LaneSteps.Count)
                Steps.RemoveAt(Current.LaneSteps.Count);

            sr_Steps.End();
            sr_Verts.Begin();

            // The strip runs from the current time to the lane's last step, which for a long
            // lane is mostly geometry sitting past the fog's far end (linear fog is opaque at
            // 200 units) and therefore invisible. Trim a run of steps off the far end.
            //
            // Only a contiguous run, and only from the end: Speed may be negative or
            // storyboarded below zero, so Distance is not guaranteed to rise with step index.
            // A lane that retreats and re-enters view stops the trim at its first visible
            // interval, which both keeps that geometry and avoids tearing a hole in the strip.
            int lastStep = Steps.Count - 1;

            if (Steps.Count >= 2)
            {
                float cutoff = GetLanePosition(time, CurrentSpeed).Offset + VisibleDistance;

                while (lastStep >= 1
                       && Mathf.Min(Steps[lastStep - 1].Distance, Steps[lastStep].Distance) > cutoff)
                    lastStep--;
            }

            // Counted before the skip below is decided, which needs it: the count is part of what
            // makes a previous build reusable.
            for (var a = 0; a <= lastStep; a++)
            {
                LaneStep step = Steps[a].CurrentStep;

                stepCount += float.IsNaN(offset)
                    ? 1 : Mathf.CeilToInt((offset == Steps[a].Offset ? Steps[a].Offset > time ? 1 : 0 : Mathf.Clamp01((time - Steps[a].Offset) / (offset - Steps[a].Offset))) * (step.IsLinear ? 1 : 16));

                offset = Steps[a].Offset;
            }

            // Ported from the Client's static-lane skip (LanePlayer.UpdateMesh). Segments ahead
            // of the playhead already interpolate at progress 0, so they emit their own step's
            // values and don't depend on time — only the segment containing the playhead moves.
            // Freeze that one and the whole strip is unchanged.
            //
            // Zero speed alone isn't enough here, unlike in the Client: it fixes the segment's
            // distance, but its lateral position still lerps with progress. Requiring the two
            // steps to share start and end points makes that lerp constant too.
            int segment = Steps.Count > 1 ? FindStepIndex(time) : 0;

            bool geometryStatic =
                !force
                && _HasBuiltMesh
                && Steps.Count > 2
                && segment >= 1
                && segment == _LastSegment
                && lastStep == _LastBuiltStep
                // Tessellation density tracks the playhead inside the segment - the count reads
                // step offsets rather than distances, so zero speed doesn't hold it still. The
                // mesh on the GPU is only the right one at the count that wrote it.
                && stepCount == _LastStepCount
                && Steps[segment - 1].CurrentStep.Speed == 0f
                && Steps[segment].CurrentStep.Speed == 0f
                && Steps[segment - 1].CurrentStep.StartPointPosition == Steps[segment].CurrentStep.StartPointPosition
                && Steps[segment - 1].CurrentStep.EndPointPosition   == Steps[segment].CurrentStep.EndPointPosition;

            _LastSegment   = segment;
            _LastBuiltStep = lastStep;

            var index = 0;
            int vertCount = stepCount * 2;

            if (_Verts.Length < vertCount)
            {
                // Doubling, not exact fit: a lane scrolling into view gains steps every frame,
                // so an exact fit reallocates both arrays on every one of those frames.
                int capacity = Mathf.Max(vertCount, _Verts.Length * 2);

                _Verts = new Vector3[capacity];
                _Uvs   = new Vector2[capacity];
            }

            Vector3[] verts = _Verts;
            Vector2[] uvs = _Uvs;

            LaneStepManager next = null;

            // Skipped entirely when static: the pooled arrays still hold last frame's vertices,
            // stepCount is unchanged under the same conditions, and CurrentDistance is constant
            // — so it must not be reset to NaN here or the fallback below would overwrite it
            // with the before-the-lane-starts formula.
            if (!geometryStatic)
                CurrentDistance = float.NaN;

            if (!geometryStatic && vertCount > 0)
                for (int a = lastStep; a >= 0; a--)
                {
                    LaneStepManager curr = Steps[a];

                    if (next == null)
                    {
                        verts[index] = (Vector3)curr.CurrentStep.StartPointPosition + Vector3.forward * curr.Distance;
                        verts[index + 1] = (Vector3)curr.CurrentStep.EndPointPosition + Vector3.forward * curr.Distance;

                        // Debug.Log(index + "/" + verts.Length + " " + verts[index] + " " + verts[index + 1]);
                        index += 2;

                        if (index >= vertCount)
                        {
                            CurrentDistance = curr.Distance + curr.CurrentStep.Speed * CurrentSpeed * (time - curr.Offset);

                            break;
                        }
                    }
                    else if (next.CurrentStep.IsLinear)
                    {
                        float offsetLerpProgress = curr.Offset == next.Offset 
                            ? curr.Offset < time 
                                ? 1 : 0 
                            : Mathf.Clamp01((time - curr.Offset) / (next.Offset - curr.Offset));
                      
                        float dist = Mathf.Lerp(curr.Distance, next.Distance, offsetLerpProgress);
                      
                        verts[index] = Vector3.Lerp(curr.CurrentStep.StartPointPosition, next.CurrentStep.StartPointPosition, offsetLerpProgress) + Vector3.forward * dist;
                        verts[index + 1] = Vector3.Lerp(curr.CurrentStep.EndPointPosition, next.CurrentStep.EndPointPosition, offsetLerpProgress) + Vector3.forward * dist;

                        // Debug.Log(index + "/" + verts.Length + " " + verts[index] + " " + verts[index + 1]);
                        index += 2;

                        if (offsetLerpProgress > 0)
                        {
                            CurrentDistance = dist;

                            break;
                        }

                        if (index >= vertCount) break;
                    }
                    else
                    {
                        float p = curr.Offset == next.Offset 
                            ? curr.Offset < time 
                                ? 1 : 0 
                            : Mathf.Clamp01((time - curr.Offset) / (next.Offset - curr.Offset));
                        
                        float dist = 0;

                        for (var i = 15; i >= 0; i--)
                        {
                            float x = Math.Max(i / 16f, p);
                            dist = Mathf.Lerp(curr.Distance, next.Distance, x);

                            verts[index] = new Vector3(Mathf.LerpUnclamped(curr.CurrentStep.StartPointPosition.x, next.CurrentStep.StartPointPosition.x, next.CurrentStep.StartEaseX.Get(x)),
                                Mathf.LerpUnclamped(curr.CurrentStep.StartPointPosition.y, next.CurrentStep.StartPointPosition.y, next.CurrentStep.StartEaseY.Get(x)), dist);

                            verts[index + 1] = new Vector3(Mathf.LerpUnclamped(curr.CurrentStep.EndPointPosition.x, next.CurrentStep.EndPointPosition.x, next.CurrentStep.EndEaseX.Get(x)),
                                Mathf.LerpUnclamped(curr.CurrentStep.EndPointPosition.y, next.CurrentStep.EndPointPosition.y, next.CurrentStep.EndEaseY.Get(x)), dist);

                            index += 2;

                            if (x == p || index >= vertCount) break;
                        }

                        if (p > 0)
                        {
                            CurrentDistance = dist;

                            break;
                        }

                        if (index >= vertCount) break;
                    }

                    next = curr;
                }

            if (float.IsNaN(CurrentDistance) && Steps.Count > 0)
                CurrentDistance = Steps[0].Distance + Steps[0].CurrentStep.Speed * CurrentSpeed * (time - Steps[0].Offset);

            sr_Verts.End();
            sr_MeshUpload.Begin();

            // Fewer than two steps produces no triangles, so the mesh draws nothing. Rewriting
            // it anyway recreates its GPU buffers every frame, and that recreation is what
            // stalls the main thread during render queue extraction — a lane sitting inside
            // its cue window but not yet visible was costing more than a lane being drawn.
            if (stepCount < 2)
            {
                if (stepCount != _LastStepCount)
                {
                    CurrentMesh.Clear();

                    _Tris.Clear();
                    _LastStepCount = stepCount;
                    _LastVertCount = 0;
                }

                // An empty mesh is nothing to reuse, so the skip above must not treat it as a build.
                _HasBuiltMesh = false;
            }
            else if (!geometryStatic)
            {
                for (var a = 0; a < vertCount; a++) uvs[a] = new Vector2(a % 2, verts[a].z);

                // Clearing every frame resets the bounds as a side effect, and that side effect
                // is the only reason it was load-bearing. Unity's automatic recalculation on a
                // vertex write cannot be relied on, and stale bounds get the renderer
                // frustum-culled outright - a lane that silently stops drawing while its
                // GameObject, mesh and material all still look correct. Lane geometry is built
                // in absolute distance space and swept back by the holder, so it moves
                // thousands of units while the vertex count sits still, and bounds left over
                // from an earlier frame leave the frustum almost immediately.
                //
                // RecalculateBounds below does that job directly, for a vertex scan rather than
                // a buffer reallocation. The clear is then only needed when the count changes,
                // so last frame's indices cannot point past the end of the new buffer.
                if (vertCount != _LastVertCount) CurrentMesh.Clear();

                _LastVertCount = vertCount;

                CurrentMesh.SetVertices(verts, 0, vertCount);
                CurrentMesh.SetUVs(0, uvs, 0, vertCount);

                CurrentMesh.RecalculateBounds();

                if (stepCount != _LastStepCount)
                {
                    FillTriangles(_Tris, stepCount);
                    _LastStepCount = stepCount;

                    #if UNITY_EDITOR
                    // Named so the profiler's mesh rows are identifiable instead of <No Name>.
                    // Only on rebuild, so the string never lands in the per-frame path.
                    CurrentMesh.name = string.IsNullOrEmpty(Current.Name)
                        ? $"Lane @{(Steps.Count > 0 ? Steps[0].Offset : 0):0.###}s ({vertCount}v)"
                        : $"Lane {Current.Name} ({vertCount}v)";
                    #endif
                }

                // Left unconditional. Gating it on the step count was tried before the bounds
                // were understood and failed for that reason, so it may well be safe now - but
                // it is worth only 0.37 ms and has not been retested.
                CurrentMesh.SetTriangles(_Tris, 0);

                // Raised only here: this is the one path that leaves geometry on the GPU.
                _HasBuiltMesh = true;
            }

            sr_MeshUpload.End();

            main.ActiveLaneCount++;
            main.ActiveLaneVerts += vertCount;
            main.ActiveLaneTris += _Tris.Count;

            FinalPosition = Current.Position;
            FinalRotation = Quaternion.Euler(Current.Rotation);

            if (Current.GroupUuid != 0 && main.Groups.ContainsKey(Current.GroupUuid))
                main.Groups[Current.GroupUuid]
                    .Get(ref FinalPosition, ref FinalRotation);

            StartPosLocal = StartPos = verts[stepCount * 2 - 2] - Vector3.forward * CurrentDistance;
            StartPos = FinalRotation * StartPos + FinalPosition;
            EndPosLocal = EndPos = verts[stepCount * 2 - 1] - Vector3.forward * CurrentDistance;
            EndPos = FinalRotation * EndPos + FinalPosition;



            sr_HitObjects.Begin();

            for (var a = 0; a < Current.Objects.Count; a++)
            {
                var originalHit = Original.Objects[a];

                if (Objects.Count <= a)
                {
                    var currentHit = (HitObject)Current.Objects[a].GetStoryboardableObject(pos);

                    Objects.Add(new HitObjectManager(originalHit, currentHit, time, this, main));

                    continue;
                }

                HitObjectManager hitManager = Objects[a];

                if (resync || hitManager.Current == null)
                    hitManager.Current = (HitObject)Current.Objects[a].GetStoryboardableObject(pos);
                else if (Current.Objects[a].Storyboard.Timestamps.Count > 0)
                    Current.Objects[a].UpdateStoryboardObject(pos, hitManager.Current);

                hitManager.Update(originalHit, hitManager.Current, time, this, main);
            }

            while (Objects.Count > Current.Objects.Count)
            {
                Objects[Current.Objects.Count].Dispose();
                Objects.RemoveAt(Current.Objects.Count);
            }

            sr_HitObjects.End();
        }

        public Mesh GetPartOfLane(float timeStart, float timeEnd, float xPos, float xLength)
            => GetPartOfLane(timeStart, timeEnd, xPos, xLength, new Mesh());

        /// <summary>
        /// Fills <paramref name="target"/> with the slice of this lane between the given
        /// times. Callers that need the slice every frame should keep one mesh and pass it
        /// back in — creating a Mesh costs native resource registration, which lands in
        /// Camera.Render rather than anywhere the GC profiler would show it.
        /// </summary>
        public Mesh GetPartOfLane(float timeStart, float timeEnd, float xPos, float xLength, Mesh target)
        {
            List<Vector3> verts = _PartVerts;
            List<Vector2> uvs = _PartUvs;

            verts.Clear();
            uvs.Clear();

            for (int a = Steps.Count - 1; a >= 1; a--)
            {
                LaneStepManager next = Steps[a];
                LaneStepManager curr = Steps[a - 1];

                float pStart = curr.Offset == next.Offset ? curr.Offset < timeStart ? 1 : 0 : Mathf.Clamp01((timeStart - curr.Offset) / (next.Offset - curr.Offset));
                float pEnd = curr.Offset == next.Offset ? curr.Offset < timeEnd ? 1 : 0 : Mathf.Clamp01((timeEnd - curr.Offset) / (next.Offset - curr.Offset));

                if (curr.Offset > timeEnd) continue;

                if (verts.Count < 1)
                {
                    if (next.CurrentStep.IsLinear)
                    {
                        float dist = Mathf.Lerp(curr.Distance, next.Distance, pEnd);
                        Vector3 currStart = Vector3.LerpUnclamped(curr.CurrentStep.StartPointPosition, curr.CurrentStep.EndPointPosition, xPos);
                        Vector3 currEnd = Vector3.LerpUnclamped(curr.CurrentStep.StartPointPosition, curr.CurrentStep.EndPointPosition, xPos + xLength);
                        Vector3 nextStart = Vector3.LerpUnclamped(next.CurrentStep.StartPointPosition, next.CurrentStep.EndPointPosition, xPos);
                        Vector3 nextEnd = Vector3.LerpUnclamped(next.CurrentStep.StartPointPosition, next.CurrentStep.EndPointPosition, xPos + xLength);
                        verts.Add(Vector3.Lerp(currStart, nextStart, pEnd) + Vector3.forward * dist);
                        verts.Add(Vector3.Lerp(currEnd, nextEnd, pEnd) + Vector3.forward * dist);

                        // Debug.Log(index + "/" + verts.Length + " " + verts[index] + " " + verts[index + 1]);
                    }
                    else
                    {
                        float dist = 0;
                        Vector3 currStart = Vector3.LerpUnclamped(curr.CurrentStep.StartPointPosition, curr.CurrentStep.EndPointPosition, xPos);
                        Vector3 currEnd = Vector3.LerpUnclamped(curr.CurrentStep.StartPointPosition, curr.CurrentStep.EndPointPosition, xPos + xLength);
                        Vector3 nextStart = Vector3.LerpUnclamped(next.CurrentStep.StartPointPosition, next.CurrentStep.EndPointPosition, xPos);
                        Vector3 nextEnd = Vector3.LerpUnclamped(next.CurrentStep.StartPointPosition, next.CurrentStep.EndPointPosition, xPos + xLength);
                        float x = pEnd;
                        dist = Mathf.Lerp(curr.Distance, next.Distance, x);

                        verts.Add(new Vector3(Mathf.LerpUnclamped(currStart.x, nextStart.x, next.CurrentStep.StartEaseX.Get(x)),
                            Mathf.LerpUnclamped(currStart.y, nextStart.y, next.CurrentStep.StartEaseY.Get(x)), dist));

                        verts.Add(new Vector3(Mathf.LerpUnclamped(currEnd.x, nextEnd.x, next.CurrentStep.EndEaseX.Get(x)),
                            Mathf.LerpUnclamped(currEnd.y, nextEnd.y, next.CurrentStep.EndEaseY.Get(x)), dist));
                    }
                }

                if (next.CurrentStep.IsLinear)
                {
                    float dist = Mathf.Lerp(curr.Distance, next.Distance, pStart);
                    Vector3 currStart = Vector3.LerpUnclamped(curr.CurrentStep.StartPointPosition, curr.CurrentStep.EndPointPosition, xPos);
                    Vector3 currEnd = Vector3.LerpUnclamped(curr.CurrentStep.StartPointPosition, curr.CurrentStep.EndPointPosition, xPos + xLength);
                    Vector3 nextStart = Vector3.LerpUnclamped(next.CurrentStep.StartPointPosition, next.CurrentStep.EndPointPosition, xPos);
                    Vector3 nextEnd = Vector3.LerpUnclamped(next.CurrentStep.StartPointPosition, next.CurrentStep.EndPointPosition, xPos + xLength);
                    verts.Add(Vector3.Lerp(currStart, nextStart, pStart) + Vector3.forward * dist);
                    verts.Add(Vector3.Lerp(currEnd, nextEnd, pStart) + Vector3.forward * dist);

                    // Debug.Log(index + "/" + verts.Length + " " + verts[index] + " " + verts[index + 1]);
                }
                else
                {
                    float dist = 0;
                    Vector3 currStart = Vector3.LerpUnclamped(curr.CurrentStep.StartPointPosition, curr.CurrentStep.EndPointPosition, xPos);
                    Vector3 currEnd = Vector3.LerpUnclamped(curr.CurrentStep.StartPointPosition, curr.CurrentStep.EndPointPosition, xPos + xLength);
                    Vector3 nextStart = Vector3.LerpUnclamped(next.CurrentStep.StartPointPosition, next.CurrentStep.EndPointPosition, xPos);
                    Vector3 nextEnd = Vector3.LerpUnclamped(next.CurrentStep.StartPointPosition, next.CurrentStep.EndPointPosition, xPos + xLength);

                    for (int i = Mathf.FloorToInt(pEnd * 16); i >= 0; i--)
                    {
                        float x = Math.Max(i / 16f, pStart);
                        dist = Mathf.Lerp(curr.Distance, next.Distance, x);

                        verts.Add(new Vector3(Mathf.LerpUnclamped(currStart.x, nextStart.x, next.CurrentStep.StartEaseX.Get(x)),
                            Mathf.LerpUnclamped(currStart.y, nextStart.y, next.CurrentStep.StartEaseY.Get(x)), dist));

                        verts.Add(new Vector3(Mathf.LerpUnclamped(currEnd.x, nextEnd.x, next.CurrentStep.EndEaseX.Get(x)),
                            Mathf.LerpUnclamped(currEnd.y, nextEnd.y, next.CurrentStep.EndEaseY.Get(x)), dist));

                        if (x == pStart) break;
                    }
                }

                if (pStart > 0) break;
            }

            for (var a = 0; a < verts.Count; a++) uvs.Add(new Vector2(a % 2, verts[a].z));

            target.Clear();
            target.SetVertices(verts);
            target.SetUVs(0, uvs);

            FillTriangles(_PartTris, verts.Count / 2);
            target.SetTriangles(_PartTris, 0);

            return target;
        }

        public LanePosition GetLanePosition(float sec, float speed = 1f)
        {
            int stepCount = Steps.Count;
            
            // Early exit for invalid input or single step
            if (stepCount <= 1)
            {
                if (stepCount == 0) return null;
                
                var firstStep = Steps[0];
                var firstLaneStep = Current.LaneSteps[0];
                _Position.StartPosition = firstLaneStep.StartPointPosition;
                _Position.EndPosition = firstLaneStep.EndPointPosition;
                _Position.Offset = firstStep.Distance - firstStep.CurrentStep.Speed * speed * (firstStep.Offset - sec);

                return _Position;
            }
            
            var firstStepOffset = Steps[0].Offset;
            var lastStepOffset = Steps[stepCount - 1].Offset;
            
            // Handle time before first step
            if (sec < firstStepOffset)
            {
                var firstStep = Steps[0];
                var firstLaneStep = Current.LaneSteps[0];
                _Position.StartPosition = firstLaneStep.StartPointPosition;
                _Position.EndPosition = firstLaneStep.EndPointPosition;
                _Position.Offset = firstStep.Distance - firstStep.CurrentStep.Speed * speed * (firstStepOffset - sec);

                return _Position;
            }
            
            // Handle time after last step
            if (sec > lastStepOffset)
            {
                var lastStep = Steps[stepCount - 1];
                var lastLaneStep = Current.LaneSteps[stepCount - 1];
                _Position.StartPosition = lastLaneStep.StartPointPosition;
                _Position.EndPosition = lastLaneStep.EndPointPosition;
                _Position.Offset = lastStep.Distance + lastStep.CurrentStep.Speed * speed * (sec - lastStepOffset);

                return _Position;
            }
            
            // Binary search for the correct step interval
            int stepIndex = FindStepIndex(sec);
            
            var prev = Steps[stepIndex - 1];
            var prevStep = prev.CurrentStep;
            var current = Steps[stepIndex];
            var currentStep = current.CurrentStep;
            
            float timeDelta = current.Offset - prev.Offset;
            float prevToCurrentProgress = (sec - prev.Offset) / timeDelta;
            float offsetValue = prev.Distance + currentStep.Speed * speed * (sec - prev.Offset);
            
            if (currentStep.IsLinear)
            {
                _Position.StartPosition = Vector2.LerpUnclamped(prevStep.StartPointPosition, currentStep.StartPointPosition, prevToCurrentProgress);
                _Position.EndPosition = Vector2.LerpUnclamped(prevStep.EndPointPosition, currentStep.EndPointPosition, prevToCurrentProgress);
                _Position.Offset = offsetValue;

                return _Position;
            }
            
            // Non-linear interpolation
            float startEaseX = currentStep.StartEaseX.Get(prevToCurrentProgress);
            float startEaseY = currentStep.StartEaseY.Get(prevToCurrentProgress);
            float endEaseX = currentStep.EndEaseX.Get(prevToCurrentProgress);
            float endEaseY = currentStep.EndEaseY.Get(prevToCurrentProgress);
            
            _Position.StartPosition = new Vector2(
                Mathf.LerpUnclamped(prevStep.StartPointPosition.x, currentStep.StartPointPosition.x, startEaseX),
                Mathf.LerpUnclamped(prevStep.StartPointPosition.y, currentStep.StartPointPosition.y, startEaseY)
            );

            _Position.EndPosition = new Vector2(
                Mathf.LerpUnclamped(prevStep.EndPointPosition.x, currentStep.EndPointPosition.x, endEaseX),
                Mathf.LerpUnclamped(prevStep.EndPointPosition.y, currentStep.EndPointPosition.y, endEaseY)
            );

            _Position.Offset = offsetValue;

            return _Position;
        }

        // Binary search to find the step index - O(log n) instead of O(n)
        private int FindStepIndex(float sec)
        {
            int left = 1;
            int right = Steps.Count - 1;
            
            while (left <= right)
            {
                int mid = (left + right) / 2;
                
                if (Steps[mid].Offset > sec)
                {
                    if (mid == 1 || Steps[mid - 1].Offset <= sec)
                        return mid;
                    right = mid - 1;
                }
                else
                {
                    left = mid + 1;
                }
            }
            
            return Steps.Count - 1;
        }

        public void Dispose()
        {
            if (CurrentMesh != null) Object.DestroyImmediate(CurrentMesh);

            foreach (HitObjectManager hitObject in Objects)
                hitObject.Dispose();
        }

        public void RemakeMesh(Mesh mesh, int stepCount)
        {
            var tris = new List<int>();

            FillTriangles(tris, stepCount);
            mesh.SetTriangles(tris, 0);
        }

        /// <summary>
        /// Rewrites <paramref name="tris"/> as the strip for <paramref name="stepCount"/>.
        /// Clearing a list keeps its capacity, so a changing step count stops allocating after
        /// the first few frames — an exact-length array would have to be replaced every time
        /// the count moved, since SetTriangles consumes the whole array.
        /// </summary>
        static void FillTriangles(List<int> tris, int stepCount)
        {
            tris.Clear();

            for (var a = 0; a < stepCount - 1; a++)
            {
                tris.Add(a * 2);
                tris.Add(a * 2 + 1);
                tris.Add(a * 2 + 2);

                tris.Add(a * 2 + 2);
                tris.Add(a * 2 + 1);
                tris.Add(a * 2 + 3);
            }
        }
    }


    public class LaneStepManager
    {
        public LaneStep CurrentStep;

        public float Offset;
        public float Distance;
    }


    public class HitObjectManager
    {
        public ulong     Uuid;
        public HitObject Original;
        public HitObject Current;
        public float     TimeStart;
        public float     TimeEnd;

        public Vector3    Position;
        public Quaternion Rotation;
        public float      Length;

        public Vector3 StartPos;
        public Vector3 EndPos;

        /// <summary>This frame's hold tail, or null when the tail shouldn't be drawn.</summary>
        public Mesh HoldMesh;

        // Kept for the manager's lifetime and refilled in place. HoldMesh points at it when
        // the tail is showing and is nulled otherwise, so consumers keep their existing check.
        private Mesh _HoldMeshInstance;

        public HitObjectManager(HitObject original, HitObject current, float time, LaneManager lane, ChartManager main)
        {
            Uuid = original.UUID > 0 ? original.UUID : original.UUID = main.HighestUuid++;
            Update(original, current, time, lane, main);
        }

        public void Update(HitObject original, HitObject current, float time, LaneManager lane, ChartManager main)
        {
            Original = original;
            Current = current;
            TimeStart = main.Song.Timing.ToSeconds(current.Offset);
            
            // Calculate TimeEnd only once and cache the comparison
            bool isHoldNote = current.HoldLength > 0;
            TimeEnd = isHoldNote ? main.Song.Timing.ToSeconds(current.Offset + current.HoldLength) : TimeStart;

            // Hidden by default; the instance behind it survives so it can be refilled rather
            // than recreated when the note comes back into range.
            HoldMesh = null;

            // Early return if time is past the end - no need to process further
            if (time > TimeEnd)
                return;

            // Handle remaining counts (only when time <= TimeStart)
            if (time <= TimeStart)
            {
                main.HitObjectsRemaining[(int)current.Type]++;
                
                if (current.Flickable) 
                    main.FlicksRemaining++;
            }

            // Main position calculations - only execute when time <= TimeEnd
            LanePosition pos = lane.GetLanePosition(Mathf.Max(TimeStart, time), main.CurrentSpeed);
            Vector3 forwardedOffset = Vector3.forward * pos.Offset;
            
            // Cache data.Position to avoid multiple property access
            float dataPosition = current.Position;
            StartPos = Vector3.LerpUnclamped(pos.StartPosition, pos.EndPosition, dataPosition) + forwardedOffset;
            EndPos = Vector3.LerpUnclamped(pos.StartPosition, pos.EndPosition, dataPosition + current.Length) + forwardedOffset;

            Position = (StartPos + EndPos) * 0.5f; // Multiply by 0.5f is slightly faster than divide by 2

            // A hit object with zero Length puts a zero-length vector into LookRotation, which
            // logs an error and returns identity. Guarding it keeps the same result without the
            // log; Unity's own normalize threshold is 1e-5, so match it rather than test != 0.
            Vector3 direction = EndPos - StartPos;

            Rotation = direction.sqrMagnitude > 1e-10f
                ? Quaternion.LookRotation(direction) * Quaternion.Euler(0, 90, 0)
                : Quaternion.Euler(0, 90, 0);

            Length = Vector3.Distance(StartPos, EndPos);

            // Cache the distance check result
            bool isInRange = pos.Offset < lane.CurrentDistance + 250;
            
            // Generate hold mesh only if needed
            if (isInRange && isHoldNote)
            {
                if (!_HoldMeshInstance)
                {
                    _HoldMeshInstance = new Mesh();
                    _HoldMeshInstance.MarkDynamic();

                    #if UNITY_EDITOR
                    _HoldMeshInstance.name = $"Hold @{TimeStart:0.###}s";
                    #endif
                }

                HoldMesh = lane.GetPartOfLane(
                    Mathf.Max(TimeStart, time), TimeEnd, dataPosition, current.Length, _HoldMeshInstance
                );
            }

            // Update counters
            if (isInRange) 
                main.ActiveHitCount++;
            
            // vertexCount/GetIndexCount read the counts directly; .vertices/.triangles would
            // each copy the whole buffer out of the mesh just to take a Length.
            if (HoldMesh)
            {
                main.ActiveLaneVerts += HoldMesh.vertexCount;
                main.ActiveLaneTris += (int)HoldMesh.GetIndexCount(0);
            }
        }

        public void Dispose()
        {
            if (_HoldMeshInstance) Object.DestroyImmediate(_HoldMeshInstance);

            _HoldMeshInstance = null;
            HoldMesh = null;
        }
    }
}
