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
            Matrix4 invVP = Matrix4.Invert(camera.Transform.LocalToParent * camera.ProjectionMatrix);

            // https://arm-software.github.io/opengl-es-sdk-for-android/terrain.html
            Vector3 lbn = Vector3.TransformPerspective((-1, -1, -1), invVP);
            Vector3 ltn = Vector3.TransformPerspective((-1, +1, -1), invVP);
            Vector3 lbf = Vector3.TransformPerspective((-1, -1, +1), invVP);
            Vector3 rbn = Vector3.TransformPerspective((+1, -1, -1), invVP);
            Vector3 rtn = Vector3.TransformPerspective((+1, +1, -1), invVP);
            Vector3 rbf = Vector3.TransformPerspective((+1, -1, +1), invVP);
            Vector3 rtf = Vector3.TransformPerspective((+1, +1, +1), invVP);

            // Check that all the lbn type var are correct, and that the cross products are correct..
            Vector3 left_normal = Vector3.Normalize(Vector3.Cross(lbf - lbn, ltn - lbn));
            Vector3 right_normal = Vector3.Normalize(Vector3.Cross(rtn - rbn, rbf - rbn));
            Vector3 top_normal = Vector3.Normalize(Vector3.Cross(ltn - rtn, rtf - rtn));
            Vector3 bottom_normal = Vector3.Normalize(Vector3.Cross(rbf - rbn, lbn - rbn));
            Vector3 near_normal = Vector3.Normalize(Vector3.Cross(ltn - lbn, rbn - lbn));
            Vector3 far_normal = Vector3.Normalize(Vector3.Cross(rtf - rbf, lbf - rbf));

            var left2 = System.Numerics.Plane.CreateFromVertices(lbn.ToNumerics(), ltn.ToNumerics(), lbf.ToNumerics());
            var left3 = System.Numerics.Plane.CreateFromVertices(ltn.ToNumerics(), lbn.ToNumerics(), lbf.ToNumerics());
            var left = new System.Numerics.Plane(left_normal.AsNumerics(), -Vector3.Dot(left_normal, lbn));

            Frustum frustum = new Frustum();
            frustum.Near = new System.Numerics.Plane(near_normal.AsNumerics(), -Vector3.Dot(near_normal, lbn));
            frustum.Far = new System.Numerics.Plane(far_normal.AsNumerics(), -Vector3.Dot(far_normal, lbf));
            frustum.Left = new System.Numerics.Plane(left_normal.AsNumerics(), -Vector3.Dot(left_normal, lbn));
            frustum.Right = new System.Numerics.Plane(right_normal.AsNumerics(), -Vector3.Dot(right_normal, rbn));
            frustum.Top = new System.Numerics.Plane(top_normal.AsNumerics(), -Vector3.Dot(top_normal, ltn));
            frustum.Bottom = new System.Numerics.Plane(bottom_normal.AsNumerics(), -Vector3.Dot(bottom_normal, lbn));
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
            planes[0] = frustum.Near;
            planes[1] = frustum.Far; // frustum.Far;
            planes[2] = frustum.Top;
            planes[3] = frustum.Bottom;
            planes[4] = frustum.Left;
            planes[5] = frustum.Right;

            planes = [frustum.Left];

            foreach (System.Numerics.Plane plane in planes)
            {
                bool inside_plane = false;
                foreach (var corner in corners)
                {
                    if (System.Numerics.Plane.Dot(plane, corner) > 0)
                    {
                        if (plane == frustum.Near)
                            ;
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
