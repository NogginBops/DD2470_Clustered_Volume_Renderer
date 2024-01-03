using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Text;
using System.Threading.Tasks;

namespace DD2470_Clustered_Volume_Renderer
{
    internal unsafe struct Frustum
    {
        public System.Numerics.Plane Left, Right, Top, Bottom, Near, Far;

        // Store SIMD optimized.
        public fixed float PlaneX[6];
        public fixed float PlaneY[6];
        public fixed float PlaneZ[6];
        public fixed float PlaneW[6];

        public static Frustum FromCamera(Camera camera)
        {
            Matrix4 vp = camera.Transform.ParentToLocal * camera.ProjectionMatrix;

            // https://fgiesen.wordpress.com/2012/08/31/frustum-planes-from-the-projection-matrix/
            Frustum frustum = new Frustum();
            frustum.Left = new System.Numerics.Plane((vp.Column3 + vp.Column0).ToNumerics());
            frustum.Right = new System.Numerics.Plane((vp.Column3 - vp.Column0).ToNumerics());
            frustum.Bottom = new System.Numerics.Plane((vp.Column3 + vp.Column1).ToNumerics());
            frustum.Top = new System.Numerics.Plane((vp.Column3 - vp.Column1).ToNumerics());
            frustum.Near = new System.Numerics.Plane((vp.Column3 + vp.Column2).ToNumerics());
            frustum.Far = new System.Numerics.Plane((vp.Column3 - vp.Column2).ToNumerics());

            // Calculate this in a more simd way...
            frustum.PlaneX[0] = frustum.Left.Normal.X;
            frustum.PlaneX[1] = frustum.Right.Normal.X;
            frustum.PlaneX[2] = frustum.Bottom.Normal.X;
            frustum.PlaneX[3] = frustum.Top.Normal.X;
            frustum.PlaneX[4] = frustum.Near.Normal.X;
            frustum.PlaneX[5] = frustum.Far.Normal.X;

            frustum.PlaneY[0] = frustum.Left.Normal.Y;
            frustum.PlaneY[1] = frustum.Right.Normal.Y;
            frustum.PlaneY[2] = frustum.Bottom.Normal.Y;
            frustum.PlaneY[3] = frustum.Top.Normal.Y;
            frustum.PlaneY[4] = frustum.Near.Normal.Y;
            frustum.PlaneY[5] = frustum.Far.Normal.Y;

            frustum.PlaneZ[0] = frustum.Left.Normal.Z;
            frustum.PlaneZ[1] = frustum.Right.Normal.Z;
            frustum.PlaneZ[2] = frustum.Bottom.Normal.Z;
            frustum.PlaneZ[3] = frustum.Top.Normal.Z;
            frustum.PlaneZ[4] = frustum.Near.Normal.Z;
            frustum.PlaneZ[5] = frustum.Far.Normal.Z;

            frustum.PlaneW[0] = frustum.Left.D;
            frustum.PlaneW[1] = frustum.Right.D;
            frustum.PlaneW[2] = frustum.Bottom.D;
            frustum.PlaneW[3] = frustum.Top.D;
            frustum.PlaneW[4] = frustum.Near.D;
            frustum.PlaneW[5] = frustum.Far.D;

            return frustum;
        }

        public static bool IntersectsAABB(Frustum frustum, Box3 aabb)
        {
            Span<System.Numerics.Vector4> corners = stackalloc System.Numerics.Vector4[8];
            corners[0] = new System.Numerics.Vector4(aabb.Min.X, aabb.Min.Y, aabb.Min.Z, 1f);
            corners[1] = new System.Numerics.Vector4(aabb.Max.X, aabb.Min.Y, aabb.Min.Z, 1f);
            corners[2] = new System.Numerics.Vector4(aabb.Min.X, aabb.Max.Y, aabb.Min.Z, 1f);
            corners[3] = new System.Numerics.Vector4(aabb.Max.X, aabb.Max.Y, aabb.Min.Z, 1f);
            corners[4] = new System.Numerics.Vector4(aabb.Min.X, aabb.Min.Y, aabb.Max.Z, 1f);
            corners[5] = new System.Numerics.Vector4(aabb.Max.X, aabb.Min.Y, aabb.Max.Z, 1f);
            corners[6] = new System.Numerics.Vector4(aabb.Min.X, aabb.Max.Y, aabb.Max.Z, 1f);
            corners[7] = new System.Numerics.Vector4(aabb.Max.X, aabb.Max.Y, aabb.Max.Z, 1f);

            Span<System.Numerics.Plane> planes = stackalloc System.Numerics.Plane[6];
            planes[0] = frustum.Left;
            planes[1] = frustum.Right;
            planes[2] = frustum.Top;
            planes[3] = frustum.Bottom;
            planes[4] = frustum.Far;
            planes[5] = frustum.Near;

            foreach (System.Numerics.Plane plane in planes)
            {
                bool inside_plane = false;
                foreach (var corner in corners)
                {
                    if (System.Numerics.Plane.Dot(plane, corner) > 0)
                    {
                        inside_plane = true;
                        break;
                    }
                }

                if (inside_plane == false)
                {
                    return false;
                }
            }

            return true;
        }

        public static unsafe bool IntersectsAABBSse41(Frustum frustum, Box3 aabb)
        {
            Vector128<float> Min = new Vector4(aabb.Min, 1.0f).ToVector128();
            Vector128<float> Max = new Vector4(aabb.Max, 1.0f).ToVector128();

            Span<Vector128<float>> corners = stackalloc Vector128<float>[8];
            corners[0] = Min;
            corners[1] = Sse41.Blend(Min, Max, 0b0001);
            corners[2] = Sse41.Blend(Min, Max, 0b0010);
            corners[3] = Sse41.Blend(Min, Max, 0b0011);
            corners[4] = Sse41.Blend(Min, Max, 0b0100);
            corners[5] = Sse41.Blend(Min, Max, 0b0101);
            corners[6] = Sse41.Blend(Min, Max, 0b0110);
            corners[7] = Max;

            Span<Vector128<float>> planes = stackalloc Vector128<float>[6];
            planes[0] = frustum.Left.AsVector128();
            planes[1] = frustum.Right.AsVector128();
            planes[2] = frustum.Top.AsVector128();
            planes[3] = frustum.Bottom.AsVector128();
            planes[4] = frustum.Far.AsVector128();
            planes[5] = frustum.Near.AsVector128();

            foreach (Vector128<float> plane in planes)
            {
                Vector128<float> inside_plane = Vector128<float>.Zero;
                foreach (var corner in corners)
                {
                    Vector128<float> dot = Sse41.DotProduct(plane, corner, 0b1111_1111);
                    inside_plane = Sse.Or(inside_plane, Sse.CompareGreaterThan(dot, Vector128<float>.Zero));
                }
                uint value; Unsafe.SkipInit(out value);
                Sse.StoreScalar((float*)&value, inside_plane);
                if (value == 0)
                {
                    return false;
                }
            }

            return true;
        }

        public static unsafe bool IntersectsAABBAvx(Matrix4 mvp, Box3 aabb)
        {
            Vector128<float> Min = new Vector4(aabb.Min, 1.0f).ToVector128();
            Vector128<float> Max = new Vector4(aabb.Max, 1.0f).ToVector128();

            Vector128<float> xMinMax = Sse.UnpackHigh(Min, Max);
            xMinMax = Sse.Shuffle(xMinMax, xMinMax, 0b00_11_00_11); // x X x X
            Vector128<float> yMinMax = Sse.Shuffle(Min, Max, 0b01_01_01_01); // y y Y Y
            Vector128<float> zMin = Avx.Permute(Min, 0b_11_11_11_11);
            Vector128<float> zMax = Avx.Permute(Min, 0b_11_11_11_11);

            // FIXME: will this generate the code we want?
            // We want to reinterpret xMinMax as m256 and insert itself again in the top...
            Vector256<float> x; Unsafe.SkipInit(out x);
            x = Avx.InsertVector128(x, xMinMax, 0);
            x = Avx.InsertVector128(x, xMinMax, 1);

            Vector256<float> y; Unsafe.SkipInit(out y);
            y = Avx.InsertVector128(y, yMinMax, 0);
            y = Avx.InsertVector128(y, yMinMax, 1);

            Vector256<float> z; Unsafe.SkipInit(out z);
            z = Avx.InsertVector128(z, Min, 0);
            z = Avx.InsertVector128(z, Max, 1);

            Vector256<float> w = Vector256<float>.One;

            Span<Vector256<float>> corners = stackalloc Vector256<float>[4];
            for (int i = 0; i < 4; i++)
            {
                Vector256<float> res = Avx.BroadcastScalarToVector256(&(&mvp.Row0)[i].W);
                res = Fma.MultiplyAdd(Avx.BroadcastScalarToVector256(&(&mvp.Row0)[i].X), x, res);
                res = Fma.MultiplyAdd(Avx.BroadcastScalarToVector256(&(&mvp.Row0)[i].Y), y, res);
                res = Fma.MultiplyAdd(Avx.BroadcastScalarToVector256(&(&mvp.Row0)[i].Z), z, res);
                corners[i] = res;
            }
            Vector256<float> neg_ws = Avx.Subtract(Vector256<float>.Zero, corners[3]);

            Vector256<float> inside =
                Avx.And(
                    Avx.Compare(neg_ws, corners[0], FloatComparisonMode.OrderedLessThanOrEqualNonSignaling),
                    Avx.Compare(corners[0], corners[3], FloatComparisonMode.OrderedLessThanOrEqualNonSignaling)
                );

            inside = Avx.And(
                inside,
                Avx.And(
                    Avx.Compare(neg_ws, corners[1], FloatComparisonMode.OrderedLessThanOrEqualNonSignaling),
                    Avx.Compare(corners[1], corners[3], FloatComparisonMode.OrderedLessThanOrEqualNonSignaling)));

            inside = Avx.And(
                inside,
                Avx.And(
                    Avx.Compare(neg_ws, corners[2], FloatComparisonMode.OrderedLessThanOrEqualNonSignaling),
                    Avx.Compare(corners[2], corners[3], FloatComparisonMode.OrderedLessThanOrEqualNonSignaling)));

            Vector128<float> reduction = Sse.Or(Avx.ExtractVector128(inside, 0), Avx.ExtractVector128(inside, 1));
            reduction = Sse.Or(reduction, Avx.Permute(reduction, 0b10_11_10_11));
            reduction = Sse.Or(reduction, Avx.Permute(reduction, 0b01_01_01_01));

            uint result; Unsafe.SkipInit(out result);
            Sse.StoreScalar((float*)&result, reduction);
            return result != 0;
        }
    }
}
