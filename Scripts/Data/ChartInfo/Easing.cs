using System;
using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;

namespace JANOARG.Shared.Data.ChartInfo
{
    [Serializable]
    public enum EaseMode
    {
        In, Out, InOut
    }

    [Serializable]
    public enum EaseFunction
    {
        Linear,
        Sine,
        Quadratic,
        Cubic,
        Quartic,
        Quintic,
        Exponential,
        Circle,
        Back,
        Elastic,
        Bounce
    }
    
    [Serializable]
    public static class EaseUtils
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float LerpTo(float from, float to, float interpolator, EaseFunction easeFunc, EaseMode mode) =>
            (1 - Ease.Get(interpolator, easeFunc, mode)) * from + Ease.Get(interpolator, easeFunc, mode) * to;
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float InverseLerpTo(float from, float to, float interpolator, EaseFunction easeFunc, EaseMode mode) =>
            Ease.Get(interpolator, easeFunc, mode) * from + (1 - Ease.Get(interpolator, easeFunc, mode)) * to;
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float LerpBy(float from, float delta, float interpolator, EaseFunction easeFunc, EaseMode mode) =>
            (1 - Ease.Get(interpolator, easeFunc, mode)) * from + Ease.Get(interpolator, easeFunc, mode) * (from + delta);
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float InverseLerpBy(float from, float delta, float interpolator, EaseFunction easeFunc, EaseMode mode) =>
            from + delta * (1 - Ease.Get(interpolator, easeFunc, mode));
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ToZero(float from, float interpolator, EaseFunction easeFunc, EaseMode mode) =>
            from * (1 - Ease.Get(interpolator, easeFunc, mode));
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float FromZero(float to, float interpolator, EaseFunction easeFunc, EaseMode mode) =>
            to * Ease.Get(interpolator, easeFunc, mode);
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float BlastIn(float to, float interpolator, EaseFunction easeFunc, EaseMode mode) =>
            to / Ease.Get(interpolator, easeFunc, mode);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float BlastOut(float from, float interpolator, EaseFunction easeFunc, EaseMode mode) =>
            from / (1 - Ease.Get(interpolator, easeFunc, mode));
        
        
        // For predefined eases
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float LerpTo(float from, float to, float ease) =>
            (1 - ease) * from + ease * to;
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float InverseLerpTo(float from, float to, float ease) =>
            ease * from + (1 - ease) * to;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float LerpBy(float from, float delta, float ease) =>
            (1 - ease) * from + ease * (from + delta);
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float InverseLerpBy(float from, float delta, float ease) =>
            from + delta * (1 - ease);
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ToZero(float from, float ease) =>
            from * (1 - ease);
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float FromZero(float to, float ease) =>
            to * ease;
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Snap(float from, float to, float ease) =>
            (int)ease == 1 ? to : from;
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float BlastIn(float to, float ease) =>
            to / ease;
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float BlastOut(float from, float ease) =>
            from / (1 - ease);
        
    }

    [Serializable]
    public class Ease
    {
        public Func<float, float> In;
        public Func<float, float> Out;
        public Func<float, float> InOut;

        private static bool s_cancelRequested;
        private static bool s_forceCancelRequested;
        
        /// <summary>
        /// Properly finish the current Animate loop by skipping to callback(1).
        /// Recommended to use this over StopCoroutine.
        /// </summary>
        public static void Skip(bool force = false)
        {
            s_cancelRequested = true;
            
            if (force) s_forceCancelRequested = true;
            
        }

        [SuppressMessage("ReSharper", "CompareOfFloatsByEqualityOperator")] // We don't care about floating point errors here
        public static float Get(float x, EaseFunction easeFunc, EaseMode mode, float multiplier = 1, float delay = 0, float xPow = 1)
        {
            // Only operate on non-default optional parameters
            x = multiplier != 1f ? x * multiplier   : x;
            x = delay      != 0f ? x - delay        : x;
            x = xPow       != 1f ? FastPow(x, xPow) : x;
            
            x = x > 1 ? 1 : x;
            x = x < 0 ? 0 : x;
            
            // No need to cache on static readonly arrays
            return mode switch
            {
                EaseMode.In  => srEases[(int)easeFunc].In(x),
                EaseMode.Out => srEases[(int)easeFunc].Out(x), 
                _            => srEases[(int)easeFunc].InOut(x)
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float GetSharpened(float x, float p, EaseFunction func, EaseMode mode, float multiplier = 1, float delay = 0) =>
            FastPow(Get(x * multiplier - delay, func, mode), p);

        // We don't need DOTween, guys
        
        /// <summary>
        /// Animates a value from 0 to 1 over specified duration, invoking callback each frame with linear progress.
        /// Supports cancellation via Ease.Skip
        /// </summary>
        /// <param name="duration">Total animation time in seconds</param>
        /// <param name="callback">Action receiving linear progress (0 to 1) each frame</param>
        public static IEnumerator Animate(float duration, Action<float> callback)
        {
            for (float a = 0; a < 1; a += Time.deltaTime / duration)
            {
                if (s_cancelRequested)
                {
                    s_cancelRequested = false;
                    break;
                }
                
                callback(a);

                yield return null;
            }

            if (s_forceCancelRequested)
            {
                s_forceCancelRequested = false;
                yield break;
            }

            callback(1);
        }

        /// <summary>
        /// Animates with easing, with support for shortcuts to ease parameters for callback.
        /// Useful for creating multiple easings with similar parameters, with declarative syntax.
        /// </summary>
        /// <param name="duration">Total animation time in seconds</param>
        /// <param name="easeFunc">Easing function type</param>
        /// <param name="mode">Easing mode (In/Out/InOut)</param>
        /// <param name="callback">Action receiving (progress, easeFunc, mode) each frame</param>
        public static IEnumerator Animate(float duration, EaseFunction easeFunc, EaseMode mode, Action<float, EaseFunction, EaseMode> callback)
        {
            for (float a = 0; a < 1; a += Time.deltaTime / duration)
            {
                if (s_cancelRequested)
                {
                    s_cancelRequested = false;
                    break;
                }
                
                callback(a, easeFunc, mode);

                yield return null;
            }

            if (s_forceCancelRequested)
            {
                s_forceCancelRequested = false;
                yield break;
            }
            callback(1, easeFunc, mode);
        }
        
        /// <summary>
        /// Animates with easing, automatically calculating eased value and providing all parameters.
        /// Most comprehensive version - gives access to raw progress, ease parameters shortcuts, and pre-calculated eased value.
        /// </summary>
        /// <param name="duration">Total animation time in seconds</param>
        /// <param name="easeFunc">Easing function type</param>
        /// <param name="mode">Easing mode (In/Out/InOut)</param>
        /// <param name="callback">Action receiving (progress, easeFunc, mode, easedValue) each frame</param>
        public static IEnumerator Animate(float duration, EaseFunction easeFunc, EaseMode mode, Action<float, EaseFunction, EaseMode, float> callback)
        {
            float ease;
            for (float a = 0; a < 1; a += Time.deltaTime / duration)
            {
                if (s_cancelRequested)
                {
                    s_cancelRequested = false;
                    break;
                }
                
                ease = Get(a, easeFunc, mode);
                callback(a, easeFunc, mode, ease);

                yield return null;
            }

            if (s_forceCancelRequested)
            {
                s_forceCancelRequested = false;
                yield break;
            }
            ease = Get(1, easeFunc, mode);
            callback(1, easeFunc, mode, ease);
        }
        
        /// <summary>
        /// Animates with easing, automatically calculating, and providing only the eased value to callback.
        /// Simplest eased animation - callback receives only the pre-calculated eased progress (0 to 1).
        /// </summary>
        /// <param name="duration">Total animation time in seconds</param>
        /// <param name="easeFunc">Easing function type</param>
        /// <param name="mode">Easing mode (In/Out/InOut)</param>
        /// <param name="callback">Action receiving eased progress value (0 to 1) each frame</param>
        public static IEnumerator Animate(float duration, EaseFunction easeFunc, EaseMode mode, Action<float> callback)
        {
            float ease;
            for (float a = 0; a < 1; a += Time.deltaTime / duration)
            {
                if (s_cancelRequested)
                {
                    s_cancelRequested = false;
                    break;
                }
                
                ease = Get(a, easeFunc, mode);
                callback(ease);

                yield return null;
            }

            if (s_forceCancelRequested)
            {
                s_forceCancelRequested = false;
                yield break;
            }
            ease = Get(1, easeFunc, mode);
            callback(ease);
        }
        
        // Task Async alternative
        // Note: This one tries to avoid Unity dependencies, so DeltaTime is not used
        // This may not play well with Unity threads so use this with caution.
        // It is recommended to replace DateTime with your own available clock implementations
        
        /// <summary>
        /// Animates a value from 0 to 1 over specified duration, invoking callback each frame with linear progress.
        /// Supports cancellation via Ease.Skip
        /// </summary>
        /// <param name="duration">Total animation time in seconds</param>
        /// <param name="callback">Action receiving linear progress (0 to 1) each frame</param>
        /// <param name="cancellationToken">Optional cancellation token</param>
        public static async Task Animate(float duration, Action<float> callback, CancellationToken cancellationToken = default)
        {
            var startTime = DateTime.Now;
            var endTime = startTime.AddSeconds(duration);

            while (DateTime.Now < endTime)
            {
                if (s_cancelRequested || cancellationToken.IsCancellationRequested)
                {
                    s_cancelRequested = false;
                    return;
                }

                var elapsed = (float)(DateTime.Now - startTime).TotalSeconds;
                var progress = Math.Min(elapsed / duration, 1f);

                callback(progress);

                await Task.Yield();
            }

            if (s_forceCancelRequested || cancellationToken.IsCancellationRequested)
            {
                s_forceCancelRequested = false;
                return;
            }

            callback(1);
        }

        /// <summary>
        /// Animates with easing, automatically calculating and providing only the eased value to callback.
        /// Simplest eased animation - callback receives only the pre-calculated eased progress (0 to 1).
        /// </summary>
        /// <param name="duration">Total animation time in seconds</param>
        /// <param name="easeFunc">Easing function type</param>
        /// <param name="mode">Easing mode (In/Out/InOut)</param>
        /// <param name="callback">Action receiving eased progress value (0 to 1) each frame</param>
        /// <param name="cancellationToken">Optional cancellation token</param>
        public static async Task Animate(float duration, EaseFunction easeFunc, EaseMode mode, Action<float> callback, CancellationToken cancellationToken = default)
        {
            var startTime = DateTime.Now;
            var endTime = startTime.AddSeconds(duration);

            while (DateTime.Now < endTime)
            {
                if (s_cancelRequested || cancellationToken.IsCancellationRequested)
                {
                    s_cancelRequested = false;
                    return;
                }

                var elapsed = (float)(DateTime.Now - startTime).TotalSeconds;
                var progress = Math.Min(elapsed / duration, 1f);
                var ease = Ease.Get(progress, easeFunc, mode);

                callback(ease);

                await Task.Yield();
            }

            if (s_forceCancelRequested || cancellationToken.IsCancellationRequested)
            {
                s_forceCancelRequested = false;
                return;
            }

            var finalEase = Ease.Get(1, easeFunc, mode);
            callback(finalEase);
        }

        /// <summary>
        /// Animates with easing, with support for shortcuts to ease parameters for callback.
        /// Useful for creating multiple easings with similar parameters, with declarative syntax.
        /// </summary>
        /// <param name="duration">Total animation time in seconds</param>
        /// <param name="easeFunc">Easing function type</param>
        /// <param name="mode">Easing mode (In/Out/InOut)</param>
        /// <param name="callback">Action receiving (progress, easeFunc, mode) each frame</param>
        /// <param name="cancellationToken">Optional cancellation token</param>
        public static async Task Animate(float duration, EaseFunction easeFunc, EaseMode mode, Action<float, EaseFunction, EaseMode> callback, CancellationToken cancellationToken = default)
        {
            var startTime = DateTime.Now;
            var endTime = startTime.AddSeconds(duration);

            while (DateTime.Now < endTime)
            {
                if (s_cancelRequested || cancellationToken.IsCancellationRequested)
                {
                    s_cancelRequested = false;
                    return;
                }

                var elapsed = (float)(DateTime.Now - startTime).TotalSeconds;
                var progress = Math.Min(elapsed / duration, 1f);

                callback(progress, easeFunc, mode);

                await Task.Yield();
            }

            if (s_forceCancelRequested || cancellationToken.IsCancellationRequested)
            {
                s_forceCancelRequested = false;
                return;
            }

            callback(1, easeFunc, mode);
        }

        /// <summary>
        /// Animates with easing, automatically calculating eased value and providing all parameters.
        /// Most comprehensive version - gives access to raw progress, ease parameters shortcuts, and pre-calculated eased value.
        /// </summary>
        /// <param name="duration">Total animation time in seconds</param>
        /// <param name="easeFunc">Easing function type</param>
        /// <param name="mode">Easing mode (In/Out/InOut)</param>
        /// <param name="callback">Action receiving (progress, easeFunc, mode, easedValue) each frame</param>
        /// <param name="cancellationToken">Optional cancellation token</param>
        public static async Task Animate(float duration, EaseFunction easeFunc, EaseMode mode, Action<float, EaseFunction, EaseMode, float> callback, CancellationToken cancellationToken = default)
        {
            var startTime = DateTime.Now;
            var endTime = startTime.AddSeconds(duration);

            while (DateTime.Now < endTime)
            {
                if (s_cancelRequested || cancellationToken.IsCancellationRequested)
                {
                    s_cancelRequested = false;
                    return;
                }

                var elapsed = (float)(DateTime.Now - startTime).TotalSeconds;
                var progress = Math.Min(elapsed / duration, 1f);
                var ease = Ease.Get(progress, easeFunc, mode);

                callback(progress, easeFunc, mode, ease);

                await Task.Yield();
            }

            if (s_forceCancelRequested || cancellationToken.IsCancellationRequested)
            {
                s_forceCancelRequested = false;
                return;
            }

            var finalEase = Ease.Get(1, easeFunc, mode);
            callback(1, easeFunc, mode, finalEase);
        }

        
        

        public static IEnumerator AnimateText(TMP_Text text, float duration, float xOffset, Action<TMP_CharacterInfo, float> letterCallback)
        {
            float minPosition;

            bool finished;

            void f_update(float x)
            {
                text.ForceMeshUpdate();
                finished = true;
                minPosition = float.NaN;

                foreach (TMP_CharacterInfo charInfo in text.textInfo.characterInfo)
                {
                    if (!charInfo.isVisible) continue;

                    if (!float.IsFinite(minPosition)) minPosition = charInfo.vertex_BL.position.x;
                    float prog = Mathf.Clamp01((x - xOffset * (charInfo.vertex_BL.position.x - minPosition)) / duration);
                    letterCallback(charInfo, prog);
                    if (prog < 1) finished = false;
                }

                var index = 0;

                foreach (TMP_MeshInfo meshInfo in text.textInfo.meshInfo)
                {
                    meshInfo.mesh.vertices = meshInfo.vertices;
                    text.UpdateGeometry(meshInfo.mesh, index);
                    index++;
                }
            }

            float elapsedTime = 0;

            while (true)
            {
                f_update(elapsedTime);

                if (finished) break;

                yield return null;

                elapsedTime += Time.deltaTime;
            }
        }
        
        private const float _PI      = Mathf.PI;
        private const float _PI_HALF = _PI / 2;
        private const float _EPSILON  = 0.000001f;
        
        private const float _BACK_OVERSHOOT        = 1.70158f;
        private const float _BACK_SCALED_OVERSHOOT = _BACK_OVERSHOOT * 1.525f;

        private const float _ELASTIC_PERIOD_IN_OUT_INNER = 11.125f;

        private const float _ELASTIC_PERIOD     = _PI * 2 / 3f;
        private const float _ELASTIC_IN_OFFSET  = 10.75f;
        private const float _ELASTIC_OUT_OFFSET = 0.75f;

        private const float _BOUNCE_CONSTANT  = 7.5625f;
        private const float _BOUNCE_THRESHOLD = 2.75f;
        
        public static float FastSin(float x)
        {
            
            // Wrap angle to [-PI, PI]
            x = (x + _PI) % (2 * _PI) - _PI;
            
            const float B = 4f / _PI;
            const float C = -4f / (_PI * _PI);
                
            float y = B * x + C * x * Mathf.Abs(x);
                
            // Optional extra precision (at some performance cost)
            const float P = 0.225f;
            y = P * (y * Mathf.Abs(y) - y) + y;
            
            // Prevent over/undershooting
            y = y > 1 ? 1 : y;
            y = y < -1 ? -1 : y;
                
            return y;
        }
            
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float FastCos(float x) =>
            // Cos in a nutshell: Sine, just translated back by 90 degrees (but we're using rad so yeah)
            FastSin(_PI_HALF - x);

        // Fast power approximation for base 2
        public static float FastPow2(float p)
        {
            float offset = (p < 0) 
                ? 1.0f : 0.0f;
            
            float clipp = (p < -126) 
                ? -126.0f : p;
            
            int w = (int)clipp;
            float z = (clipp - w) + offset;
        
            // Approximation of 2^z for z in [0,1]
            // Where z = p - i, i = floor(p)
            // Uses a fast bit-level hack by manipulating the float’s exponent bits directly.
            // The constants are empirically tuned to produce a close approximation without calling Mathf.Pow.
            // Equivalent to “fast 2^z” in older graphics/audio routines or assembly tricks.
            return BitConverter.Int32BitsToSingle(
                (int)((1 << 23) * (clipp + 121.2740575f + 27.7280233f / (4.84252568f - z) - 1.49012907f * z)));
        }

        // Fast power function for any base
        public static float FastPow(float a, float b, bool forceCalc = false)
        { 
            // Testing
            //return Mathf.Pow(a, b);
            
            // Domain checks first
            Debug.Assert(a >= 0f, "FastPow(float, float): input must be non-negative.");
            Debug.Assert(!float.IsNaN(a) && !float.IsNaN(b));
            Debug.Assert(!float.IsInfinity(a) && !float.IsInfinity(b));

            // Transform exponent
            float exponent = b * Mathf.Log(a, 2);
            
            // Logarithm shouldn't be costly, I think?
            return FastPow2(exponent);
        }
        
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float PseudoFastSqrt(float x) => FastPow(x, 0.5f);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float FastSqrt(float x, bool precision = false)
        {
            // Testing
            //return Mathf.Sqrt(x);
            
            Debug.Assert(x >= 0f, "FastSqrt(float): input must be non-negative.");

            if (x == 0f) return 0f;

            float y = x;
            
            // 1 / derivative of y^2 - x
            const float COEFFICIENT = 0.5f;

            // Single Newton-Raphson iteration: fast approximation of sqrt(x)
            y = COEFFICIENT * (y + x / y);

            // Optional second iteration for slightly higher accuracy.
            // More iterations would exceed float precision and provide negligible benefit.
            if (precision)
                y = COEFFICIENT * (y + x / y);

            y = y > 1 ? 1 : y;
            y = y < 0 ? 0 : y;
            
            return y; 
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double FastSqrt(double x, bool precision = false)
        {
            Debug.Assert(x >= 0.0, "FastSqrt(double): input must be non-negative.");

            if (x == 0.0) return 0.0;

            double y = x;
            
            // 1 / derivative of y^2 - x
            const double COEFFICIENT = 0.5;

            y = COEFFICIENT * (y + x / y);

            // Optional three extra iterations for full double-precision accuracy
            if (precision)
            {
                y = COEFFICIENT * (y + x / y);
                y = COEFFICIENT * (y + x / y);
            }

            return y;
        }


        // Fast approximate equality check
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool FastApproximately(float a, float b)
        {
            // For our easing functions, we typically compare with 1 or 0
            // Using subtraction is faster than Mathf.Abs for this case
            float diff = a - b;
            return diff is < _EPSILON and > -_EPSILON;
        }
        
        public static readonly Ease[] srEases;

        // We will reduce as much external calls as possible,
        // given this library is being called ~3000+ times per frame
        static Ease()
        {
            srEases = new Ease[Enum.GetValues(typeof(EaseFunction)).Length];

            srEases[(int)EaseFunction.Linear] = new Ease
            {
                In = x => x,
                Out = x => x,
                InOut = x => x
            };

            srEases[(int)EaseFunction.Sine] = new Ease
            {
                In = x => 1 - FastCos(x * _PI / 2),
                Out = x => FastSin(x * _PI / 2),
                InOut = x => (1 - FastCos(x * _PI)) / 2
            };

            srEases[(int)EaseFunction.Quadratic] = new Ease
            {
                In = x => x * x,
                Out = x => 1 - ((1 - x) * (1 - x)),
                InOut = x => x < 0.5f
                    ? 2 * x * x
                    : 1 - ((-2 * x + 2) * (-2 * x + 2)) / 2
            };

            srEases[(int)EaseFunction.Cubic] = new Ease
            {
                In = x => x * x * x,
                Out = x => 1 - ((1 - x) * (1 - x) * (1 - x)),
                InOut = x => x < 0.5f
                    ? 4 * x * x * x
                    : 1 - ((-2 * x + 2) * (-2 * x + 2) * (-2 * x + 2)) / 2
            };

            srEases[(int)EaseFunction.Quartic] = new Ease
            {
                In = x => x * x * x * x,
                Out = x => 1 - ((1 - x) * (1 - x) * (1 - x) * (1 - x)),
                InOut = x => x < 0.5f
                    ? 8 * x * x * x * x
                    : 1 - ((-2 * x + 2) * (-2 * x + 2) * (-2 * x + 2) * (-2 * x + 2)) / 2
            };

            // For fuck's sake, why do C# not have an exponent operator??
            // Maybe exponent is not ALU standard
            srEases[(int)EaseFunction.Quintic] = new Ease
            {
                In = x => x * x * x * x * x,
                Out = x => 1 - ((1 - x) * (1 - x) * (1 - x) * (1 - x) * (1 - x)),
                InOut = x => x < 0.5f
                    ? 16 * x * x * x * x * x
                    : 1 - ((-2 * x + 2) * (-2 * x + 2) * (-2 * x + 2) * (-2 * x + 2) * (-2 * x + 2)) / 2
            };

            srEases[(int)EaseFunction.Exponential] = new Ease
            {
                In = x => x == 0
                    ? 0
                    : FastPow(2, 10 * x - 10) - 0.0009765625f * (1 - x),
                Out = x => FastApproximately(x, 1)
                    ? 1
                    : 1 - FastPow(2, -10 * x) + 0.0009765625f * x,
                InOut = x => x == 0
                    ? 0
                    : FastApproximately(x, 1)
                        ? 1
                        : x < 0.5
                            ? FastPow(2, 20 * x - 10) / 2 - 0.0009765625f * (1 - x)
                            : (2 - FastPow(2, -20 * x + 10)) / 2 + 0.0009765625f * x
            };

            srEases[(int)EaseFunction.Circle] = new Ease
            {
                // PseudoFastSqrt is more visually stable than FastSqrt for this case
                In = x => 1 - PseudoFastSqrt(1 - (x * x)),
                Out = x => PseudoFastSqrt(1 - ((x - 1) * (x - 1))),
                InOut = x => x < 0.5
                    ? (1 - PseudoFastSqrt(1 - ((2 * x) * (2 * x)))) / 2
                    : (PseudoFastSqrt(1 - ((-2 * x + 2) * (-2 * x + 2))) + 1) / 2
            };

            srEases[(int)EaseFunction.Back] = new Ease
            {
                In = x => 2.70158f * x * x * x - _BACK_OVERSHOOT * x * x,
                Out = x => 1 + 2.70158f * ((x - 1) * (x - 1) * (x - 1)) + _BACK_OVERSHOOT * ((x - 1) * (x - 1)),
                InOut = x => x < 0.5f
                    ? ((2 * x) * (2 * x)) * ((_BACK_SCALED_OVERSHOOT + 1) * 2 * x - _BACK_SCALED_OVERSHOOT) / 2
                    : (((2 * x - 2) * (2 * x - 2))* ((_BACK_SCALED_OVERSHOOT + 1) * (x * 2 - 2) + _BACK_SCALED_OVERSHOOT) + 2) / 2
            };

            srEases[(int)EaseFunction.Elastic] = new Ease
            {
                In = x =>
                {
                    if (x == 0) return 0;
                    if (FastApproximately(x, 1)) return 1;

                    return -FastPow(2, 10 * x - 10) * FastSin((x * 10 - _ELASTIC_IN_OFFSET) * _ELASTIC_PERIOD);
                },
                Out = x =>
                {

                    if (x == 0) return 0;

                    if (FastApproximately(x, 1)) return 1;

                    return FastPow(2, -10 * x) * FastSin((x * 10 - _ELASTIC_OUT_OFFSET) * _ELASTIC_PERIOD) + 1;
                },
                InOut = x =>
                {

                    if (x == 0) return 0;
                    if (FastApproximately(x, 1)) return 1;

                    if (x < 0.5) return -(FastPow(2, 20 * x - 10) * FastSin((20 * x - _ELASTIC_PERIOD_IN_OUT_INNER) * _ELASTIC_PERIOD)) / 2;

                    return FastPow(2, -20 * x + 10) * FastSin((20 * x - _ELASTIC_PERIOD_IN_OUT_INNER) * _ELASTIC_PERIOD) / 2 + 1;
                }
            };

            srEases[(int)EaseFunction.Bounce] = new Ease
            {
                In = x => 1 - Get(1 - x, EaseFunction.Bounce, EaseMode.Out),
                Out = x =>
                {

                    if (x < 1 / _BOUNCE_THRESHOLD)
                        return _BOUNCE_CONSTANT * (x * x);


                    if (x < 2 / _BOUNCE_THRESHOLD)
                        return _BOUNCE_CONSTANT * (x -= 1.5f / _BOUNCE_THRESHOLD) * x + 0.75f;

                    if (x < 2.5 / _BOUNCE_THRESHOLD)
                        return _BOUNCE_CONSTANT * (x -= 2.25f / _BOUNCE_THRESHOLD) * x + 0.9375f;

                    return _BOUNCE_CONSTANT * (x -= 2.625f / _BOUNCE_THRESHOLD) * x + 0.984375f;
                },
                InOut = x => x < 0.5
                    ? (1 - Get(1 - 2 * x, EaseFunction.Bounce, EaseMode.Out)) / 2
                    : (1 + Get(2 * x - 1, EaseFunction.Bounce, EaseMode.Out)) / 2
            };
        }
    }


    public interface IEaseDirective
    {
        public float Get(float x);
    }

    [Serializable]
    public struct BasicEaseDirective : IEaseDirective
    {
        public EaseFunction Function;
        public EaseMode     Mode;

        public BasicEaseDirective(EaseFunction function, EaseMode mode)
        {
            Function = function;
            Mode = mode;
        }

        public float Get(float x)
        {
            return Ease.Get(x, Function, Mode);
        }

        public override string ToString()
        {
            if (Function == EaseFunction.Linear) return "Linear";

            return Function + "/" + (Mode == EaseMode.In ? "I" : Mode == EaseMode.Out ? "O" : "IO");
        }
    }

    [Serializable]
    public struct CubicBezierEaseDirective : IEaseDirective
    {
        private const int   _BINARY_SEARCH_MAX_ITERATIONS = 10;
        private const float _BINARY_SEARCH_PRECISION      = 1e-6f;
        private const int   _NEWTON_ITERATIONS            = 4;
        private const float _NEWTON_MIN_SLOPE             = 0.001f;

        public readonly Vector2 Point1;
        public readonly Vector2 Point2;

        private readonly float[] _Samples;

        public CubicBezierEaseDirective(Vector2 point1, Vector2 point2)
        {
            Validate(point1);
            Validate(point2);
            Point1 = point1;
            Point2 = point2;
            _Samples = new float[11];
            UpdateSamples();
        }

        public CubicBezierEaseDirective(float point1X, float point1Y, float point2X, float point2Y)
            : this(new Vector2(point1X, point1Y), new Vector2(point2X, point2Y))
        {
        }

        private static void Validate(Vector2 point)
        {
            if (point.x is < 0 or > 1)
                throw new ArgumentException("X value of control points must be within [0, 1] range");
        }

        private void UpdateSamples()
        {
            float step = 1.0f / (_Samples.Length - 1);
            for (var t = 0; t < _Samples.Length - 1; t++) _Samples[t] = GetBezier(t * step, Point1.x, Point2.x);
        }

        public float Get(float x)
        {
            if (x == 0 || Ease.FastApproximately(x, 1)) return x;

            int nIndex = Array.FindIndex(_Samples, n => n > x);
            nIndex = Math.Max(nIndex, 1);

            float step = 1.0f / (_Samples.Length - 1);
            float t = (nIndex - Mathf.InverseLerp(_Samples[nIndex], _Samples[nIndex - 1], x)) * step;
            float slope = GetBezierSlope(t, Point1.x, Point2.x);

            if (slope >= _NEWTON_MIN_SLOPE)
                t = NewtonSolveApprox(x, t);
            else if (slope != 0) t = BinarySearchApprox(x, nIndex * step, nIndex * step + step);

            float ans = GetBezier(t, Point1.y, Point2.y);

            return ans;
        }

        private float GetBezier(float t, float point1, float point2)
        {
            float cubicCoefficient = 1 - 3 * point2 + 3 * point1;
            float quadraticCoefficient = 3 * point2 - 6 * point1;
            float linearCoefficient = 3 * point1;

            return ((cubicCoefficient * t + quadraticCoefficient) * t + linearCoefficient) * t;
        }

        private float GetBezierSlope(float t, float point1, float point2)
        {
            float cubicCoefficient = 1 - 3 * point2 + 3 * point1;
            float quadraticCoefficient = 3 * point2 - 6 * point1;
            float linearCoefficient = 3 * point1;

            return 3 * cubicCoefficient * t * t + 2 * quadraticCoefficient * t + linearCoefficient;
        }

        private float BinarySearchApprox(float x, float minBound, float maxBound)
        {
            float t, xDiff, i = 0;

            do
            {
                t = (minBound + maxBound) / 2;
                xDiff = GetBezier(t, Point1.x, Point2.x) - x;

                if (xDiff < 0) minBound = t;
                else maxBound = t;
            }
            while (Mathf.Abs(xDiff) < _BINARY_SEARCH_PRECISION && i < _BINARY_SEARCH_MAX_ITERATIONS);

            return t;
        }

        private float NewtonSolveApprox(float targetX, float initialGuess)
        {
            for (var i = 0; i < _NEWTON_ITERATIONS; i++)
            {
                float slope = GetBezierSlope(initialGuess, Point1.x, Point2.x);

                if (slope == 0)
                    return initialGuess;

                float currentX = GetBezier(initialGuess, Point1.x, Point2.x);

                if (Ease.FastApproximately(targetX, currentX))
                    return initialGuess;

                initialGuess -= (currentX - targetX) / slope;
                initialGuess = Mathf.Clamp01(initialGuess);
            }

            return initialGuess;
        }

        public override string ToString()
        {
            return "CubicBézier";
        }
    }
}