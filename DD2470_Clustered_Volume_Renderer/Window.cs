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
using static Assimp.Metadata;

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

        public VAO TheVAO;

        // FIXME: Handle framebuffer resize!
        public Framebuffer HDRFramebuffer;

        public Texture DefaultAlbedo;
        public Texture DefaultNormal;

        public Material DefaultMaterial;
        public Material DefaultMaterialAlphaCutout;

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
            Shader defaultShaderPrepass = Shader.CreateVertexFragment("Default Shader Prepass", "./Shaders/default.vert", "./Shaders/default_prepass.frag");
            DefaultMaterial = new Material(defaultShader, defaultShaderPrepass);
            Shader defaultShaderAlphaCutout = Shader.CreateVertexFragment("Default Shader Alpha Cutout", "./Shaders/default.vert", "./Shaders/alphaCutout.frag");
            Shader defaultShaderAlphaCutoutPrepass = Shader.CreateVertexFragment("Default Shader Alpha Cutout", "./Shaders/default.vert", "./Shaders/alphaCutout_prepass.frag");
            DefaultMaterialAlphaCutout = new Material(defaultShaderAlphaCutout, defaultShaderAlphaCutoutPrepass);


            // FIXME: Make the tonemapping more consistent?
            Shader tonemapShader = Shader.CreateVertexFragment("Tonemap Shader", "./Shaders/fullscreen.vert", "./Shaders/tonemap.frag");
            Tonemap = new Material(tonemapShader, null);

            DefaultAlbedo = Texture.FromColor(Color4.White, true);
            DefaultNormal = Texture.FromColor(new Color4(0.5f, 0.5f, 1f, 1f), false);

            Camera = new Camera(90, Size.X / (float)Size.Y, 0.1f, 10000f);

            //Entities = Model.LoadModel("./Sponza/sponza.obj", 0.3f, defaultShader, defaultShaderPrepass, defaultShaderAlphaCutout, defaultShaderAlphaCutoutPrepass);
            //Entities = Model.LoadModel("C:\\Users\\juliu\\Desktop\\temple.glb", defaultShader, defaultShaderAlphaCutout);
            Entities = Model.LoadModel("./temple/temple.gltf", 1.0f, defaultShader, defaultShaderPrepass, defaultShaderAlphaCutout, defaultShaderAlphaCutoutPrepass);
            // Octahedron mapped point light shadows put into a atlas?

            const int NLights = 100;
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
                        rand.NextSingle() * 10000 + 1f));
            }
            // "sun"
            Lights.Add(new PointLight(
                new Vector3(0, 500, 0),
                10000,
                Color4.White,
                1_000_00
                ));
            LightBuffer = Buffer.CreateBuffer("Point Light buffer", Lights, BufferStorageFlags.None);



            // FIXME: Make a VAO for each mesh?
            TheVAO = Graphics.SetupVAO("The VAO");

            // Separate position buffer.
            Graphics.LinkAttributeBufferBinding(TheVAO, 0, 0);
            Graphics.SetVertexAttribute(TheVAO, 0, true, 3, VertexAttribType.HalfFloat, false, 0);
            // Other vertex attributes.
            Graphics.LinkAttributeBufferBinding(TheVAO, 1, 1);
            Graphics.LinkAttributeBufferBinding(TheVAO, 2, 1);
            Graphics.LinkAttributeBufferBinding(TheVAO, 3, 1);
            Graphics.SetVertexAttribute(TheVAO, 1, true, 4, VertexAttribType.Int2101010Rev, false, 0);
            Graphics.SetVertexAttribute(TheVAO, 2, true, 4, VertexAttribType.Int2101010Rev, false, 4);
            Graphics.SetVertexAttribute(TheVAO, 3, true, 2, VertexAttribType.Float, false, 8);

            Graphics.BindVertexArray(TheVAO);

            // FIXME: Make a graphics thing for this...
            GL.Enable(EnableCap.DepthTest);
            GL.Enable(EnableCap.CullFace);
            Graphics.SetCullMode(CullMode.CullBackFacing);
            Graphics.SetDepthFunc(DepthFunc.PassIfLessOrEqual);
            Graphics.SetDepthWrite(true);
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

            Title = $"{args.Time*1000:0.000}ms";
        }

        struct Drawcall
        {
            public Buffer PositionBuffer;
            public Buffer AttributeBuffer;
            public Buffer IndexBuffer;

            public Shader Shader;
            public Shader? PrepassShader;

            public Texture AlbedoTexture;
            public Texture NormalTexture;

            public int InstanceCount;
            public int BaseVertex;
            public int IndexCount;
            public int IndexByteOffset;
            public int IndexSize;

            public Buffer InstanceData;
        }

        struct MaterialData
        {
            public Texture Albedo;
            public Texture Normal;
            // ...

            MaterialBlock Data;

            struct MaterialBlock
            {
                public float AlphaCutout;
            }
        }

        struct InstanceData
        {
            public Matrix4 ModelMatrix;
            public Matrix4 MVP;
            // Do this for std430 alignment rules...
            public Matrix4 NormalMatrix;
        }

        protected unsafe override void OnRenderFrame(FrameEventArgs args)
        {
            base.OnRenderFrame(args);

            List<Entity> RenderEntities = new List<Entity>(Entities);
            RenderEntities.Sort((e1, e2) => Material.Compare(e1.Mesh?.Material, e2.Mesh?.Material));

            List<Drawcall> drawcalls = new List<Drawcall>();
            for (int i = 0; i < RenderEntities.Count; i++)
            {
                Entity baseEntity = RenderEntities[i];
                if (baseEntity.Mesh == null)
                    continue;

                int instanceCount = 1;
                while (i + instanceCount < RenderEntities.Count && CanInstance(baseEntity, RenderEntities[i + instanceCount]))
                {
                    instanceCount++;
                }

                Matrix4 viewMatrix = Camera.Transform.ParentToLocal;
                Matrix4 projectionMatrix = Camera.ProjectionMatrix;
                Matrix4 vp = viewMatrix * projectionMatrix;

                InstanceData[] instanceData = new InstanceData[instanceCount];
                for (int instance = 0; instance < instanceData.Length; instance++)
                {
                    Matrix4 modelMatrix = GetLocalToWorldTransform(RenderEntities[i + instance]);
                    Matrix4 mvp = modelMatrix * vp;
                    Matrix3 normalMatrix = Matrix3.Transpose(new Matrix3(modelMatrix).Inverted());

                    instanceData[instance].ModelMatrix = modelMatrix;
                    instanceData[instance].MVP = mvp;
                    instanceData[instance].NormalMatrix = new Matrix4(normalMatrix);
                }

                Drawcall drawcall = new Drawcall
                {
                    PositionBuffer = baseEntity.Mesh.PositionBuffer,
                    AttributeBuffer = baseEntity.Mesh.AttributeBuffer,
                    IndexBuffer = baseEntity.Mesh.IndexBuffer,
                    Shader = baseEntity.Mesh.Material.Shader,
                    PrepassShader = baseEntity.Mesh.Material.PrepassShader,
                    AlbedoTexture = baseEntity.Mesh.Material.Albedo ?? DefaultAlbedo,
                    NormalTexture = baseEntity.Mesh.Material.Normal ?? DefaultNormal,
                    InstanceCount = instanceCount,
                    BaseVertex = baseEntity.Mesh.BaseVertex,
                    IndexCount = baseEntity.Mesh.IndexCount,
                    IndexByteOffset = baseEntity.Mesh.IndexByteOffset,
                    IndexSize = baseEntity.Mesh.IndexSize,
                    // FIXME: We are allocating this every frame?
                    InstanceData = Buffer.CreateBuffer("", instanceData, BufferStorageFlags.None)
                };
                drawcalls.Add(drawcall);

                i += instanceCount - 1;

                static bool CanInstance(Entity @base, Entity entity)
                {
                    if (@base.Mesh == null || entity.Mesh == null) return false;

                    if (@base.Mesh.PositionBuffer == entity.Mesh.PositionBuffer &&
                        @base.Mesh.AttributeBuffer == entity.Mesh.AttributeBuffer &&
                        @base.Mesh.IndexBuffer == entity.Mesh.IndexBuffer &&
                        @base.Mesh.BaseVertex == entity.Mesh.BaseVertex &&
                        @base.Mesh.IndexCount == entity.Mesh.IndexCount &&
                        @base.Mesh.IndexSize == entity.Mesh.IndexSize &&
                        @base.Mesh.Material.Shader == entity.Mesh.Material.Shader)
                    {
                        return true;
                    }
                    else
                    {
                        return false;
                    }
                }
            }

            Graphics.SetClearColor(Color4.Black);
            Graphics.Clear(ClearMask.Color | ClearMask.Depth | ClearMask.Stencil);

            GL.BindFramebuffer(FramebufferTarget.Framebuffer, HDRFramebuffer.Handle);

            // FIXME: Reverse Z?

            GL.PushDebugGroup(DebugSourceExternal.DebugSourceApplication, 1, -1, "Depth prepass");

            Graphics.SetDepthWrite(true);
            Graphics.SetColorWrite(ColorChannels.None);
            Graphics.Clear(ClearMask.Depth);
            Graphics.SetDepthFunc(DepthFunc.PassIfLessOrEqual);

#if false
            // FIXME: Loop through all entities and draw them.
            for (int i = 0; i < RenderEntities.Count; i++)
            {
                Entity entity = RenderEntities[i];

                // If there is nothing to render, skip it.
                if (entity.Mesh == null)
                    continue;

                // Bind the prepass shader?
                Graphics.UseShader(entity.Mesh.Material.PrepassShader);

                // FIXME: Make calculating these more efficient?
                Matrix4 modelMatrix = GetLocalToWorldTransform(entity);
                Matrix4 viewMatrix = Camera.Transform.ParentToLocal;
                Matrix4 projectionMatrix = Camera.ProjectionMatrix;

                Matrix4 vp = viewMatrix * projectionMatrix;
                Matrix4 mvp = modelMatrix * vp;

                GL.UniformMatrix4(0, true, ref mvp);

                if (entity.Mesh.Material.PrepassShader == DefaultMaterialAlphaCutout.PrepassShader)
                {
                    Graphics.BindTexture(0, entity.Mesh.Material.Albedo ?? DefaultAlbedo);
                    // Alpha cutout
                    GL.Uniform1(20, 0.5f);
                }

                Graphics.BindVertexAttributeBuffer(TheVAO, 0, entity.Mesh.PositionBuffer, 0, sizeof(Vector3h));
                Graphics.BindVertexAttributeBuffer(TheVAO, 1, entity.Mesh.AttributeBuffer, 0, sizeof(VertexAttributes));
                Graphics.SetElementBuffer(TheVAO, entity.Mesh.IndexBuffer);
                
                var elementType = entity.Mesh.IndexSize switch
                {
                    2 => DrawElementsType.UnsignedShort,
                    4 => DrawElementsType.UnsignedInt,
                    _ => throw new NotSupportedException(),
                };

                GL.DrawElementsBaseVertex(PrimitiveType.Triangles, entity.Mesh.IndexCount, elementType, entity.Mesh.IndexByteOffset, entity.Mesh.BaseVertex);
                //GL.DrawElements(PrimitiveType.Triangles, entity.Mesh.IndexBuffer.Count, elementType, 0);
            }
#else
            for (int i = 0; i < drawcalls.Count; i++)
            {
                Drawcall drawcall = drawcalls[i];
                Graphics.UseShader(drawcall.PrepassShader ?? drawcall.Shader);

                if (drawcall.PrepassShader == DefaultMaterialAlphaCutout.PrepassShader)
                {
                    Graphics.BindTexture(0, drawcall.AlbedoTexture);

                    // Alpha cutout
                    GL.Uniform1(20, 0.5f);
                }

                Graphics.BindShaderStorageBlock(1, drawcall.InstanceData);

                Graphics.BindVertexAttributeBuffer(TheVAO, 0, drawcall.PositionBuffer, 0, sizeof(Vector3h));
                Graphics.BindVertexAttributeBuffer(TheVAO, 1, drawcall.AttributeBuffer, 0, sizeof(VertexAttributes));
                Graphics.SetElementBuffer(TheVAO, drawcall.IndexBuffer);

                var elementType = drawcall.IndexSize switch
                {
                    2 => DrawElementsType.UnsignedShort,
                    4 => DrawElementsType.UnsignedInt,
                    _ => throw new NotSupportedException(),
                };

                GL.DrawElementsInstancedBaseVertex(PrimitiveType.Triangles, drawcall.IndexCount, elementType, drawcall.IndexByteOffset, drawcall.InstanceCount, drawcall.BaseVertex);

                //GL.DrawElementsBaseVertex(PrimitiveType.Triangles, drawcall.IndexCount, elementType, drawcall.IndexByteOffset, drawcall.BaseVertex);
            }
#endif

            GL.PopDebugGroup();

            GL.PushDebugGroup(DebugSourceExternal.DebugSourceApplication, 1, -1, "Color pass");

            Graphics.SetDepthWrite(false);
            Graphics.SetColorWrite(ColorChannels.All);
            Graphics.SetDepthFunc(DepthFunc.PassIfEqual);

            Graphics.SetClearColor(Camera.ClearColor);
            Graphics.Clear(ClearMask.Color);

            Graphics.UseShader(DefaultMaterial.Shader);

#if false
            // FIXME: Loop through all entities and draw them.
            for (int i = 0; i < RenderEntities.Count; i++)
            {
                Entity entity = RenderEntities[i];

                // If there is nothing to render, skip it.
                if (entity.Mesh == null)
                    continue;

                Graphics.UseShader(entity.Mesh.Material.Shader);

                Graphics.BindTexture(0, entity.Mesh.Material.Albedo ?? DefaultAlbedo);
                Graphics.BindTexture(1, entity.Mesh.Material.Normal ?? DefaultNormal);

                // Bind the light buffer handle
                Graphics.BindShaderStorageBlock(0, LightBuffer);

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

                if (entity.Mesh.Material.Shader == DefaultMaterialAlphaCutout.Shader)
                {
                    // Alpha cutout
                    GL.Uniform1(20, 0.5f);
                }

                Graphics.BindVertexAttributeBuffer(TheVAO, 0, entity.Mesh.PositionBuffer, 0, sizeof(Vector3h));
                Graphics.BindVertexAttributeBuffer(TheVAO, 1, entity.Mesh.AttributeBuffer, 0, sizeof(VertexAttributes));
                Graphics.SetElementBuffer(TheVAO, entity.Mesh.IndexBuffer);

                var elementType = entity.Mesh.IndexSize switch
                {
                    2 => DrawElementsType.UnsignedShort,
                    4 => DrawElementsType.UnsignedInt,
                    _ => throw new NotSupportedException(),
                };

                GL.DrawElementsBaseVertex(PrimitiveType.Triangles, entity.Mesh.IndexCount, elementType, entity.Mesh.IndexByteOffset, entity.Mesh.BaseVertex);

                //GL.DrawElements(PrimitiveType.Triangles, entity.Mesh.IndexBuffer.Count, elementType, 0);
            }
#else
            for (int i = 0; i < drawcalls.Count; i++)
            {
                Drawcall drawcall = drawcalls[i];

                Graphics.UseShader(drawcall.Shader);

                Graphics.BindTexture(0, drawcall.AlbedoTexture);
                Graphics.BindTexture(1, drawcall.NormalTexture);

                Graphics.BindShaderStorageBlock(0, LightBuffer);
                Graphics.BindShaderStorageBlock(1, drawcall.InstanceData);

                // FIXME: We assume the camera transform has no parent.
                GL.Uniform3(10, Camera.Transform.LocalPosition);

                if (drawcall.Shader == DefaultMaterialAlphaCutout.Shader)
                {
                    // Alpha cutout
                    GL.Uniform1(20, 0.5f);
                }

                Graphics.BindVertexAttributeBuffer(TheVAO, 0, drawcall.PositionBuffer, 0, sizeof(Vector3h));
                Graphics.BindVertexAttributeBuffer(TheVAO, 1, drawcall.AttributeBuffer, 0, sizeof(VertexAttributes));
                Graphics.SetElementBuffer(TheVAO, drawcall.IndexBuffer);

                var elementType = drawcall.IndexSize switch
                {
                    2 => DrawElementsType.UnsignedShort,
                    4 => DrawElementsType.UnsignedInt,
                    _ => throw new NotSupportedException(),
                };

                GL.DrawElementsInstancedBaseVertex(PrimitiveType.Triangles, drawcall.IndexCount, elementType, drawcall.IndexByteOffset, drawcall.InstanceCount, drawcall.BaseVertex);
            }
#endif

            GL.PopDebugGroup();
            GL.PushDebugGroup(DebugSourceExternal.DebugSourceApplication, 1, -1, "Postprocess");

            // Clean up instance data.
            // FIXME: Do something smarter with the buffers??
            for (int i = 0; i < drawcalls.Count; i++)
            {
                Drawcall drawcall = drawcalls[i];

                Buffer.DeleteBuffer(drawcall.InstanceData);
            }

            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
            Graphics.UseShader(Tonemap.Shader);

            Graphics.BindTexture(0, HDRFramebuffer.ColorAttachment0);

            Graphics.SetDepthWrite(false);
            Graphics.SetColorWrite(ColorChannels.All);
            Graphics.SetDepthFunc(DepthFunc.AlwaysPass);

            // Do the tonemap
            GL.DrawArrays(PrimitiveType.Triangles, 0, 3);

            GL.PopDebugGroup();

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
