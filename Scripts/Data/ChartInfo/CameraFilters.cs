using System;
using System.Collections.Generic;
using UnityEngine;

namespace JANOARG.Shared.Data.ChartInfo
{
    [Serializable]
    public enum EffectType
    {
        Bloom,
        ChromaticAberration,
        FishEye,
        Glitch,
        Glitch2,
        Greyscale,
        HueShift,
        Inversion,
        Mosaic,
        Noise,
        Reflections,
        Retro,
        SplitScreen,
        Vignette,
        GaussianBlur,
        MotionBlur,
        ZoomBlur,
        RadialBlur
    }

    [Serializable]
    public class EffectSettings
    {
        public EffectType Type;
        public bool Enabled = true;

        [Range(0f, 1f)] public float Strength = 0.5f;

// Bloom-specific
//
// BloomSigma and BloomRadius are retained from the original port but are no longer read: fluXis
// derives the kernel radius from the sigma, and derives the sigma from Strength. Setting them
// does nothing. BloomDirection is honoured as an axis mask — (1,0) blurs horizontally only.
[Range(0f, 1f)] public float BloomThreshold = 0.65f;
[Range(1, 100)] public int BloomRadius = 50;
[Range(0.1f, 100f)] public float BloomSigma = 50f;
public Vector2 BloomDirection = Vector2.one;

        // Split Screen-specific
        [Range(1, 10)] public int SplitsX = 2;
        [Range(1, 10)] public int SplitsY = 2;

        // Glitch-specific
        [Range(0f, 1f)] public float StrengthY = 0.1f;
        [Range(0f, 1f)] public float BlockSize = 0.5f;

// Reflections-specific
[Range(0f, 1f)] public float Scale = 0.1f;

        // Glitch2-specific
        [Range(0f, 1f)] public float GlitchAmount = 0.1f;
        [Range(0f, 1f)] public float GlitchSpeed  = 0.5f;

        // ZoomBlur / RadialBlur-specific. The centre is stored top-left origin to match the
        // rest of the chart, and flipped to GL's bottom-left origin on the way to the shader.
        public Vector2 BlurCentre = new(0.5f, 0.5f);

        // MotionBlur-specific, in degrees.
        [Range(0f, 360f)] public float BlurAngle;

        /// <summary>Required by Unity's serializer and by any XmlSerializer/JsonUtility round-trip.</summary>
        public EffectSettings() {}

        public EffectSettings(EffectType type)
        {
            Type     = type;
            Strength = Effect.DefaultStrength(type);
        }

        public EffectSettings Clone() => new(Type)
        {
            Enabled       = Enabled,
            Strength      = Strength,
            BloomThreshold = BloomThreshold,
            BloomRadius   = BloomRadius,
            BloomSigma    = BloomSigma,
            BloomDirection = BloomDirection,
            SplitsX       = SplitsX,
            SplitsY       = SplitsY,
            StrengthY     = StrengthY,
            BlockSize     = BlockSize,
            Scale         = Scale,
            GlitchAmount  = GlitchAmount,
            GlitchSpeed   = GlitchSpeed,
            BlurCentre    = BlurCentre,
            BlurAngle     = BlurAngle
        };
    }

    /// <summary>
    /// Static description of one camera effect: where its shader lives and how to push an
    /// <see cref="EffectSettings"/> into a material. Materials are deliberately NOT held here —
    /// they are per-renderer state, so <see cref="PostProcessingManager"/> owns its own.
    /// </summary>
    public class Effect
    {
        public EffectType Type;

        /// <summary>Resource path of the shader, relative to any Resources folder. Null when the effect is unimplemented.</summary>
        public string ResourcePath;

        /// <summary>Pushes settings into the material. Used when <see cref="Draw"/> is null.</summary>
        public Action<EffectSettings, Material> ApplySettings;

        /// <summary>
        /// Set instead of <see cref="ApplySettings"/> for effects that need more than one blit.
        /// Writes the finished result into <see cref="EffectDrawContext.Dest"/>; the manager then
        /// continues the chain from there.
        /// </summary>
        public Action<EffectDrawContext> Draw;

        public static Effect[] sEffects;

        /// <summary>Set by whoever drives the chain so time-varying effects can be made deterministic per frame.</summary>
        public static float FilterTime;

        internal static readonly int StrengthId     = Shader.PropertyToID("_Strength");
        internal static readonly int RadiusId       = Shader.PropertyToID("_Radius");
        internal static readonly int SigmaId        = Shader.PropertyToID("_Sigma");
        internal static readonly int BlurDirectionId = Shader.PropertyToID("_BlurDirection");
        internal static readonly int TexSizeId      = Shader.PropertyToID("_TexSize");
        internal static readonly int AdditiveTexId  = Shader.PropertyToID("_AdditiveTex");
        internal static readonly int ThresholdId    = Shader.PropertyToID("_Threshold");
        internal static readonly int StrengthX      = Shader.PropertyToID("_StrengthX");
        internal static readonly int StrengthY      = Shader.PropertyToID("_StrengthY");
        internal static readonly int BlockSize      = Shader.PropertyToID("_BlockSize");
        internal static readonly int Scale          = Shader.PropertyToID("_Scale");
        internal static readonly int SplitsX        = Shader.PropertyToID("_SplitsX");
        internal static readonly int SplitsY        = Shader.PropertyToID("_SplitsY");
        internal static readonly int FilterTimeId   = Shader.PropertyToID("_FilterTime");
        internal static readonly int AmountId        = Shader.PropertyToID("_Amount");
        internal static readonly int SpeedId         = Shader.PropertyToID("_Speed");
        internal static readonly int PositionId      = Shader.PropertyToID("_Position");
        internal static readonly int SigmaBlurId     = Shader.PropertyToID("_Sigma");

        static Effect()
        {
            sEffects = new Effect[Enum.GetValues(typeof(EffectType)).Length];

            Register(EffectType.Bloom, apply: null, draw: BloomEffect.Draw, BloomEffect.GaussianPath);

            Register(EffectType.ChromaticAberration, (settings, mat) =>
            {
                mat.SetFloat(StrengthId, settings.Strength);
            });

            Register(EffectType.FishEye, (settings, mat) =>
            {
                mat.SetFloat(StrengthId, settings.Strength);
            });

            Register(EffectType.Glitch, (settings, mat) =>
            {
                mat.SetFloat(FilterTimeId, FilterTime);

                // fluXis divides by ten before these reach the shader (Strength / 10f in
                // GlitchShaderStep), because the shader treats them as a direct UV displacement
                // of +/- 0.5. Sent raw, a slider at 1.0 shoves blocks half a screen sideways.
                // The shader's own default of 0.1 is fluXis's effective maximum, which is the
                // scale the port never applied.
                mat.SetFloat(StrengthX, settings.Strength / 10f);
                mat.SetFloat(StrengthY, settings.StrengthY / 10f);
                mat.SetFloat(BlockSize, settings.BlockSize);
            });

            Register(EffectType.Greyscale, (settings, mat) =>
            {
                mat.SetFloat(StrengthId, settings.Strength);
            });

            Register(EffectType.HueShift, (settings, mat) =>
            {
                mat.SetFloat(StrengthId, settings.Strength);
            });

            Register(EffectType.Inversion, (settings, mat) =>
            {
                mat.SetFloat(StrengthId, settings.Strength);
            });

            Register(EffectType.Mosaic, (settings, mat) =>
            {
                mat.SetFloat(StrengthId, settings.Strength);
            });

            Register(EffectType.Noise, (settings, mat) =>
            {
                mat.SetFloat(FilterTimeId, FilterTime);
                mat.SetFloat(StrengthId, settings.Strength);
            });

            Register(EffectType.Reflections, (settings, mat) =>
            {
                mat.SetFloat(StrengthId, settings.Strength);
                mat.SetFloat(Scale, settings.Scale);
            });

            Register(EffectType.Retro, (settings, mat) =>
            {
                mat.SetFloat(StrengthId, settings.Strength);
            });

            Register(EffectType.SplitScreen, (settings, mat) =>
            {
                mat.SetFloat(StrengthId, settings.Strength);
                mat.SetInt(SplitsX, settings.SplitsX);
                mat.SetInt(SplitsY, settings.SplitsY);
            });

            Register(EffectType.Vignette, (settings, mat) =>
            {
                mat.SetFloat(StrengthId, settings.Strength);
            });

            Register(EffectType.Glitch2, (settings, mat) =>
            {
                mat.SetFloat(FilterTimeId, FilterTime);
                mat.SetFloat(AmountId, settings.GlitchAmount);
                mat.SetFloat(SpeedId, settings.GlitchSpeed);
            });

            // GaussianBlur and MotionBlur are multi-pass like Bloom, and share its kernel shader.
            Register(EffectType.GaussianBlur, apply: null, draw: GaussianBlurEffect.Draw, resourcePath: BloomEffect.GaussianPath);
            Register(EffectType.MotionBlur,   apply: null, draw: MotionBlurEffect.Draw,   resourcePath: BloomEffect.GaussianPath);

            Register(EffectType.ZoomBlur, (settings, mat) =>
            {
                mat.SetVector(PositionId, ToGlCentre(settings.BlurCentre));
                mat.SetFloat(SigmaBlurId, settings.Strength / 2f + 0.5f);
            });

            Register(EffectType.RadialBlur, (settings, mat) =>
            {
                mat.SetVector(PositionId, ToGlCentre(settings.BlurCentre));
                mat.SetFloat(SigmaBlurId, settings.Strength / 4f + 0.5f);
            });
        }

        /// <summary>
        /// The chart works in a top-left origin; GL samples bottom-left. fluXis folds the flip into
        /// the centre it sends to the shader, and so do we, so the centre field stays in the same
        /// coordinate space as everything else in the editor.
        /// </summary>
        static Vector4 ToGlCentre(Vector2 centre) => new(centre.x, 1f - centre.y, 0f, 0f);

        static void Register(EffectType type, Action<EffectSettings, Material> apply, Action<EffectDrawContext> draw = null, string resourcePath = null)
        {
            int index = (int)type;
            if (index < 0 || index >= sEffects.Length)
            {
                Debug.LogError($"[CameraFilters] {type} is outside the registered effect range and was skipped.");
                return;
            }

            sEffects[index] = new Effect
            {
                Type          = type,
                ResourcePath  = resourcePath ?? DefaultResourcePath + type.ToString(),
                ApplySettings = apply,
                Draw          = draw
            };
        }

        internal const string DefaultResourcePath = "Shaders/Camera/";

        /// <summary>Null-safe lookup. Returns null for out-of-range or unregistered effect types.</summary>
        public static Effect Get(EffectType type)
        {
            int index = (int)type;
            return index >= 0 && index < sEffects.Length ? sEffects[index] : null;
        }

        public static IReadOnlyList<EffectType> AllTypes => (EffectType[])Enum.GetValues(typeof(EffectType));

        /// <summary>
        /// Where a freshly added effect starts.
        ///
        /// fluXis's vignette curve is left exactly as written, but it bases its falloff on a term
        /// that peaks at 1/16 at the centre of the frame, so pow() drives it to zero almost
        /// immediately: at strength 0.5 the exact centre is 82% black, at 0.25 it is 54%. fluXis
        /// sets no default strength at all, so chart authors simply never go that high. Starting
        /// the slider at 0.1 puts the centre at 25%, which is a vignette you can still see the
        /// chart through.
        /// </summary>
        public static float DefaultStrength(EffectType type) => type switch
        {
            EffectType.Vignette => 0.1f,
            _ => 0.5f
        };
    }

    /// <summary>
    /// What a multi-pass effect gets handed for one step of the chain. Effects that only need a
    /// single blit should leave <see cref="Effect.Draw"/> null and use <see cref="Effect.ApplySettings"/>.
    /// </summary>
    public class EffectDrawContext
    {
        public RenderTexture Src;
        public RenderTexture Dest;
        public EffectSettings Settings;
        public Material Material;
        public PostProcessingManager Manager;

        public EffectDrawContext()
        {
        }

        /// <summary>Re-points this instance at one step of the chain and returns it.</summary>
        public EffectDrawContext Bind(PostProcessingManager manager, RenderTexture src, RenderTexture dest, EffectSettings settings, Material material)
        {
            Manager  = manager;
            Src      = src;
            Dest     = dest;
            Settings = settings;
            Material = material;
            return this;
        }

    }

    /// <summary>
    /// Half-resolution scratch buffers for the effects built on the separable gaussian kernel —
    /// Bloom, GaussianBlur and MotionBlur all blur at half scale first, exactly as fluXis does,
    /// and pay for it in sharpness rather than in bandwidth.
    /// </summary>
    public class BlurBuffers : IDisposable
    {
        public RenderTexture HalfA;
        public RenderTexture HalfB;

        Material _compose;
        string   _composePath;

        int _width, _height;

        public void Ensure(RenderTexture src)
        {
            int width  = Mathf.CeilToInt(src.width  * GaussianKernel.BufferScale);
            int height = Mathf.CeilToInt(src.height * GaussianKernel.BufferScale);

            if (HalfA != null && HalfA.width == width && HalfA.height == height) return;

            ReleaseBuffers();

            HalfA = NewBuffer(width, height, src);
            HalfB = NewBuffer(width, height, src);
            HalfA.Create();
            HalfB.Create();

            _width  = width;
            _height = height;
        }

        static RenderTexture NewBuffer(int width, int height, RenderTexture src) =>
            new(width, height, 0, src.format, src.sRGB ? RenderTextureReadWrite.sRGB : RenderTextureReadWrite.Default)
            {
                hideFlags  = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode   = TextureWrapMode.Clamp
            };

        /// <summary>Matches Unity's _MainTex_TexelSize convention: xy is pixels, zw the reciprocal.</summary>
        public Vector4 TexelSize => new(_width, _height, 1f / _width, 1f / _height);

        /// <summary>
        /// The pass that turns a blurred buffer back into the frame. Obtained by path so that
        /// Bloom's additive compose and the dither passes the other two use can share the buffers.
        /// </summary>
        public Material GetCompose(string resourcePath)
        {
            if (_compose != null && _composePath == resourcePath) return _compose;

            if (_compose != null) { UnityEngine.Object.DestroyImmediate(_compose); _compose = null; }

            Shader shader = Resources.Load<Shader>(resourcePath);
            if (shader == null)
            {
                Debug.LogError($"[CameraFilters] Missing shader at \"{resourcePath}\".");
                return null;
            }

            _compose     = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            _composePath = resourcePath;
            return _compose;
        }

        void ReleaseBuffers()
        {
            if (HalfA != null) { HalfA.Release(); UnityEngine.Object.DestroyImmediate(HalfA); HalfA = null; }
            if (HalfB != null) { HalfB.Release(); UnityEngine.Object.DestroyImmediate(HalfB); HalfB = null; }
        }

        public void Dispose()
        {
            ReleaseBuffers();
            if (_compose != null) { UnityEngine.Object.DestroyImmediate(_compose); _compose = null; }
        }
    }

    /// <summary>The separable gaussian the three kernel-based effects share, plus its radius maths.</summary>
    public static class GaussianKernel
    {
        public const float BufferScale = 0.5f;

        /// <summary>Sets the kernel up for one axis and hands back the blur target.</summary>
        public static RenderTexture Axis(EffectDrawContext context, BlurBuffers buffers, float sigma, Vector2 direction, RenderTexture source)
        {
            context.Material.SetFloat(Effect.SigmaId, sigma);
            context.Material.SetInt(Effect.RadiusId, Radius(sigma));
            context.Material.SetVector(Effect.TexSizeId, buffers.TexelSize);
            context.Material.SetVector(Effect.BlurDirectionId, direction);

            Graphics.Blit(source, buffers.HalfA, context.Material);
            return buffers.HalfA;
        }

        /// <summary>finXis's Blur.KernelSize: how far the gaussian is still within
        /// <paramref name="cutoffThreshold"/> of its peak.</summary>
        public static int Radius(float sigma, float cutoffThreshold = 0.1f)
        {
            if (sigma <= 0f) return 0;

            const int maxRadius = 200;

            double threshold = cutoffThreshold * EvalGaussian(0, sigma);
            for (int i = 0; i < maxRadius; i++)
            {
                if (EvalGaussian(i, sigma) < threshold) return Mathf.Max(i - 1, 0);
            }

            return maxRadius;
        }

        static double EvalGaussian(float x, float sigma)
        {
            const double invSqrt2Pi = 0.39894;
            return invSqrt2Pi * Math.Exp(-0.5 * x * x / (sigma * sigma)) / sigma;
        }
    }

    /// <summary>
    /// Bloom, ported from fluXis's BloomShaderStep.
    ///
    /// It cannot be a single blit: fluXis blurs separably (horizontal then vertical) at half
    /// resolution and then adds the result back onto the scene, rather than replacing the frame
    /// with it. Doing only one axis, or replacing instead of adding, gives a directional smear
    /// that darkens the image.
    ///
    /// Strength drives everything, as in fluXis: it sets the gaussian sigma, and it is also the
    /// intensity the blurred frame is added at.
    /// </summary>
    public static class BloomEffect
    {
        const float MaxSigma = 20f;

        /// <summary>The separable kernel lives in one shader shared with GaussianBlur and MotionBlur.</summary>
        public const string GaussianPath = Effect.DefaultResourcePath + "Gaussian";

        public static void Describe(EffectSettings settings, Material blur, System.Text.StringBuilder report)
        {
            float strength = settings.Strength;
            float sigma = MaxSigma * strength * Mathf.Max(0.5f, 1f - strength * 0.75f);

            Vector2 last = Vector2.zero;
            if (blur != null && blur.HasProperty(Effect.BlurDirectionId))
                last = blur.GetVector(Effect.BlurDirectionId);

            report.Append($" | sigma {sigma:F3} radius {GaussianKernel.Radius(sigma)} " +
                          $"texel {blur?.GetVector(Effect.TexSizeId)} axes ({last.x:F0},{last.y:F0}) " +
                          $"threshold {settings.BloomThreshold:F2}");
        }

        public static void Draw(EffectDrawContext context)
        {
            BlurBuffers buffers = context.Manager.EffectResource(EffectType.Bloom, () => new BlurBuffers());
            if (buffers == null) return;

            buffers.Ensure(context.Src);

            float strength = context.Settings.Strength;

            // fluXis: sigma = max_sigma * Strength * downsampleScale. Both ends of the range
            // matter — the floor keeps sigma from collapsing before the kernel is meaningful.
            float downsampleScale = Mathf.Max(0.5f, 1f - strength * 0.75f);
            float sigma = MaxSigma * strength * downsampleScale;

            Vector2 axes = context.Settings.BloomDirection;
            RenderTexture blurred = context.Src;

            // Horizontal, then vertical. BloomDirection is honoured as an axis mask so a caller
            // can deliberately blur one way only.
            if (axes.x != 0f) blurred = GaussianKernel.Axis(context, buffers, sigma, Vector2.right, context.Src);

            if (axes.y != 0f)
            {
                Graphics.Blit(blurred, buffers.HalfB, context.Material);
                blurred = buffers.HalfB;
            }

            // Both axes masked off — there is nothing to add, so leave the frame alone rather
            // than compositing in a buffer that was never written this frame.
            if (blurred == context.Src) return;

            Material compose = buffers.GetCompose(Effect.DefaultResourcePath + "BloomCompose");
            if (compose == null) return;

            compose.SetFloat(Effect.StrengthId, strength);
            compose.SetFloat(Effect.ThresholdId, context.Settings.BloomThreshold);
            compose.SetTexture(Effect.AdditiveTexId, blurred);
            Graphics.Blit(context.Src, context.Dest, compose);
        }
    }

    /// <summary>
    /// GaussianBlur, ported from fluXis's GaussianBlurStep. Same separable kernel and half-res
    /// buffers as Bloom, but the blurred frame replaces the scene instead of being added to it,
    /// with a dither pass on the way out to keep the wide low-contrast result from banding.
    /// </summary>
    public static class GaussianBlurEffect
    {
        const float MaxSigma = 32f;
        const float MinScale = 0.5f;

        public static void Draw(EffectDrawContext context)
        {
            BlurBuffers buffers = context.Manager.EffectResource(EffectType.GaussianBlur, () => new BlurBuffers());
            if (buffers == null) return;

            buffers.Ensure(context.Src);

            float strength = context.Settings.Strength;
            if (strength <= 0f) return;

            float sigma = MaxSigma * strength * Mathf.Max(MinScale, 1f - strength);

            RenderTexture blurred = GaussianKernel.Axis(context, buffers, sigma, Vector2.right, context.Src);
            Graphics.Blit(blurred, buffers.HalfB, context.Material);

            Dither(context, buffers, blurred, strength * 6f);
        }

        internal static void Dither(EffectDrawContext context, BlurBuffers buffers, RenderTexture blurred, float strength)
        {
            Material dither = buffers.GetCompose(Effect.DefaultResourcePath + "Dither");
            if (dither == null) return;

            dither.SetFloat(Effect.StrengthId, strength);
            Graphics.Blit(blurred, context.Dest, dither);
        }
    }

    /// <summary>
    /// MotionBlur, ported from fluXis's MotionBlurStep. The same gaussian kernel as Bloom, but
    /// swept along an arbitrary angle twice rather than once per axis, at half resolution.
    /// </summary>
    public static class MotionBlurEffect
    {
        const float MaxSigma = 45f;
        const float MinScale = 0.4f;

        public static void Draw(EffectDrawContext context)
        {
            BlurBuffers buffers = context.Manager.EffectResource(EffectType.MotionBlur, () => new BlurBuffers());
            if (buffers == null) return;

            buffers.Ensure(context.Src);

            float strength = context.Settings.Strength;
            if (strength <= 0f) return;

            float sigma = MaxSigma * strength * Mathf.Max(MinScale, 1f - strength);

            float radians = context.Settings.BlurAngle * Mathf.Deg2Rad;
            Vector2 direction = new(Mathf.Cos(radians), Mathf.Sin(radians));

            // Two passes along the same axis. fluXis does the same: repeating a directional
            // gaussian widens it, which is the point.
            GaussianKernel.Axis(context, buffers, sigma, direction, context.Src);
            Graphics.Blit(buffers.HalfA, buffers.HalfB, context.Material);

            GaussianBlurEffect.Dither(context, buffers, buffers.HalfB, strength * 8f);
        }
    }


    /// <summary>
    /// Runs an ordered stack of camera effects over everything its camera renders.
    /// Attach to a camera; effects are applied in array order.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class PostProcessingManager : MonoBehaviour
    {
        [Tooltip("Effects applied in order. An entry with nothing enabled is skipped entirely.")]
        public EffectSettings[] Effects = Array.Empty<EffectSettings>();

        [Tooltip("When set, the camera renders untouched. Used to keep offscreen utility renders (icon export) clean.")]
        public bool Bypass;

        /// <summary>Drives _FilterTime for time-varying effects (Noise, Glitch). Null means use Unity's Time.time.</summary>
        public float? TimeOverride;

        [Tooltip("Logs the camera viewport and intermediate size for the first few frames after the hook activates.")]
        public bool LogDiagnostics;

        readonly Dictionary<EffectType, Material> _materials = new();
        readonly Dictionary<EffectType, IDisposable> _effectResources = new();

        /// <summary>Resolved once per frame by CollectActive, so the passes do not repeat the lookups.</summary>
        readonly List<ActiveEffect> _active = new();

        Camera _camera;
        bool   _cameraResolved;

        /// <summary>Reused across frames — the chain is sequential, so one is enough.</summary>
        readonly EffectDrawContext _drawContext = new();

        Camera Camera
        {
            get
            {
                if (!_cameraResolved)
                {
                    _camera = GetComponent<Camera>();
                    _cameraResolved = true;
                }
                return _camera;
            }
        }

        bool _materialsBuilt;

        /// <summary>
        /// Scratch state an effect needs beyond its own material — extra render targets, composite
        /// materials. Tied to the manager's lifetime so a disabled or revalidated component lets
        /// go of its GPU allocations.
        /// </summary>
        public T EffectResource<T>(EffectType type, Func<T> create) where T : class, IDisposable
        {
            if (_effectResources.TryGetValue(type, out IDisposable existing) && existing is T typed)
                return typed;

            if (existing != null) existing.Dispose();

            T created = create();
            _effectResources[type] = created;
            return created;
        }

        /// <summary>True when at least one entry would actually draw a blit this frame.</summary>
        public bool HasActiveEffects
        {
            get
            {
                if (Effects == null) return false;

                foreach (EffectSettings settings in Effects)
                {
                    if (settings is { Enabled: true } && Effect.Get(settings.Type) != null)
                        return true;
                }
                return false;
            }
        }

        void OnEnable() => BuildMaterials();

        void OnDestroy()
        {
            SetHdrOverridden(false);
            ReleaseMaterials();
        }

        /// <summary>
        /// Unity only routes a camera through the OnRenderImage intermediate while this component
        /// is enabled. Leaving it enabled for an empty stack costs an intermediate render target
        /// and a full-screen blit every frame, whether or not any effect is on, so the hook takes
        /// itself out of the pipeline when there is nothing to do.
        ///
        /// Driven from mutation sites rather than a tick: a disabled component does not receive
        /// Update, so it cannot re-enable itself.
        /// </summary>
        public void SyncActivity()
        {
            _loggedFrames = 0;

            bool active = HasActiveEffects;
            SetHdrOverridden(active);
            enabled = active;
        }

        bool _hdrOverridden;

        /// <summary>
        /// Turns camera HDR off while the chain is running and puts it back afterwards.
        ///
        /// Nothing this view draws exceeds 1.0 — the lane, hit and highlight shaders all return
        /// plain albedo and alpha, and there is no tonemapping anywhere in the chain. HDR
        /// therefore buys nothing here, but it does force the hook's intermediate to ARGBHalf
        /// instead of BGRA8, doubling the bandwidth of every pass for values nobody produces.
        /// </summary>
        void SetHdrOverridden(bool disable)
        {
            Camera cam = Camera;
            if (cam == null || _hdrOverridden == disable) return;

            if (disable)
            {
                _hdrWasAllowed  = cam.allowHDR;
                cam.allowHDR    = false;
                _hdrOverridden  = true;
            }
            else
            {
                cam.allowHDR   = _hdrWasAllowed;
                _hdrOverridden = false;
            }
        }

        bool _hdrWasAllowed;

        int _loggedFrames;

        /// <summary>
        /// Reports what the hook is actually being handed. The numbers that matter are whether the
        /// intermediate matches the camera viewport or the whole window, and whether each effect
        /// was handed sane parameters — a gaussian sigma or texel size gone wrong produces
        /// uninitialised texture reads long before it produces a visible error.
        /// </summary>
        void LogFrame(RenderTexture src)
        {
            if (!LogDiagnostics || _loggedFrames >= 3) return;
            _loggedFrames++;

            Camera cam = Camera;

            System.Text.StringBuilder report = new();
            report.Append($"[CameraFilters] viewport {cam.pixelWidth}x{cam.pixelHeight} rect {cam.rect} | ");
            report.Append($"intermediate {src.width}x{src.height} fmt {src.format} aa {src.antiAliasing} | ");
            report.Append($"screen {Screen.width}x{Screen.height} aa {QualitySettings.antiAliasing} | ");
            report.Append($"{SystemInfo.graphicsDeviceName}");

            foreach (ActiveEffect active in _active)
            {
                report.Append($"\n    {active.Effect.Type} strength {active.Settings.Strength:F2}");
                if (active.Effect.Type == EffectType.Bloom)
                {
                    BloomEffect.Describe(active.Settings, active.Material, report);
                }
            }


            Debug.Log(report.ToString());
        }




        void OnValidate()
        {
            // Effect types can be edited live in the inspector, so drop anything stale. Releasing
            // rather than just clearing the flag matters: BuildMaterials overwrites the dictionary
            // entries, which would otherwise orphan the old materials.
            ReleaseMaterials();
            SyncActivity();
        }

        void BuildMaterials()
        {
            if (_materialsBuilt) return;
            _materialsBuilt = true;

            foreach (Effect effect in Effect.sEffects)
            {
                if (effect?.ResourcePath == null) continue;

                Shader shader = Resources.Load<Shader>(effect.ResourcePath);
                if (shader == null)
                {
                    Debug.LogError($"[CameraFilters] Missing shader at \"{effect.ResourcePath}\". That effect will not render.");
                    continue;
                }

                _materials[effect.Type] = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            }
        }

        void ReleaseMaterials()
        {
            foreach (Material material in _materials.Values)
            {
                if (material != null) DestroyImmediate(material);
            }
            _materials.Clear();

            foreach (IDisposable resource in _effectResources.Values) resource.Dispose();
            _effectResources.Clear();

            _materialsBuilt = false;
        }


        void OnRenderImage(RenderTexture src, RenderTexture dest)
        {
            BuildMaterials();
            CollectActive();
            LogFrame(src);

            if (Bypass || _active.Count == 0)
            {
                Graphics.Blit(src, dest);
                return;
            }

            Effect.FilterTime = TimeOverride ?? Time.time;

            // Single effect goes straight into dest: one blit and no ping-pong buffer at all.
            // That is the common case while tuning, and the temp target is the expensive part.
            if (_active.Count == 1)
            {
                ActiveEffect only = _active[0];

                if (only.Effect.Draw != null)
                {
                    only.Effect.Draw(_drawContext.Bind(this, src, dest, only.Settings, only.Material));
                }
                else
                {
                    only.Effect.ApplySettings(only.Settings, only.Material);
                    Graphics.Blit(src, dest, only.Material);
                }
                return;
            }

            RenderTexture current = src;

            // depth 0: nothing in the chain samples depth, and passing src.depth would allocate a
            // depth attachment on the temporary every frame.
            RenderTexture temp = RenderTexture.GetTemporary(
                src.width, src.height, 0, src.format,
                src.sRGB ? RenderTextureReadWrite.sRGB : RenderTextureReadWrite.Default);

            foreach (ActiveEffect active in _active)
            {
                if (active.Effect.Draw != null)
                {
                    active.Effect.Draw(_drawContext.Bind(this, current, temp, active.Settings, active.Material));
                }
                else
                {
                    active.Effect.ApplySettings(active.Settings, active.Material);
                    Graphics.Blit(current, temp, active.Material);
                }

                (current, temp) = (temp, current);
            }

            Graphics.Blit(current, dest);
            RenderTexture.ReleaseTemporary(temp);
        }

        /// <summary>
        /// Resolves the enabled entries once, so the frame does not repeat the dictionary and
        /// enum lookups for every effect on every pass.
        /// </summary>
        void CollectActive()
        {
            _active.Clear();
            if (Effects == null) return;

            foreach (EffectSettings settings in Effects)
            {
                if (settings is not { Enabled: true }) continue;

                Effect effect = Effect.Get(settings.Type);
                if (effect == null) continue;
                if (!_materials.TryGetValue(settings.Type, out Material material) || material == null) continue;

                _active.Add(new ActiveEffect(settings, effect, material));
            }
        }

        readonly struct ActiveEffect
        {
            public readonly EffectSettings Settings;
            public readonly Effect Effect;
            public readonly Material Material;

            public ActiveEffect(EffectSettings settings, Effect effect, Material material)
            {
                Settings = settings;
                Effect   = effect;
                Material = material;
            }
        }

        /// <summary>Adds or replaces an effect in the stack. Passing null for settings removes the entry.</summary>
        public void SetEffect(EffectSettings settings)
        {
            if (settings == null) return;

            List<EffectSettings> list = new(Effects ?? Array.Empty<EffectSettings>());
            int existing = list.FindIndex(x => x.Type == settings.Type);

            if (existing >= 0) list[existing] = settings;
            else list.Add(settings);

            Effects = list.ToArray();
            SyncActivity();
        }

        public EffectSettings GetEffect(EffectType type)
        {
            if (Effects == null) return null;
            return Array.Find(Effects, x => x.Type == type);
        }

        public void RemoveEffect(EffectType type)
        {
            if (Effects == null) return;
            Effects = Array.FindAll(Effects, x => x.Type != type);
            SyncActivity();
        }
    }
}