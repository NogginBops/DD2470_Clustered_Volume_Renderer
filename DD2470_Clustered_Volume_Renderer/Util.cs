using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace DD2470_Clustered_Volume_Renderer
{
    internal static class Util
    {
        public const float D2R = MathF.PI / 180f;
        public const float R2D = 180f / MathF.PI;

        [Pure]
        public static unsafe ulong GetAvailableStack()
        {
            int a = 0;
            void* aref = Unsafe.AsPointer(ref a);
            GetCurrentThreadStackLimits(out nuint lowLimit, out nuint highLimit);

            var remaining = (nuint)aref - lowLimit;

            return remaining;

            [DllImport("kernel32.dll")]
            static extern void GetCurrentThreadStackLimits(out nuint lowLimit, out nuint highLimit);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public static System.Numerics.Vector3 ToNumerics(this Vector3 vec3) =>
          Unsafe.As<Vector3, System.Numerics.Vector3>(ref vec3);

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public static System.Numerics.Vector4 ToNumerics(this Vector4 vec4) =>
          Unsafe.As<Vector4, System.Numerics.Vector4>(ref vec4);

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public static ref System.Numerics.Vector3 AsNumerics(ref this Vector3 vec3) =>
            ref Unsafe.As<Vector3, System.Numerics.Vector3>(ref vec3);

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public static ref Vector3 AsOpenTK(ref this System.Numerics.Vector3 vec3) =>
            ref Unsafe.As<System.Numerics.Vector3, Vector3>(ref vec3);

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public static ref System.Numerics.Vector4 AsNumerics(ref this Vector4 vec4) =>
            ref Unsafe.As<Vector4, System.Numerics.Vector4>(ref vec4);

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public static System.Runtime.Intrinsics.Vector128<float> ToVector128(this Vector3 vec3)
        {
            Vector4 vec4 = new Vector4(vec3);
            return Unsafe.As<Vector4, System.Runtime.Intrinsics.Vector128<float>>(ref vec4);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public static ref Vector3 AsVector3(ref this System.Runtime.Intrinsics.Vector128<float> vec128)
        {
            return ref Unsafe.As<System.Runtime.Intrinsics.Vector128<float>, Vector3>(ref vec128);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public static Vector3 ToVector3(this System.Runtime.Intrinsics.Vector128<float> vec128)
        {
            return Unsafe.As<System.Runtime.Intrinsics.Vector128<float>, Vector3>(ref vec128);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public static ref System.Runtime.Intrinsics.Vector128<float> AsVector128(ref this Vector4 vec4) =>
            ref Unsafe.As<Vector4, System.Runtime.Intrinsics.Vector128<float>>(ref vec4);

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public static System.Runtime.Intrinsics.Vector128<float> ToVector128(this Vector4 vec4) =>
            Unsafe.As<Vector4, System.Runtime.Intrinsics.Vector128<float>>(ref vec4);

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public static ref System.Runtime.Intrinsics.Vector128<float> AsVector128(ref this System.Numerics.Plane plane) =>
            ref Unsafe.As<System.Numerics.Plane, System.Runtime.Intrinsics.Vector128<float>>(ref plane);

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public static ref Vector4 AsOpenTK(ref this System.Numerics.Vector4 vec4) =>
            ref Unsafe.As<System.Numerics.Vector4, Vector4>(ref vec4);

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public static ref System.Numerics.Quaternion AsNumerics(ref this Quaternion quat) =>
            ref Unsafe.As<Quaternion, System.Numerics.Quaternion>(ref quat);

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public static ref Quaternion AsOpenTK(ref this System.Numerics.Quaternion quat) =>
            ref Unsafe.As<System.Numerics.Quaternion, Quaternion>(ref quat);

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public static ref System.Numerics.Vector4 AsNumerics4(ref this Color4 col) =>
            ref Unsafe.As<Color4, System.Numerics.Vector4>(ref col);

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public static ref System.Numerics.Vector3 AsNumerics3(ref this Color4 col) =>
            ref Unsafe.As<Color4, System.Numerics.Vector3>(ref col);

        public static Vector3 NextVector3(this Random random, Vector3 min, Vector3 max)
        {
            Vector3 vec;
            vec.X = random.NextSingle();
            vec.Y = random.NextSingle();
            vec.Z = random.NextSingle();
            return Vector3.Lerp(min, max, vec);
        }

        public static Color4 NextColor4Hue(this Random random, float saturation, float value)
        {
            float hue = random.NextSingle();
            return Color4.FromHsv(new Vector4(hue, saturation, value, 1.0f));
        }
    }
}
