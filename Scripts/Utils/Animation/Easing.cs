
using System;
using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;

namespace JANOARG.Shared.Utils.Animation
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
        /// <summary>Eases from <paramref name="from"/> to <paramref name="to"/>. At interpolator=0 returns from, at interpolator=1 returns to.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float LerpTo(float from, float to, float interpolator, EaseFunction easeFunc, EaseMode mode) =>
            from + (to - from) * Ease.Get(interpolator, easeFunc, mode);

        /// <summary>Reverse of LerpTo — eases from <paramref name="to"/> back to <paramref name="from"/>. At interpolator=0 returns <paramref name="to"/>, at interpolator=1 returns <paramref name="from"/>.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float InverseLerpTo(float from, float to, float interpolator, EaseFunction easeFunc, EaseMode mode) =>
            to - (to - from) * Ease.Get(interpolator, easeFunc, mode);

        /// <summary>Eases <paramref name="from"/> by <paramref name="delta"/>. Equivalent to LerpTo with an offset instead of an explicit target.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float LerpBy(float from, float delta, float interpolator, EaseFunction easeFunc, EaseMode mode) =>
            from + delta * Ease.Get(interpolator, easeFunc, mode);

        /// <summary>Reverse of LerpBy — eases from <paramref name="from"/> + <paramref name="delta"/> back to <paramref name="from"/>.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float InverseLerpBy(float from, float delta, float interpolator, EaseFunction easeFunc, EaseMode mode) =>
            from + delta * (1 - Ease.Get(interpolator, easeFunc, mode));

        /// <summary>Eases <paramref name="from"/> toward zero. At interpolator=0 returns <paramref name="from"/>, at interpolator=1 returns 0.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ToZero(float from, float interpolator, EaseFunction easeFunc, EaseMode mode) =>
            from * (1 - Ease.Get(interpolator, easeFunc, mode));

        /// <summary>
        /// Eases from zero toward <paramref name="to"/>. At interpolator=0 returns 0, at interpolator=1 returns <paramref name="to"/>.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float FromZero(float to, float interpolator, EaseFunction easeFunc, EaseMode mode) =>
            to * Ease.Get(interpolator, easeFunc, mode);

        /// <summary>
        /// Returns <paramref name="to"/> divided by the eased interpolator.
        /// Produces a value that starts very large and converges toward <paramref name="to"/> as the ease approaches 1.
        /// Useful for overshoot or dramatic approach effects. Infinity at interpolator=0 (division by zero).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float BlastIn(float to, float interpolator, EaseFunction easeFunc, EaseMode mode) =>
            Ease.Get(interpolator, easeFunc, mode) is var ease && to / ease > float.MaxValue
                ? float.MaxValue
                : to / ease;


        /// <summary>
        /// Returns <paramref name="from"/> divided by (1 - eased interpolator).
        /// Produces a value that starts at <paramref name="from"/> and diverges toward infinity as the ease approaches 1.
        /// Useful for explosive exit or departure effects. Infinity at interpolator=1 (division by zero).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float BlastOut(float from, float interpolator, EaseFunction easeFunc, EaseMode mode) =>
            (1 - Ease.Get(interpolator, easeFunc, mode)) is var ease && from / ease > float.MaxValue
                ? float.MaxValue
                : from / ease;


        // Predefined ease overloads — pass a pre-calculated ease value (e.g. from Ease.Get or a cached result)
        // instead of recomputing it each call. Useful when the same ease value drives multiple properties per frame.

        /// <summary>
        /// Eases from <paramref name="from"/> to <paramref name="to"/> using a pre-calculated <paramref name="ease"/> value.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float LerpTo(float from, float to, float ease) =>
            (1 - ease) * from + ease * to;

        /// <summary>
        /// Reverse of LerpTo — eases from <paramref name="to"/> back to <paramref name="from"/> using a pre-calculated <paramref name="ease"/> value.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float InverseLerpTo(float from, float to, float ease) =>
            ease * from + (1 - ease) * to;

        /// <summary>
        /// Eases from <paramref name="from"/> to <paramref name="from"/> + <paramref name="delta"/> using a pre-calculated <paramref name="ease"/> value.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float LerpBy(float from, float delta, float ease) =>
            (1 - ease) * from + ease * (from + delta);

        /// <summary>
        /// Reverse of LerpBy using a pre-calculated <paramref name="ease"/> value.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float InverseLerpBy(float from, float delta, float ease) =>
            from + delta * (1 - ease);

        /// <summary>
        /// Eases <paramref name="from"/> toward zero using a pre-calculated <paramref name="ease"/> value.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ToZero(float from, float ease) =>
            from * (1 - ease);

        /// <summary>
        /// Eases from zero toward <paramref name="to"/> using a pre-calculated <paramref name="ease"/> value.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float FromZero(float to, float ease) =>
            to * ease;

        /// <summary>
        /// Returns <paramref name="to"/> when <paramref name="ease"/> reaches 1, otherwise returns <paramref name="from"/>. Hard cut with no interpolation.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Snap(float from, float to, float ease) =>
            (int)ease == 1 ? to : from;

        /// <summary>
        /// BlastIn using a pre-calculated <paramref name="ease"/> value. Undefined at ease=0.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float BlastIn(float to, float ease) =>
            (to / ease) is var val && val > float.MaxValue ?
                float.MaxValue :
                val;

        /// <summary>
        /// BlastOut using a pre-calculated <paramref name="ease"/> value. Undefined at ease=1.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float BlastOut(float from, float ease) =>
            (from / (1 - ease)) is var val && val > float.MaxValue ?
                float.MaxValue :
                val;

    }

    public class Ease
    {
        public Func<float, float> In;
        public Func<float, float> Out;
        public Func<float, float> InOut;

        /// <summary>
        /// Skips all currently active Ease.Animate coroutines.
        /// Also cleans up any stale handlers left by external StopCoroutine calls.
        /// </summary>
        public static void SkipAll(bool force = false)
        {
            foreach (var weak in EaseEnumerator.s_active)
                if (weak.TryGetTarget(out var handler))
                    handler.Skip(force);
        }

        [SuppressMessage("ReSharper", "CompareOfFloatsByEqualityOperator")] // We don't care about floating point errors here
        public static float Get(float x, EaseFunction easeFunc, EaseMode mode, float multiplier = 1, float delay = 0, float xPow = 1)
        {
            // Only operate on non-default optional parameters
            x = multiplier != 1f ? x * multiplier   : x;
            x = delay      != 0f ? x - delay        : x;
            x = xPow       != 1f ? FastMath.Pow(x, xPow) : x;
            
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
            FastMath.Pow(Get(x * multiplier - delay, func, mode), p);

        // We don't need DOTween, guys

        /// <summary>
        /// Animates a value from 0 to 1 over specified duration, invoking callback each frame with linear progress.
        /// Use <see cref="EnumAnimate(float, Action{float})"/> instead if you need Skip control or IsComplete tracking.
        /// </summary>
        /// <param name="duration">Total animation time in seconds</param>
        /// <param name="callback">Action receiving linear progress (0 to 1) each frame</param>
        public static IEnumerator Animate(float duration, Action<float> callback)
        {
            for (float a = 0; a < 1; a += Time.deltaTime / duration)
            {
                callback(a);
                yield return null;
            }
            callback(1);
        }

        /// <summary>
        /// Animates with easing, with support for shortcuts to ease parameters for callback.
        /// Use <see cref="EnumAnimate(float, EaseFunction, EaseMode, Action{float, EaseFunction, EaseMode})"/> instead if you need Skip control or IsComplete tracking.
        /// </summary>
        /// <param name="duration">Total animation time in seconds</param>
        /// <param name="easeFunc">Easing function type</param>
        /// <param name="mode">Easing mode (In/Out/InOut)</param>
        /// <param name="callback">Action receiving (progress, easeFunc, mode) each frame</param>
        public static IEnumerator Animate(float duration, EaseFunction easeFunc, EaseMode mode, Action<float, EaseFunction, EaseMode> callback)
        {
            for (float a = 0; a < 1; a += Time.deltaTime / duration)
            {
                callback(a, easeFunc, mode);
                yield return null;
            }
            callback(1, easeFunc, mode);
        }

        /// <summary>
        /// Animates with easing, automatically calculating eased value and providing all parameters.
        /// Most comprehensive version - gives access to raw progress, ease parameters shortcuts, and pre-calculated eased value.
        /// Use <see cref="EnumAnimate(float, EaseFunction, EaseMode, Action{float, EaseFunction, EaseMode, float})"/> instead if you need Skip control or IsComplete tracking.
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
                ease = Get(a, easeFunc, mode);
                callback(a, easeFunc, mode, ease);
                yield return null;
            }
            ease = Get(1, easeFunc, mode);
            callback(1, easeFunc, mode, ease);
        }

        /// <summary>
        /// Animates with easing, automatically calculating, and providing only the eased value to callback.
        /// Simplest eased animation - callback receives only the pre-calculated eased progress (0 to 1).
        /// Use <see cref="EnumAnimate(float, EaseFunction, EaseMode, Action{float})"/> instead if you need Skip control or IsComplete tracking.
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
                ease = Get(a, easeFunc, mode);
                callback(ease);
                yield return null;
            }
            ease = Get(1, easeFunc, mode);
            callback(ease);
        }

        // EnumAnimate — opt-in EaseEnumerator variants with per-animation Skip control and IsComplete tracking.
        // Use these when you need to Skip an individual animation or check completion state.
        // Carries a small overhead per call vs plain Animate (extra allocation + MoveNext wrapper).

        /// <summary>
        /// Animate variant returning an <see cref="EaseEnumerator"/> for per-animation Skip control and IsComplete tracking.
        /// </summary>
        /// <param name="duration">Total animation time in seconds</param>
        /// <param name="callback">Action receiving linear progress (0 to 1) each frame</param>
        /// <returns>An EaseEnumerator that can be passed to StartCoroutine and used to Skip individually.</returns>
        public static EaseEnumerator EnumAnimate(float duration, Action<float> callback) =>
            new EaseEnumerator(EnumAnimateInner(duration, callback));

        private static IEnumerator EnumAnimateInner(float duration, Action<float> callback)
        {
            var handler = EaseEnumerator.Current;
            for (float a = 0; a < 1; a += Time.deltaTime / duration)
            {
                if (handler.CancelRequested) { handler.CancelRequested = false; break; }
                callback(a);
                yield return null;
            }
            if (handler.ForceCancelRequested) { handler.ForceCancelRequested = false; handler.MarkComplete(); yield break; }
            callback(1);
            handler.MarkComplete();
        }

        /// <summary>
        /// Animate variant returning an <see cref="EaseEnumerator"/> for per-animation Skip control and IsComplete tracking.
        /// </summary>
        /// <param name="duration">Total animation time in seconds</param>
        /// <param name="easeFunc">Easing function type</param>
        /// <param name="mode">Easing mode (In/Out/InOut)</param>
        /// <param name="callback">Action receiving (progress, easeFunc, mode) each frame</param>
        /// <returns>An EaseEnumerator that can be passed to StartCoroutine and used to Skip individually.</returns>
        public static EaseEnumerator EnumAnimate(float duration, EaseFunction easeFunc, EaseMode mode, Action<float, EaseFunction, EaseMode> callback) =>
            new EaseEnumerator(EnumAnimateInner(duration, easeFunc, mode, callback));

        private static IEnumerator EnumAnimateInner(float duration, EaseFunction easeFunc, EaseMode mode, Action<float, EaseFunction, EaseMode> callback)
        {
            var handler = EaseEnumerator.Current;
            for (float a = 0; a < 1; a += Time.deltaTime / duration)
            {
                if (handler.CancelRequested) { handler.CancelRequested = false; break; }
                callback(a, easeFunc, mode);
                yield return null;
            }
            if (handler.ForceCancelRequested) { handler.ForceCancelRequested = false; handler.MarkComplete(); yield break; }
            callback(1, easeFunc, mode);
            handler.MarkComplete();
        }

        /// <summary>
        /// Animate variant returning an <see cref="EaseEnumerator"/> for per-animation Skip control and IsComplete tracking.
        /// </summary>
        /// <param name="duration">Total animation time in seconds</param>
        /// <param name="easeFunc">Easing function type</param>
        /// <param name="mode">Easing mode (In/Out/InOut)</param>
        /// <param name="callback">Action receiving (progress, easeFunc, mode, easedValue) each frame</param>
        /// <returns>An EaseEnumerator that can be passed to StartCoroutine and used to Skip individually.</returns>
        public static EaseEnumerator EnumAnimate(float duration, EaseFunction easeFunc, EaseMode mode, Action<float, EaseFunction, EaseMode, float> callback) =>
            new EaseEnumerator(EnumAnimateInner(duration, easeFunc, mode, callback));

        private static IEnumerator EnumAnimateInner(float duration, EaseFunction easeFunc, EaseMode mode, Action<float, EaseFunction, EaseMode, float> callback)
        {
            var handler = EaseEnumerator.Current;
            float ease;
            for (float a = 0; a < 1; a += Time.deltaTime / duration)
            {
                if (handler.CancelRequested) { handler.CancelRequested = false; break; }
                ease = Get(a, easeFunc, mode);
                callback(a, easeFunc, mode, ease);
                yield return null;
            }
            if (handler.ForceCancelRequested) { handler.ForceCancelRequested = false; handler.MarkComplete(); yield break; }
            ease = Get(1, easeFunc, mode);
            callback(1, easeFunc, mode, ease);
            handler.MarkComplete();
        }

        /// <summary>
        /// Animate variant returning an <see cref="EaseEnumerator"/> for per-animation Skip control and IsComplete tracking.
        /// </summary>
        /// <param name="duration">Total animation time in seconds</param>
        /// <param name="easeFunc">Easing function type</param>
        /// <param name="mode">Easing mode (In/Out/InOut)</param>
        /// <param name="callback">Action receiving eased progress value (0 to 1) each frame</param>
        /// <returns>An EaseEnumerator that can be passed to StartCoroutine and used to Skip individually.</returns>
        public static EaseEnumerator EnumAnimate(float duration, EaseFunction easeFunc, EaseMode mode, Action<float> callback) =>
            new EaseEnumerator(EnumAnimateInner(duration, easeFunc, mode, callback));

        private static IEnumerator EnumAnimateInner(float duration, EaseFunction easeFunc, EaseMode mode, Action<float> callback)
        {
            var handler = EaseEnumerator.Current;
            float ease;
            for (float a = 0; a < 1; a += Time.deltaTime / duration)
            {
                if (handler.CancelRequested) { handler.CancelRequested = false; break; }
                ease = Get(a, easeFunc, mode);
                callback(ease);
                yield return null;
            }
            if (handler.ForceCancelRequested) { handler.ForceCancelRequested = false; handler.MarkComplete(); yield break; }
            ease = Get(1, easeFunc, mode);
            callback(ease);
            handler.MarkComplete();
        }
        
        // Task Async alternative
        // Note: This one tries to avoid Unity dependencies, so DeltaTime is not used
        // This may not play well with Unity threads so use this with caution.
        // It is recommended to replace DateTime with your own available clock implementations

        #region TASK BASED ANIMATOR
        private static bool s_taskCancelRequested;
        private static bool s_taskForceCancelRequested;

        /// <summary>
        /// Cancels all active Task-based Ease.Animate calls.
        /// Non-force: snaps to callback(1). Force: aborts without callback(1).
        /// </summary>
        public static void SkipAsync(bool force = false)
        {
            s_taskCancelRequested = true;
            if (force) s_taskForceCancelRequested = true;
        }

        /// <summary>
        /// Animates a value from 0 to 1 over specified duration, invoking callback each frame with linear progress.
        /// Supports cancellation via Ease.SkipAsync
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
                if (s_taskCancelRequested || cancellationToken.IsCancellationRequested)
                {
                    s_taskCancelRequested = false;
                    return;
                }

                var elapsed = (float)(DateTime.Now - startTime).TotalSeconds;
                var progress = Math.Min(elapsed / duration, 1f);

                callback(progress);

                await Task.Yield();
            }

            if (s_taskForceCancelRequested || cancellationToken.IsCancellationRequested)
            {
                s_taskForceCancelRequested = false;
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
                if (s_taskCancelRequested || cancellationToken.IsCancellationRequested)
                {
                    s_taskCancelRequested = false;
                    return;
                }

                var elapsed = (float)(DateTime.Now - startTime).TotalSeconds;
                var progress = Math.Min(elapsed / duration, 1f);
                var ease = Ease.Get(progress, easeFunc, mode);

                callback(ease);

                await Task.Yield();
            }

            if (s_taskForceCancelRequested || cancellationToken.IsCancellationRequested)
            {
                s_taskForceCancelRequested = false;
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
                if (s_taskCancelRequested || cancellationToken.IsCancellationRequested)
                {
                    s_taskCancelRequested = false;
                    return;
                }

                var elapsed = (float)(DateTime.Now - startTime).TotalSeconds;
                var progress = Math.Min(elapsed / duration, 1f);

                callback(progress, easeFunc, mode);

                await Task.Yield();
            }

            if (s_taskForceCancelRequested || cancellationToken.IsCancellationRequested)
            {
                s_taskForceCancelRequested = false;
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
                if (s_taskCancelRequested || cancellationToken.IsCancellationRequested)
                {
                    s_taskCancelRequested = false;
                    return;
                }

                var elapsed = (float)(DateTime.Now - startTime).TotalSeconds;
                var progress = Math.Min(elapsed / duration, 1f);
                var ease = Ease.Get(progress, easeFunc, mode);

                callback(progress, easeFunc, mode, ease);

                await Task.Yield();
            }

            if (s_taskForceCancelRequested || cancellationToken.IsCancellationRequested)
            {
                s_taskForceCancelRequested = false;
                return;
            }

            var finalEase = Ease.Get(1, easeFunc, mode);
            callback(1, easeFunc, mode, finalEase);
        }

        #endregion
        

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
        
        private const float _BACK_OVERSHOOT        = 1.70158f;
        private const float _BACK_SCALED_OVERSHOOT = _BACK_OVERSHOOT * 1.525f;

        private const float _ELASTIC_PERIOD_IN_OUT_INNER = 11.125f;

        private const float _ELASTIC_PERIOD     = Mathf.PI * 2 / 3f;
        private const float _ELASTIC_IN_OFFSET  = 10.75f;
        private const float _ELASTIC_OUT_OFFSET = 0.75f;

        private const float _BOUNCE_CONSTANT  = 7.5625f;
        private const float _BOUNCE_THRESHOLD = 2.75f;
        
        private static readonly Ease[] srEases;

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
                In = x => 1 - FastMath.Cos(x * FastMath.PI_HALF),
                Out = x => FastMath.Sin(x * FastMath.PI_HALF),
                InOut = x => (1 - FastMath.Cos(x * FastMath.PI)) / 2
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
                    : FastMath.Pow2(10 * x - 10) - 0.0009765625f * (1 - x),
                Out = x => FastMath.Approximately(x, 1)
                    ? 1
                    : 1 - FastMath.Pow2(-10 * x) + 0.0009765625f * x,
                InOut = x => x == 0
                    ? 0
                    : FastMath.Approximately(x, 1)
                        ? 1
                        : x < 0.5
                            ? FastMath.Pow2(20 * x - 10) / 2 - 0.0009765625f * (1 - x)
                            : (2 - FastMath.Pow2(-20 * x + 10)) / 2 + 0.0009765625f * x
            };

            srEases[(int)EaseFunction.Circle] = new Ease
            {
                // PseudoFastSqrt is more visually stable than FastSqrt for this case
                In = x => 1 - FastMath.PseudoSqrt(1 - (x * x)),
                Out = x => FastMath.PseudoSqrt(1 - ((x - 1) * (x - 1))),
                InOut = x => x < 0.5
                    ? (1 - FastMath.PseudoSqrt(1 - ((2 * x) * (2 * x)))) / 2
                    : (FastMath.PseudoSqrt(1 - ((-2 * x + 2) * (-2 * x + 2))) + 1) / 2
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
                    if (FastMath.Approximately(x, 1)) return 1;

                    return -FastMath.Pow2(10 * x - 10) * FastMath.Sin((x * 10 - _ELASTIC_IN_OFFSET) * _ELASTIC_PERIOD);
                },
                Out = x =>
                {

                    if (x == 0) return 0;

                    if (FastMath.Approximately(x, 1)) return 1;

                    return FastMath.Pow2(-10 * x) * FastMath.Sin((x * 10 - _ELASTIC_OUT_OFFSET) * _ELASTIC_PERIOD) + 1;
                },
                InOut = x =>
                {

                    if (x == 0) return 0;
                    if (FastMath.Approximately(x, 1)) return 1;

                    if (x < 0.5) return -(FastMath.Pow2(20 * x - 10) * FastMath.Sin((20 * x - _ELASTIC_PERIOD_IN_OUT_INNER) * _ELASTIC_PERIOD)) / 2;

                    return FastMath.Pow2(-20 * x + 10) * FastMath.Sin((20 * x - _ELASTIC_PERIOD_IN_OUT_INNER) * _ELASTIC_PERIOD) / 2 + 1;
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
}