using OpenTK.Audio.OpenAL;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace DD2470_Clustered_Volume_Renderer
{
    internal struct PointLight
    {
        public Vector3 Position;
        public float InverseSquareRadius;
        public Vector3 Color;
        public float Padding0;

        public PointLight(Vector3 position, float radius, Color4 color, float intensity)
        {
            Position = position;
            InverseSquareRadius = 1 / (radius * radius);
            Color = new Vector3(color.R, color.G, color.B) * intensity;
            Padding0 = 0;
        }
    }

    internal class Window : GameWindow
    {
        public Window(GameWindowSettings gameWindowSettings, NativeWindowSettings nativeWindowSettings) : base(gameWindowSettings, nativeWindowSettings)
        {
        }

        public int VAO;

        // FIXME: Handle framebuffer resize!
        public Framebuffer HDRFramebuffer;

        public Texture DefaultAlbedo;
        public Texture DefaultNormal;

        public Material DefaultMaterial;

        public Material Tonemap;

        public Camera Camera;
        public List<Entity> Entities;

        public Buffer LightBuffer;
        public List<PointLight> Lights = new List<PointLight>();

        protected override void OnLoad()
        {
            base.OnLoad();

            // FIXME: change this to FramebufferSize when we get OpenTK 4.8.2
            HDRFramebuffer = Framebuffer.CreateHDRFramebuffer("HDR Framebuffer", ClientSize.X, ClientSize.Y);

            Shader defaultShader = Shader.CreateVertexFragment("Default Shader", "./Shaders/default.vert", "./Shaders/default.frag");
            DefaultMaterial = new Material(defaultShader);

            // FIXME: Make the tonemapping more consistent?
            Shader tonemapShader = Shader.CreateVertexFragment("Tonemap Shader", "./Shaders/fullscreen.vert", "./Shaders/tonemap.frag");
            Tonemap = new Material(tonemapShader);

            DefaultAlbedo = Texture.FromColor(Color4.White, true);
            DefaultNormal = Texture.FromColor(new Color4(0.5f, 0.5f, 1f, 1f), false);

            Camera = new Camera(90, Size.X / (float)Size.Y, 0.1f, 10000f);

            Entities = Model.LoadModel("./Sponza/sponza.obj", defaultShader);

            // Octahedron mapped point light shadows put into a atlas?

            const int NLights = 50;
            Random rand = new Random();
            Vector3 min = new Vector3(-300, 0, -100);
            Vector3 max = new Vector3(300, 200, 100);
            for (int i = 0; i < NLights; i++)
            {
                Lights.Add(
                    new PointLight(
                        rand.NextVector3(min, max),
                        rand.NextSingle() * 10 + 0.1f,
                        rand.NextColor4Hue(1, 1),
                        rand.NextSingle() * 1000 + 0.1f));
            }
            LightBuffer = Buffer.CreateBuffer("Point Light buffer", Lights, BufferStorageFlags.None);

            // FIXME: Make a VAO for each mesh?
            GL.CreateVertexArrays(1, out VAO);

            // Separate position buffer.
            GL.VertexArrayAttribBinding(VAO, 0, 0);
            GL.VertexArrayAttribFormat(VAO, 0, 3, VertexAttribType.Float, false, 0);
            GL.EnableVertexArrayAttrib(VAO, 0);
            // Other vertex attributes.
            GL.VertexArrayAttribBinding(VAO, 1, 1);
            GL.VertexArrayAttribBinding(VAO, 2, 1);
            GL.VertexArrayAttribBinding(VAO, 3, 1);
            GL.VertexArrayAttribFormat(VAO, 1, 3, VertexAttribType.Float, false, 0);
            GL.EnableVertexArrayAttrib(VAO, 1);
            GL.VertexArrayAttribFormat(VAO, 2, 3, VertexAttribType.Float, false, sizeof(float) * 3);
            GL.EnableVertexArrayAttrib(VAO, 2);
            GL.VertexArrayAttribFormat(VAO, 3, 2, VertexAttribType.Float, false, sizeof(float) * 6);
            GL.EnableVertexArrayAttrib(VAO, 3);

            GL.BindVertexArray(VAO);

            GL.Enable(EnableCap.DepthTest);
            GL.DepthFunc(DepthFunction.Lequal);
            GL.DepthMask(true);
        }

        protected override void OnResize(ResizeEventArgs e)
        {
            base.OnResize(e);

            GL.Viewport(0, 0, e.Width, e.Height);

            GL.DeleteFramebuffer(HDRFramebuffer.Handle);
            GL.DeleteTexture(HDRFramebuffer.ColorAttachment0.Handle);
            GL.DeleteTexture(HDRFramebuffer.DepthStencilAttachment.Handle);
            HDRFramebuffer = Framebuffer.CreateHDRFramebuffer("HDR Framebuffer", e.Width, e.Height);

            Camera.AspectRatio = e.Width / (float)e.Height;
        }

        protected override void OnUpdateFrame(FrameEventArgs args)
        {
            base.OnUpdateFrame(args);

            float deltaTime = (float)args.Time;

            Camera.UpdateEditorCamera(Camera, KeyboardState, MouseState, deltaTime);

            if (KeyboardState.IsKeyPressed(Keys.Escape))
            {
                Close();
            }

            if (KeyboardState.IsKeyPressed(Keys.F11))
            {
                if (WindowState == WindowState.Fullscreen)
                {
                    WindowState = WindowState.Normal;
                }
                else
                {
                    WindowState = WindowState.Fullscreen;
                }
            }
        }

        protected unsafe override void OnRenderFrame(FrameEventArgs args)
        {
            base.OnRenderFrame(args);

            GL.ClearColor(Color4.Black);
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit | ClearBufferMask.StencilBufferBit);

            // FIXME: Depth prepass?

            GL.BindFramebuffer(FramebufferTarget.Framebuffer, HDRFramebuffer.Handle);

            GL.ClearColor(Camera.ClearColor);
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit | ClearBufferMask.StencilBufferBit);

            Shader.UseShader(DefaultMaterial.Shader);

            // FIXME: Loop through all entities and draw them.
            for (int i = 0; i < Entities.Count; i++)
            {
                Entity entity = Entities[i];

                // If there is nothing to render, skip it.
                if (entity.Mesh == null)
                    continue;

                Shader.UseShader(entity.Mesh.Material.Shader);
                // FIXME: Default albedo and normal textures.
                GL.BindTextureUnit(0, entity.Mesh.Material.Albedo?.Handle ?? DefaultAlbedo.Handle);
                GL.BindTextureUnit(1, entity.Mesh.Material.Normal?.Handle ?? DefaultNormal.Handle);

                // Bind the light buffer handle
                GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 0, LightBuffer.Handle);

                // FIXME: Make calculating these more efficient?
                Matrix4 modelMatrix = GetLocalToWorldTransform(entity);
                Matrix4 viewMatrix = Camera.Transform.ParentToLocal;
                Matrix4 projectionMatrix = Camera.ProjectionMatrix;

                Matrix4 vp = viewMatrix * projectionMatrix;
                Matrix4 mvp = modelMatrix * vp;

                Matrix3 normalMatrix = Matrix3.Transpose(new Matrix3(modelMatrix).Inverted());

                GL.UniformMatrix4(0, true, ref mvp);
                GL.UniformMatrix4(1, true, ref modelMatrix);
                GL.UniformMatrix3(2, true, ref normalMatrix);

                // FIXME: We assume the camera transform has no parent.
                GL.Uniform3(10, Camera.Transform.LocalPosition);

                GL.VertexArrayVertexBuffer(VAO, 0, entity.Mesh.PositionBuffer.Handle, 0, sizeof(Vector3));
                GL.VertexArrayVertexBuffer(VAO, 1, entity.Mesh.AttributeBuffer.Handle, 0, sizeof(VertexAttributes));
                GL.VertexArrayElementBuffer(VAO, entity.Mesh.IndexBuffer.Handle);

                GL.DrawElements(PrimitiveType.Triangles, entity.Mesh.IndexBuffer.Count, DrawElementsType.UnsignedShort, 0);
            }

            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
            GL.UseProgram(Tonemap.Shader.Handle);

            GL.BindTextureUnit(0, HDRFramebuffer.ColorAttachment0.Handle);

            // Do the tonemap
            GL.DrawArrays(PrimitiveType.Triangles, 0, 3);
            
            // FIXME: Tonemap

            SwapBuffers();

            // FIXME: Move to entity?
            static Matrix4 GetLocalToWorldTransform(Entity entity)
            {
                Matrix4 matrix = Matrix4.Identity;
                do
                {
                    matrix = matrix * entity.Transform.LocalToParent;
                    entity = entity.Parent!;
                } while (entity != null);
                return matrix;
            }
        }
    }
}
