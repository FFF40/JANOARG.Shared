using System;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace JANOARG.Shared.Utils
{
    static class FastMath
    {
        public const float PI      = Mathf.PI;
        public const float PI_HALF = PI / 2;
        public const float EPSILON  = 0.000001f;
        
        public static float Sin(float x)
        {
            
            // Wrap angle to [-PI, PI]
            // Note: C# % is remainder, not modulo — floor-based wrap handles negative x correctly
            x -= (2 * PI) * System.MathF.Floor((x + PI) / (2 * PI));
            
            const float B = 4f / PI;
            const float C = -4f / (PI * PI);
                
            float y = B * x + C * x * System.MathF.Abs(x);
                
            // Optional extra precision (at some performance cost)
            const float P = 0.225f;
            y = P * (y * System.MathF.Abs(y) - y) + y;
            
            // Prevent over/undershooting
            y = y > 1 ? 1 : y;
            y = y < -1 ? -1 : y;
                
            return y;
        }
            
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Cos(float x) =>
            // Cos in a nutshell: Sine, just translated back by 90 degrees (but we're using rad so yeah)
            Sin(PI_HALF - x);

        // Fast power approximation for base 2
        public static float Pow2(float p)
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
        public static float Pow(float a, float b)
        { 
            // Domain checks first
            Debug.Assert(a >= 0f, "FastPow(float, float): input must be non-negative.");
            Debug.Assert(!float.IsNaN(a) && !float.IsNaN(b));
            Debug.Assert(!float.IsInfinity(a) && !float.IsInfinity(b));

            // Transform exponent
            float exponent = b * Mathf.Log(a, 2);
            
            // Logarithm shouldn't be costly, I think?
            return Pow2(exponent);
        }
        
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float PseudoSqrt(float x) => Pow(x, 0.5f);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Sqrt(float x, bool precision = false)
        {
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

            // Note: this clamps output to [0, 1], making it incorrect for inputs > 1.
            // This is intentional for easing contexts where values are always normalized,
            // but use PseudoFastSqrt (FastPow(x, 0.5f)) instead if visual stability is
            // a concern, as it is more consistent for Circle easing and similar curves.
            y = y > 1 ? 1 : y;
            y = y < 0 ? 0 : y;
            
            return y; 
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double Sqrt(double x, bool precision = false)
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
        public static bool Approximately(float a, float b)
        {
            // For our easing functions, we typically compare with 1 or 0
            // Using subtraction is faster than Mathf.Abs for this case
            float diff = a - b;
            return diff is < EPSILON and > -EPSILON;
        }
    }
}