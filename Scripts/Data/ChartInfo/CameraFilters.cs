using System;
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
        Greyscale,
        HueShift,
        Inversion,
        Mosaic,
        Noise,
        Reflections,
        Retro,
        SplitScreen,
        Vignette
    }

    [Serializable]
    public class EffectSettings
    {
        public EffectType Type;
        public bool Enabled = true;
        
        [Range(0f, 1f)] public float Strength = 0.5f;
        
        // Bloom-specific
        [Range(1, 100)] public int BloomRadius = 50;
        [Range(0.1f, 100f)] public float BloomSigma = 50f;
        public Vector2 BloomDirection = Vector2.right;
        
        // Split Screen-specific
        [Range(1, 10)] public int SplitsX = 2;
        [Range(1, 10)] public int SplitsY = 2;
        
        // Glitch-specific
        [Range(0f, 1f)] public float StrengthY = 0.1f;
        [Range(0f, 1f)] public float BlockSize = 0.5f;
        
        // Reflections-specific
        [Range(0f, 1f)] public float Scale = 0.1f;

        public EffectSettings(EffectType type)
        {
            Type = type;
        }
    }

    public class Effect
    {
        public Material Material;
        public Action<EffectSettings, Material> ApplySettings;

        public static Effect[] sEffects;
        
        private static readonly int Strength = Shader.PropertyToID("_Strength");

        static Effect()
        {
            sEffects = new Effect[Enum.GetValues(typeof(EffectType)).Length];

            sEffects[(int)EffectType.Bloom] = new Effect
            {
                ApplySettings = (settings, mat) =>
                {
                    mat.SetInt("_Radius", settings.BloomRadius);
                    mat.SetFloat("_Sigma", settings.BloomSigma);
                    mat.SetVector("_BlurDirection", settings.BloomDirection);
                }
            };

            sEffects[(int)EffectType.ChromaticAberration] = new Effect
            {
                ApplySettings = (settings, mat) =>
                {
                    mat.SetFloat(Strength, settings.Strength);
                }
            };

            sEffects[(int)EffectType.FishEye] = new Effect
            {
                ApplySettings = (settings, mat) =>
                {
                    mat.SetFloat(Strength, settings.Strength);
                }
            };

            sEffects[(int)EffectType.Glitch] = new Effect
            {
                ApplySettings = (settings, mat) =>
                {
                    mat.SetFloat("_StrengthX", settings.Strength);
                    mat.SetFloat("_StrengthY", settings.StrengthY);
                    mat.SetFloat("_BlockSize", settings.BlockSize);
                }
            };

            sEffects[(int)EffectType.Greyscale] = new Effect
            {
                ApplySettings = (settings, mat) =>
                {
                    mat.SetFloat(Strength, settings.Strength);
                }
            };

            sEffects[(int)EffectType.HueShift] = new Effect
            {
                ApplySettings = (settings, mat) =>
                {
                    mat.SetFloat(Strength, settings.Strength);
                }
            };

            sEffects[(int)EffectType.Inversion] = new Effect
            {
                ApplySettings = (settings, mat) =>
                {
                    mat.SetFloat(Strength, settings.Strength);
                }
            };

            sEffects[(int)EffectType.Mosaic] = new Effect
            {
                ApplySettings = (settings, mat) =>
                {
                    mat.SetFloat(Strength, settings.Strength);
                }
            };

            sEffects[(int)EffectType.Noise] = new Effect
            {
                ApplySettings = (settings, mat) =>
                {
                    mat.SetFloat(Strength, settings.Strength);
                }
            };

            sEffects[(int)EffectType.Reflections] = new Effect
            {
                ApplySettings = (settings, mat) =>
                {
                    mat.SetFloat(Strength, settings.Strength);
                    mat.SetFloat("_Scale", settings.Scale);
                }
            };

            sEffects[(int)EffectType.Retro] = new Effect
            {
                ApplySettings = (settings, mat) =>
                {
                    mat.SetFloat(Strength, settings.Strength);
                }
            };

            sEffects[(int)EffectType.SplitScreen] = new Effect
            {
                ApplySettings = (settings, mat) =>
                {
                    mat.SetFloat(Strength, settings.Strength);
                    mat.SetInt("_SplitsX", settings.SplitsX);
                    mat.SetInt("_SplitsY", settings.SplitsY);
                }
            };

            sEffects[(int)EffectType.Vignette] = new Effect
            {
                ApplySettings = (settings, mat) =>
                {
                    mat.SetFloat(Strength, settings.Strength);
                }
            };
        }

        public static void LoadMaterials(string basePath = "Shaders/Camera/")
        {
            sEffects[(int)EffectType.Bloom].Material               = new Material(Resources.Load<Shader>(basePath + "Bloom"));
            sEffects[(int)EffectType.ChromaticAberration].Material = new Material(Resources.Load<Shader>(basePath + "ChromaticAberration"));
            sEffects[(int)EffectType.FishEye].Material             = new Material(Resources.Load<Shader>(basePath + "FishEye"));
            sEffects[(int)EffectType.Glitch].Material              = new Material(Resources.Load<Shader>(basePath + "Glitch"));
            sEffects[(int)EffectType.Greyscale].Material           = new Material(Resources.Load<Shader>(basePath + "Greyscale"));
            sEffects[(int)EffectType.HueShift].Material            = new Material(Resources.Load<Shader>(basePath + "HueShift"));
            sEffects[(int)EffectType.Inversion].Material           = new Material(Resources.Load<Shader>(basePath + "Inversion"));
            sEffects[(int)EffectType.Mosaic].Material              = new Material(Resources.Load<Shader>(basePath + "Mosaic"));
            sEffects[(int)EffectType.Noise].Material               = new Material(Resources.Load<Shader>(basePath + "Noise"));
            sEffects[(int)EffectType.Reflections].Material         = new Material(Resources.Load<Shader>(basePath + "Reflections"));
            sEffects[(int)EffectType.Retro].Material               = new Material(Resources.Load<Shader>(basePath + "Retro"));
            sEffects[(int)EffectType.SplitScreen].Material         = new Material(Resources.Load<Shader>(basePath + "SplitScreen"));
            sEffects[(int)EffectType.Vignette].Material            = new Material(Resources.Load<Shader>(basePath + "Vignette"));
        }
    }

    [ExecuteInEditMode]
    public class PostProcessingManager : MonoBehaviour
    {
        public EffectSettings[] Effects;

        private void Start()
        {
            Effect.LoadMaterials();
        }

        void OnRenderImage(RenderTexture src, RenderTexture dest)
        {
            RenderTexture current = src;
            RenderTexture temp = RenderTexture.GetTemporary(src.width, src.height);

            foreach (var effectSettings in Effects)
            {
                if (!effectSettings.Enabled) continue;

                Effect effect = Effect.sEffects[(int)effectSettings.Type];
                if (effect.Material == null) continue;

                // Apply settings to material
                effect.ApplySettings(effectSettings, effect.Material);

                // Blit
                Graphics.Blit(current, temp, effect.Material);

                // Swap
                (current, temp) = (temp, current);
            }

            Graphics.Blit(current, dest);
            RenderTexture.ReleaseTemporary(temp);
        }
    }
}