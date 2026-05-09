using System;
using JANOARG.Shared.Utils;
using JANOARG.Shared.Utils.Animation;
using UnityEngine;

namespace JANOARG.Shared.Data.ChartInfo
{
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

        [SerializeField] private float[] _Samples;

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
            if (x == 0 || FastMath.Approximately(x, 1)) return x;

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
            float t, xDiff;
            int i = 0;

            do
            {
                t = (minBound + maxBound) / 2;
                xDiff = GetBezier(t, Point1.x, Point2.x) - x;

                if (xDiff < 0) minBound = t;
                else maxBound = t;
            }
            while (Mathf.Abs(xDiff) > _BINARY_SEARCH_PRECISION && i++ < _BINARY_SEARCH_MAX_ITERATIONS);

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

                if (FastMath.Approximately(targetX, currentX))
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