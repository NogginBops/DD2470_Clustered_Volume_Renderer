using OpenTK.Mathematics;

namespace DD2470_Clustered_Volume_Renderer
{
    internal class Transform
    {

        public Quaternion LocalRotation;
        public Vector3 LocalPosition;
        public Vector3 LocalScale;

        public Matrix4 LocalToParent
        {
            get
            {
                Matrix4 matrix;
                Matrix3.CreateFromQuaternion(LocalRotation, out Matrix3 rotation);

                matrix.Row0 = new Vector4(rotation.Row0 * LocalScale.X, 0);
                matrix.Row1 = new Vector4(rotation.Row1 * LocalScale.Y, 0);
                matrix.Row2 = new Vector4(rotation.Row2 * LocalScale.Z, 0);
                matrix.Row3 = new Vector4(LocalPosition, 1);

                return matrix;
            }
        }

        public Matrix4 ParentToLocal => LocalToParent.Inverted();

        public Vector3 Forward => Vector3.TransformVector(-Vector3.UnitZ, LocalToParent /*FIXME: This should be LocalToWorld and not local to parent!!!*/);
        public Vector3 Right => Vector3.TransformVector(Vector3.UnitX, LocalToParent /*FIXME: This should be LocalToWorld and not local to parent!!!*/);
        public Vector3 Up => Vector3.TransformVector(Vector3.UnitY, LocalToParent /*FIXME: This should be LocalToWorld and not local to parent!!!*/);

        // FIXME: Parent and child relations...

        public Transform()
        {
            LocalRotation = Quaternion.Identity;
            LocalPosition = Vector3.Zero;
            LocalScale = Vector3.One;
        }

        public Transform(Quaternion rotation, Vector3 position, Vector3 scale)
        {
            LocalRotation = rotation;
            LocalPosition = position;
            LocalScale = scale;
        }

        public override string ToString()
        {
            return $"T: {LocalPosition}, R: ({LocalRotation}), S: {LocalScale}";
        }
    }
}
