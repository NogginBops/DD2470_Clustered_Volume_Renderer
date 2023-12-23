using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace DD2470_Clustered_Volume_Renderer
{
    internal class Frustum
    {
        public System.Numerics.Plane Left, Right, Top, Bottom, Near, Far;

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
    }
}
