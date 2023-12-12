using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DD2470_Clustered_Volume_Renderer
{
    internal class Camera
    {
        public Transform Transform = new Transform();

        public Color4 ClearColor = Color4.Black;

        public float VerticalFov;
        // FIXME: What should we do with aspect ratio?
        public float AspectRatio;
        public float NearPlane, FarPlane;

        public float XAxisRotation, YAxisRotation;

        public Matrix4 ProjectionMatrix => Matrix4.CreatePerspectiveFieldOfView(VerticalFov * Util.D2R, AspectRatio, NearPlane, FarPlane);

        public Camera(float verticalFov, float aspectRatio, float nearPlane, float farPlane)
        {
            VerticalFov = verticalFov;
            AspectRatio = aspectRatio;
            NearPlane = nearPlane;
            FarPlane = farPlane;
        }

        public static float EditorCameraSpeed = 100;
        public static float MouseSpeedX = 0.3f;
        public static float MouseSpeedY = 0.3f;
        public static float CameraMinY = -80f;
        public static float CameraMaxY = 80f;
        public static void UpdateEditorCamera(Camera camera, KeyboardState keyboard, MouseState mouse, float deltaTime)
        {
            UpdateCameraMovement(camera, keyboard, deltaTime);
            UpdateCameraDirection(camera, mouse, deltaTime);

            static void UpdateCameraMovement(Camera camera, KeyboardState keyboard, float deltaTime)
            {
                Vector3 direction = Vector3.Zero;

                if (keyboard.IsKeyDown(Keys.W))
                {
                    direction += camera.Transform.Forward;
                }

                if (keyboard.IsKeyDown(Keys.S))
                {
                    direction += -camera.Transform.Forward;
                }

                if (keyboard.IsKeyDown(Keys.A))
                {
                    direction += -camera.Transform.Right;
                }

                if (keyboard.IsKeyDown(Keys.D))
                {
                    direction += camera.Transform.Right;
                }

                if (keyboard.IsKeyDown(Keys.Space))
                {
                    direction += Vector3.UnitY;
                }

                if (keyboard.IsKeyDown(Keys.LeftShift) |
                    keyboard.IsKeyDown(Keys.RightShift))
                {
                    direction += -Vector3.UnitY;
                }

                float speed = EditorCameraSpeed;
                if (keyboard.IsKeyDown(Keys.LeftControl))
                {
                    speed /= 4f;
                }

                camera.Transform.LocalPosition += direction * speed * deltaTime;
            }

            static void UpdateCameraDirection(Camera camera, MouseState mouse, float deltaTime)
            {
                // FIXME: Some way to detect trackpad...
                //if (mouse.IsButtonDown(MouseButton.Right))
                if (mouse.IsButtonDown(MouseButton.Left))
                {
                    var delta = mouse.Delta;

                    camera.YAxisRotation += -delta.X * MouseSpeedX * deltaTime;
                    camera.XAxisRotation += -delta.Y * MouseSpeedY * deltaTime;
                    camera.XAxisRotation = MathHelper.Clamp(camera.XAxisRotation, CameraMinY * Util.D2R, CameraMaxY * Util.D2R);

                    camera.Transform.LocalRotation =
                        Quaternion.FromAxisAngle(Vector3.UnitY, camera.YAxisRotation) *
                        Quaternion.FromAxisAngle(Vector3.UnitX, camera.XAxisRotation);
                }
            }
        }
    }
}
