
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace JANOARG.Shared.Utils.Animation
{
    /// <summary>
    /// Wraps an Ease.Animate coroutine, providing per-animation Skip control and completion tracking. <br/>
    /// Compatible with all IEnumerator usage patterns: <br/>
    ///   yield return Ease.Animate(...)        — non-breaking <br/>
    ///   StartCoroutine(Ease.Animate(...))     — non-breaking <br/>
    ///   var anim = Ease.Animate(...); <br/>
    ///   StartCoroutine(anim); anim.Skip();   — new: individual skip <br/>
    ///
    /// </summary>
    ///
    /// <remark>
    /// If Unity's StopCoroutine is called externally, IsComplete will not update automatically. <br/>
    /// Call anim.Complete() manually after StopCoroutine to keep state consistent.
    /// </remark>
    public class EaseEnumerator : IEnumerator, IEnumerable
    {
        internal static readonly List<WeakReference<EaseEnumerator>> s_active = new();

        public IEnumerator GetEnumerator() => this;
        
        // ThreadStatic so AnimateInner can retrieve its own handler during construction
        [ThreadStatic]
        internal static EaseEnumerator Current;

        private readonly IEnumerator _inner;

        internal bool CancelRequested;
        internal bool ForceCancelRequested;

        /// <summary>True once the animation has naturally completed or Complete() was called.</summary>
        public bool IsComplete { get; private set; }

        object IEnumerator.Current => _inner.Current;

        internal EaseEnumerator(IEnumerator inner)
        {
            _inner = inner;
            s_active.Add(new WeakReference<EaseEnumerator>(this));
        }

        /// <summary>
        /// Snaps the animation to its end state (calls callback(1)).
        /// Use Skip(force: true) to abort without calling callback(1).
        /// </summary>
        /// <returns>true if succeeded, false if fails where IsComplete is true, throws if type is null</returns>
        /// <exception cref="NullReferenceException"> When target is null</exception>
        public bool Skip(bool force = false)
        {
            if (CancelRequested)
            {
                Debug.LogWarning("Duplicate call to EaseEnumerator.Skip(), ignoring. Refer to stack trace.");
                return false;
            }
            
            if (IsComplete)
                return false;

            CancelRequested = true;
            ForceCancelRequested = force;

            return true;
        }

        /// <summary>
        /// Marks the animation as complete and removes it from the active list.
        /// Call this manually after StopCoroutine to keep IsComplete and SkipAll consistent.
        /// </summary>
        public void Complete() => MarkComplete();

        internal void MarkComplete()
        {
            IsComplete = true;
            s_active.RemoveAll(w => !w.TryGetTarget(out var t) || t == this);
        }

        public bool MoveNext()
        {
            // Set Current so AnimateInner can retrieve its handler via EaseEnumerator.Current
            Current = this;
            bool hasNext = _inner.MoveNext();
            Current = null;
            if (!hasNext) MarkComplete();
            return hasNext;
        }

        public void Reset() => _inner.Reset();
    }
}