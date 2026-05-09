using System;
using JANOARG.Shared.Utils.Animation;
using UnityEngine;

namespace JANOARG.Shared.Utils.Animation
{
    /// <summary>
    /// Helper class for creating physical spring-based animations
    /// </summary>
    public static class SpringEase
    {
        /// <summary>
        ///     Ease a value from two arbitrary values based on perspective parameters of the spring model.
        /// </summary>
        /// <param name="t">Current time, in seconds.</param>
        /// <param name="from">The starting value to animate from.</param>
        /// <param name="to">The target value to animate to.</param>
        /// <param name="bounciness">
        ///     Animation's "bounciness" constant, ranging from (-1,1), which affects the damping of the spring.<br/>
        ///    (-1,0) = over-damped, 0 = critically damped, (0,1) = under-damped
        /// </param>
        /// <param name="perceptiveDuration">
        ///     Animation's perspective duration, in seconds.<br/>
        ///     The actual resting duration will likely be different from this value.
        /// </param>
        /// <param name="initialVelocity">Animation's initial velocity, in units / second.</param>
        /// <returns></returns>
        public static float Get(float t, float from, float to, float bounciness, float perceptiveDuration, float initialVelocity = 0)
        {
            float result = Get(
                t, 
                bounciness,
                perceptiveDuration,
                Mathf.InverseLerp(from, to, initialVelocity)
            );
            return Mathf.Lerp(from, to, result);
        }

        /// <summary>
        ///     Ease a value from 0 to 1 based on perspective parameters of the spring model.
        /// </summary>
        /// <param name="t">Current time, in seconds.</param>
        /// <param name="bounciness">
        ///     Animation's "bounciness" constant, ranging from (-1,1), which affects the damping of the spring.<br/>
        ///     (-1,0) = over-damped, 0 = critically damped, (0,1) = under-damped
        /// </param>
        /// <param name="perceptiveDuration">
        ///     Animation's perspective duration, in seconds.<br/>
        ///     The actual resting duration will likely be different from this value.
        /// </param>
        /// <param name="initialVelocity">Animation's initial velocity, in units / second.</param>
        /// <returns></returns>
        public static float Get(float t, float bounciness, float perceptiveDuration, float initialVelocity = 0)
        {
            return GetByPhysicsModel(
                t, 
                MathF.Pow(2 * Mathf.PI / perceptiveDuration, 2),
                bounciness >= 0
                    ? 4 * Mathf.PI / perceptiveDuration * (1 - bounciness)
                    : 4 * Mathf.PI / perceptiveDuration / (1 + bounciness),
                initialVelocity
            );
        }

        /// <summary>
        ///     Ease a value from 0 to 1 based on physical parameters of the spring model.
        /// </summary>
        /// <param name="t">Current time, in seconds.</param>
        /// <param name="stiffness">Spring's stiffness, in mass constant / second^2.</param>
        /// <param name="damping">Object's damping, in mass constant / second.</param>
        /// <param name="initialVelocity">Object's initial velocity, in units / second.</param>
        /// <returns></returns>
        public static float GetByPhysicsModel(float t, float stiffness, float damping, float initialVelocity = 0)
        {
            const float E = 2.718281828459045f;

            float dampRatio = damping / (FastMath.PseudoSqrt(stiffness) * 2);
            float naturalFreq = FastMath.PseudoSqrt(stiffness);

            if (dampRatio < 1 - float.Epsilon)
            {
                // Under-damped
                float actualFreq = naturalFreq * FastMath.PseudoSqrt(1 - dampRatio * dampRatio);

                float cycleTime = actualFreq * t;
                float naturalFreq_dampRatio_neg = - naturalFreq * dampRatio;

                return FastMath.Pow(E, naturalFreq_dampRatio_neg * t) * (
                    (initialVelocity + naturalFreq_dampRatio_neg) / actualFreq * FastMath.Sin(cycleTime)
                    - FastMath.Cos(cycleTime)
                ) + 1;
            }
            else if (dampRatio > 1 + float.Epsilon)
            {
                // Over-damped
                float genSolCenter = -naturalFreq * dampRatio;
                float genSolDiscr = naturalFreq * FastMath.PseudoSqrt(dampRatio * dampRatio - 1);
                float genSol1 = genSolCenter - genSolDiscr;
                float genSol2 = genSolCenter + genSolDiscr;

                float speed = (initialVelocity + genSol2) / (-2 * genSolDiscr);

                return speed * FastMath.Pow(E, genSol1 * t) 
                    + (-1 - speed) * FastMath.Pow(E, genSol2 * t)
                    + 1;
            }
            else
            {
                // Critically damped
                return ((initialVelocity - naturalFreq) * t - 1) * FastMath.Pow(E, -naturalFreq * t) + 1;
            }
        }
    }
}