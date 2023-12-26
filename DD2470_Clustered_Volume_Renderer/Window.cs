using ImGuiNET;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
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

        public ImGuiController ImGuiController;

        public VAO TheVAO;

        // FIXME: Handle framebuffer resize!
        public Framebuffer HDRFramebuffer;
        public Framebuffer HiZMipFramebuffer;

        public Texture DefaultAlbedo;
        public Texture DefaultNormal;
        public Texture DefaultRoughnessMetallic;

        public Texture SkyboxRadiance;
        public Texture SkyboxIrradiance;
        public Material SkyboxMaterial;
        public float SkyBoxExposure = 0.1f;

        public Texture BrdfLUT;

        public Material DebugMaterial;

        public Material DefaultMaterial;
        public Material DefaultMaterialAlphaCutout;

        public Material Tonemap;

        public Material HiZDepthCopy;
        public Material HiZPass;

        public Buffer ClusterData;
        public Material ClusterGenPass;

        public Camera Camera;
        public Camera Camera2;
        public List<Entity> Entities;

        public Buffer LightBuffer;
        public List<PointLight> Lights = new List<PointLight>();

        public Mesh2 CubeMesh;

        protected override void OnLoad()
        {
            base.OnLoad();

            VSync = VSyncMode.Off;

            ImGuiController = new ImGuiController(FramebufferSize.X, FramebufferSize.Y);
            
            HDRFramebuffer = Framebuffer.CreateHDRFramebuffer("HDR Framebuffer", FramebufferSize.X, FramebufferSize.Y);

            HiZMipFramebuffer = Framebuffer.CreateHiZFramebuffer("HI-Z Framebuffer", FramebufferSize.X, FramebufferSize.Y);

            BrdfLUT = Texture.LoadTexture("./ibl_brdf_lut.png", false, true);

            Shader skyboxShader = Shader.CreateVertexFragment("Skybox Shader", "./Shaders/skybox.vert", "./Shaders/skybox.frag");
            SkyboxMaterial = new Material(skyboxShader, null);
            SkyboxMaterial.Albedo = Texture.LoadHDRICubeMapTexture("./Skybox/moonlit_golf/", true);

            SkyboxIrradiance = Texture.LoadHDRICubeMapTexture("./Skybox/moonlit_golf/irradiance/", true);
            SkyboxRadiance = Texture.LoadHDRICubeMapTexture("./Skybox/moonlit_golf/radiance/", true);
            SkyboxMaterial.Albedo = SkyboxRadiance;

            Shader debugShader = Shader.CreateVertexFragment("Debug Shader", "./Shaders/debug.vert", "./Shaders/debug.frag");
            DebugMaterial = new Material(debugShader, null);

            Shader defaultShader = Shader.CreateVertexFragment("Default Shader", "./Shaders/default.vert", "./Shaders/default.frag");
            Shader defaultShaderPrepass = Shader.CreateVertexFragment("Default Shader Prepass", "./Shaders/default.vert", "./Shaders/default_prepass.frag");
            DefaultMaterial = new Material(defaultShader, defaultShaderPrepass);
            Shader defaultShaderAlphaCutout = Shader.CreateVertexFragment("Default Shader Alpha Cutout", "./Shaders/default.vert", "./Shaders/alphaCutout.frag");
            Shader defaultShaderAlphaCutoutPrepass = Shader.CreateVertexFragment("Default Shader Alpha Cutout", "./Shaders/default.vert", "./Shaders/alphaCutout_prepass.frag");
            DefaultMaterialAlphaCutout = new Material(defaultShaderAlphaCutout, defaultShaderAlphaCutoutPrepass);

            CubeMesh = Model.CreateCube((1, 1, 1), DebugMaterial);

            // FIXME: Make the tonemapping more consistent?
            Shader tonemapShader = Shader.CreateVertexFragment("Tonemap Shader", "./Shaders/fullscreen.vert", "./Shaders/tonemap.frag");
            Tonemap = new Material(tonemapShader, null);

            Shader hiZPass = Shader.CreateVertexFragment("HiZ Pass Shader", "./Shaders/fullscreen.vert", "./Shaders/HiZMip.frag");
            HiZPass = new Material(hiZPass, null);

            Shader hiZDepthCopy = Shader.CreateVertexFragment("HiZ copy depth Shader", "./Shaders/fullscreen.vert", "./Shaders/depth_to_texture.frag");
            HiZDepthCopy = new Material(hiZDepthCopy, null);

            ClusterData = Buffer.CreateBuffer("Cluster AABBs", 16 * 9 * 24, 8 * sizeof(float), BufferStorageFlags.None);
            Shader clusterGen = Shader.CreateCompute("Cluster generation", "./Shaders/Clustered/build_clusters.comp");
            ClusterGenPass = new Material(clusterGen, null);

            DefaultAlbedo = Texture.FromColor(Color4.White, true);
            DefaultNormal = Texture.FromColor(new Color4(0.5f, 0.5f, 1f, 1f), false);
            DefaultRoughnessMetallic = Texture.FromColor(new Color4(1,1,1,1), false);

            Camera = new Camera(90, Size.X / (float)Size.Y, 0.1f, 10000f);
            Camera2 = new Camera(70, Size.X / (float)Size.Y, 50f, 1000f);
            Camera2.Transform.LocalPosition += (0, 100, 0);

            //Entities = Model.LoadModel("./Sponza/sponza.obj", 0.3f, defaultShader, defaultShaderPrepass, defaultShaderAlphaCutout, defaultShaderAlphaCutoutPrepass);
            //Entities = Model.LoadModel("C:\\Users\\juliu\\Desktop\\temple.glb", defaultShader, defaultShaderAlphaCutout);
            Entities = Model.LoadModel("./temple/temple.gltf", 1.0f, defaultShader, defaultShaderPrepass, defaultShaderAlphaCutout, defaultShaderAlphaCutoutPrepass);
            // Octahedron mapped point light shadows put into a atlas?

            const int NLights = 0;
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
            /*Lights.Add(new PointLight(
                new Vector3(0, 500, 0),
                10000,
                Color4.White,
                1_000_00
                ));*/
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
            Graphics.SetVertexAttribute(TheVAO, 1, true, 4, VertexAttribType.Int2101010Rev, true, 0);
            Graphics.SetVertexAttribute(TheVAO, 2, true, 4, VertexAttribType.Int2101010Rev, true, 4);
            Graphics.SetVertexAttribute(TheVAO, 3, true, 2, VertexAttribType.Float, false, 8);

            Graphics.BindVertexArray(TheVAO);

            // FIXME: Make a graphics thing for this...
            GL.Enable(EnableCap.DepthTest);
            GL.Enable(EnableCap.TextureCubeMapSeamless);
            Graphics.SetCullMode(CullMode.CullBackFacing);
            Graphics.SetDepthFunc(DepthFunc.PassIfLessOrEqual);
            Graphics.SetDepthWrite(true);
        }

        protected override void OnFramebufferResize(FramebufferResizeEventArgs e)
        {
            base.OnFramebufferResize(e);

            GL.Viewport(0, 0, e.Width, e.Height);

            GL.DeleteFramebuffer(HDRFramebuffer.Handle);
            GL.DeleteTexture(HDRFramebuffer.ColorAttachment0.Handle);
            GL.DeleteTexture(HDRFramebuffer.DepthStencilAttachment.Handle);
            HDRFramebuffer = Framebuffer.CreateHDRFramebuffer("HDR Framebuffer", e.Width, e.Height);

            GL.DeleteFramebuffer(HiZMipFramebuffer.Handle);
            GL.DeleteTexture(HiZMipFramebuffer.ColorAttachment0.Handle);
            HiZMipFramebuffer = Framebuffer.CreateHiZFramebuffer("HI-Z Framebuffer", e.Width, e.Height);

            Camera.AspectRatio = e.Width / (float)e.Height;

            ImGuiController.WindowResized(e.Width, e.Height);
        }

        protected override void OnUpdateFrame(FrameEventArgs args)
        {
            base.OnUpdateFrame(args);

            float deltaTime = (float)args.Time;

            Camera.UpdateEditorCamera(Camera, KeyboardState, MouseState, deltaTime);

            // This starts a imgui frame.
            ImGuiController.Update(this, deltaTime);

            if (ImGui.Begin("Camera"))
            {
                const float CameraMinY = -80f;
                const float CameraMaxY = 80f;

                ImGui.SeparatorText("Camera 2");
                ImGui.DragFloat3("Position", ref Unsafe.As<Vector3, System.Numerics.Vector3>(ref Camera2.Transform.LocalPosition));
                ImGui.DragFloat("Rotation X", ref Camera2.XAxisRotation, 0.01f);
                ImGui.DragFloat("Rotation Y", ref Camera2.YAxisRotation, 0.01f);
                Camera2.XAxisRotation = MathHelper.Clamp(Camera2.XAxisRotation, CameraMinY * Util.D2R, CameraMaxY * Util.D2R);
                Camera2.Transform.LocalRotation =
                    Quaternion.FromAxisAngle(Vector3.UnitY, Camera2.YAxisRotation) *
                    Quaternion.FromAxisAngle(Vector3.UnitX, Camera2.XAxisRotation);

                ImGui.DragFloat("Near plane", ref Camera2.NearPlane);
                if (Camera2.NearPlane < 0.001f)
                    Camera2.NearPlane = 0.001f;
                ImGui.DragFloat("Far plane", ref Camera2.FarPlane);
                if (Camera2.FarPlane <= Camera2.NearPlane + 1)
                    Camera2.FarPlane = Camera.NearPlane + 1;
                ImGui.DragFloat("Vertical FoV", ref Camera2.VerticalFov);
            }
            ImGui.End();

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

            Title = $"{args.Time*1000:0.000}ms ({1/args.Time:0.00} fps)";
        }

        // How do I want to represent entity draw data?
        // I do a pass over all the entities wherer I gather all of their transforms?
        // Then I do a frustum culling pass over that?

        struct Drawcall
        {
            public Buffer PositionBuffer;
            public Buffer AttributeBuffer;
            public Buffer IndexBuffer;

            public Shader Shader;
            public Shader? PrepassShader;

            public Texture AlbedoTexture;
            public Texture NormalTexture;
            public Texture RoughnessMetallicTexture;

            public int InstanceCount;
            public int BaseVertex;
            public int IndexCount;
            public int IndexByteOffset;
            public int IndexSize;

            public Buffer InstanceData;
            public int InstanceOffset;
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
            // Use mat4 for std430 alignment rules...
            public Matrix4 NormalMatrix;
        }

        struct EntityRenderData
        {
            public bool Culled;
            public Entity Entity;
            public Matrix4 ModelMatrix;
        }

        protected unsafe override void OnRenderFrame(FrameEventArgs args)
        {
            base.OnRenderFrame(args);

            Matrix4 viewMatrix = Camera.Transform.ParentToLocal;
            Matrix4 projectionMatrix = Camera.ProjectionMatrix;
            Matrix4 vp = viewMatrix * projectionMatrix;

            Frustum frustum = Frustum.FromCamera(Camera);

            Stopwatch watch = Stopwatch.StartNew();

            // FIXME: Do not create a new list every frame...
            // FIXME: Linq
            List<EntityRenderData> RenderEntities = new List<EntityRenderData>(Entities.Select(e => new EntityRenderData() { Entity = e }));

            {
                // Calculate transformation matrices
                Span<EntityRenderData> RenderEntitiesSpan = CollectionsMarshal.AsSpan(RenderEntities);
                for (int i = 0; i < RenderEntities.Count; i++)
                {
                    RenderEntitiesSpan[i].Culled = RenderEntitiesSpan[i].Entity.Mesh == null;
                    RenderEntitiesSpan[i].ModelMatrix = GetLocalToWorldTransform(RenderEntities[i].Entity);
                }
            }

            double transformTime = watch.Elapsed.TotalMilliseconds;
            watch.Restart();

            RenderEntities.Sort((e1, e2) => Material.Compare(e1.Entity.Mesh?.Material, e2.Entity.Mesh?.Material));

            {
                // Add a debug visualization for this?
                Span<EntityRenderData> RenderEntitiesSpan = CollectionsMarshal.AsSpan(RenderEntities);
                Frustum debugFrustum = Frustum.FromCamera(Camera2);
                for (int i = 0; i < RenderEntities.Count; i++)
                {
                    ref EntityRenderData renderData = ref RenderEntitiesSpan[i];
                    if (renderData.Entity.Mesh == null)
                        continue;

                    // FIXME: Something weird is going on...
                    Box3 AABB = RecalculateAABB(renderData.Entity.Mesh.AABB, renderData.ModelMatrix);
                    if (Frustum.IntersectsAABB(frustum, AABB) == false)
                    {
                        //Console.WriteLine($"Intersects! {renderData.Entity.Name}");
                        renderData.Culled = true;
                    }

                    static Box3 RecalculateAABB(Box3 AABB, Matrix4 localToWorld)
                    {
                        // http://www.realtimerendering.com/resources/GraphicsGems/gems/TransBox.c

                        // FIXME:
                        //var l2w = transform.LocalToWorld;
                        Matrix4 l2w = localToWorld;
                        Matrix3 rotation = new Matrix3(l2w);
                        Vector3 translation = l2w.Row3.Xyz;

                        Span<float> Amin = stackalloc float[3];
                        Span<float> Amax = stackalloc float[3];
                        Span<float> Bmin = stackalloc float[3];
                        Span<float> Bmax = stackalloc float[3];

                        Amin[0] = AABB.Min.X; Amax[0] = AABB.Max.X;
                        Amin[1] = AABB.Min.Y; Amax[1] = AABB.Max.Y;
                        Amin[2] = AABB.Min.Z; Amax[2] = AABB.Max.Z;

                        Bmin[0] = Bmax[0] = translation.X;
                        Bmin[1] = Bmax[1] = translation.Y;
                        Bmin[2] = Bmax[2] = translation.Z;

                        for (int i = 0; i < 3; i++)
                        {
                            for (int j = 0; j < 3; j++)
                            {
                                var a = rotation[j, i] * Amin[j];
                                var b = rotation[j, i] * Amax[j];
                                Bmin[i] += a < b ? a : b;
                                Bmax[i] += a < b ? b : a;
                            }
                        }

                        return new Box3(Bmin[0], Bmin[1], Bmin[2], Bmax[0], Bmax[1], Bmax[2]);
                    }
                }
            }

            // Remove all culled entities from the list
            RenderEntities.RemoveAll(e => e.Culled);

            double cullingTime = watch.Elapsed.TotalMilliseconds;
            watch.Restart();

            // FIXME: We are allocating this every frame...
            // Create buffer for instance data
            Buffer instanceDataBuffer = Buffer.CreateBuffer("Instance data", RenderEntities.Count, sizeof(InstanceData), BufferStorageFlags.DynamicStorageBit);

            // FIXME: Ideally we would want to calculate all the transformation data on the GPU
            // That would allow us to much more efficiently calculate all transforms
            // But for now we will just optimize this case.
            int thingsToRender = 0;
            List<Drawcall> drawcalls = new List<Drawcall>();
            for (int i = 0; i < RenderEntities.Count; i++)
            {
                EntityRenderData baseRenderData = RenderEntities[i];
                if (baseRenderData.Entity.Mesh == null)
                    continue;

                int instanceCount = 1;
                while (i + instanceCount < RenderEntities.Count && CanInstance(baseRenderData.Entity, RenderEntities[i + instanceCount].Entity))
                {
                    instanceCount++;
                }

                // FIXME: Sort the instanced items by distance from AABB center.
                
                Span<EntityRenderData> instanceEntities = CollectionsMarshal.AsSpan(RenderEntities).Slice(i, instanceCount);

                // Sort by distance to the near plane
                instanceEntities.Sort((e1, e2) => 
                            MathF.Sign(
                                System.Numerics.Plane.Dot(frustum.Near, e1.ModelMatrix.Row3.AsNumerics()) - 
                                System.Numerics.Plane.Dot(frustum.Near, e2.ModelMatrix.Row3.AsNumerics())
                                ));

                var test = instanceEntities.ToArray().Select(e => System.Numerics.Plane.Dot(frustum.Near, e.ModelMatrix.Row3.AsNumerics())).ToArray();

                InstanceData[] instanceData = new InstanceData[instanceCount];
                for (int instance = 0; instance < instanceData.Length; instance++)
                {
                    Matrix4 modelMatrix = RenderEntities[i + instance].ModelMatrix;
                    Matrix4 mvp = modelMatrix * vp;
                    Matrix3 normalMatrix = Matrix3.Transpose(new Matrix3(modelMatrix).Inverted());

                    instanceData[instance].ModelMatrix = modelMatrix;
                    instanceData[instance].MVP = mvp;
                    instanceData[instance].NormalMatrix = new Matrix4(normalMatrix);
                }

                Buffer.UpdateSubData<InstanceData>(instanceDataBuffer, instanceData, i);

                Stopwatch watch2 = Stopwatch.StartNew();
                Drawcall drawcall = new Drawcall
                {
                    PositionBuffer = baseRenderData.Entity.Mesh.PositionBuffer,
                    AttributeBuffer = baseRenderData.Entity.Mesh.AttributeBuffer,
                    IndexBuffer = baseRenderData.Entity.Mesh.IndexBuffer,
                    Shader = baseRenderData.Entity.Mesh.Material.Shader,
                    PrepassShader = baseRenderData.Entity.Mesh.Material.PrepassShader,
                    AlbedoTexture = baseRenderData.Entity.Mesh.Material.Albedo ?? DefaultAlbedo,
                    NormalTexture = baseRenderData.Entity.Mesh.Material.Normal ?? DefaultNormal,
                    RoughnessMetallicTexture = baseRenderData.Entity.Mesh.Material.RoughnessMetallic ?? DefaultRoughnessMetallic,
                    InstanceCount = instanceCount,
                    BaseVertex = baseRenderData.Entity.Mesh.BaseVertex,
                    IndexCount = baseRenderData.Entity.Mesh.IndexCount,
                    IndexByteOffset = baseRenderData.Entity.Mesh.IndexByteOffset,
                    IndexSize = baseRenderData.Entity.Mesh.IndexSize,

                    InstanceData = instanceDataBuffer,
                    InstanceOffset = i,
                };
                drawcalls.Add(drawcall);
                thingsToRender += instanceCount;

                //Console.WriteLine($"Upload buffer data: {watch2.Elapsed.TotalMilliseconds}ms");
                watch2.Stop();

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

            double drawcallGenTime = watch.Elapsed.TotalMilliseconds;
            watch.Stop();

            //Console.WriteLine($"Transform time: {cullingTime}ms");
            //Console.WriteLine($"Culling time: {cullingTime}ms");
            //Console.WriteLine($"Gen drawcall time: {drawcallGenTime}ms");

            //Console.WriteLine(thingsToRender);

            Graphics.SetDepthWrite(true);
            Graphics.SetColorWrite(ColorChannels.All);
            Graphics.SetClearColor(Color4.Black);
            Graphics.Clear(ClearMask.Color | ClearMask.Depth | ClearMask.Stencil);

            GL.BindFramebuffer(FramebufferTarget.Framebuffer, HDRFramebuffer.Handle);
            
            // FIXME: Reverse Z?

            Graphics.SetCullMode(CullMode.CullBackFacing);

            GL.PushDebugGroup(DebugSourceExternal.DebugSourceApplication, 1, -1, "Depth prepass");
            {
                Graphics.SetDepthWrite(true);
                Graphics.SetColorWrite(ColorChannels.None);
                Graphics.Clear(ClearMask.Depth);
                Graphics.SetDepthFunc(DepthFunc.PassIfLessOrEqual);

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

                    Graphics.BindShaderStorageBlockRange(1, drawcall.InstanceData, drawcall.InstanceOffset * sizeof(InstanceData), drawcall.InstanceCount * sizeof(InstanceData));


                    // FIXME: maybe use the buffer element size here instead of sizeof()?
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
            }
            GL.PopDebugGroup();

            GL.PushDebugGroup(DebugSourceExternal.DebugSourceApplication, 1, -1, "Hi-Z");
            {
                Graphics.UseShader(HiZDepthCopy.Shader);

                int mipWidth = HDRFramebuffer.DepthStencilAttachment.Width;
                int mipHeight = HDRFramebuffer.DepthStencilAttachment.Height;

                GL.Disable(EnableCap.DepthTest);
                Graphics.SetDepthWrite(false);
                Graphics.SetColorWrite(ColorChannels.All);

                // Copy over the depth data...
                GL.BindFramebuffer(FramebufferTarget.Framebuffer, HiZMipFramebuffer.Handle);
                GL.NamedFramebufferTexture(HiZMipFramebuffer.Handle, FramebufferAttachment.ColorAttachment0, HiZMipFramebuffer.ColorAttachment0.Handle, 0);
                GL.Viewport(0, 0, HiZMipFramebuffer.ColorAttachment0.Width, HiZMipFramebuffer.ColorAttachment0.Height);
                Graphics.BindTexture(0, HDRFramebuffer.DepthStencilAttachment);
                GL.DrawArrays(PrimitiveType.Triangles, 0, 3);

                Graphics.UseShader(HiZPass.Shader);

                for (int i = 1; i < HiZMipFramebuffer.ColorAttachment0.MipCount; i++)
                {
                    mipWidth = Math.Max(1, mipWidth / 2);
                    mipHeight = Math.Max(1, mipHeight / 2);

                    int layerToRenderTo = i;
                    int layerToSampleFrom = layerToRenderTo - 1;

                    GL.NamedFramebufferTexture(HiZMipFramebuffer.Handle, FramebufferAttachment.ColorAttachment0, HiZMipFramebuffer.ColorAttachment0.Handle, layerToRenderTo);
                    GL.Viewport(0, 0, mipWidth, mipHeight);

                    var status = GL.CheckNamedFramebufferStatus(HiZMipFramebuffer.Handle, FramebufferTarget.Framebuffer);
                    if (status != FramebufferStatus.FramebufferComplete)
                    {
                        Console.WriteLine($"Incomplete framebuffer: {status}");
                    }

                    Graphics.BindTexture(0, HiZMipFramebuffer.ColorAttachment0);
                    GL.TextureParameter(HiZMipFramebuffer.ColorAttachment0.Handle, TextureParameterName.TextureBaseLevel, layerToSampleFrom);
                    GL.TextureParameter(HiZMipFramebuffer.ColorAttachment0.Handle, TextureParameterName.TextureMaxLevel, layerToSampleFrom);

                    GL.DrawArrays(PrimitiveType.Triangles, 0, 3);
                }

                GL.TextureParameter(HiZMipFramebuffer.ColorAttachment0.Handle, TextureParameterName.TextureBaseLevel, 0);
                GL.TextureParameter(HiZMipFramebuffer.ColorAttachment0.Handle, TextureParameterName.TextureMaxLevel, 1000);

                GL.Enable(EnableCap.DepthTest);

                GL.BindFramebuffer(FramebufferTarget.Framebuffer, HDRFramebuffer.Handle);
                GL.Viewport(0, 0, HDRFramebuffer.ColorAttachment0.Width, HDRFramebuffer.ColorAttachment0.Height);
            }
            GL.PopDebugGroup();

            GL.PushDebugGroup(DebugSourceExternal.DebugSourceApplication, 1, -1, "Clustering");
            {
                Graphics.UseShader(ClusterGenPass.Shader);
                Graphics.BindShaderStorageBlock(0, ClusterData);

                GL.DispatchCompute(16, 9, 24);
            }
            GL.PopDebugGroup();

            GL.PushDebugGroup(DebugSourceExternal.DebugSourceApplication, 1, -1, "Color pass");
            {
                Graphics.SetDepthWrite(false);
                Graphics.SetColorWrite(ColorChannels.All);
                Graphics.SetDepthFunc(DepthFunc.PassIfEqual);

                Graphics.SetClearColor(Camera.ClearColor);
                Graphics.Clear(ClearMask.Color);

                Graphics.UseShader(DefaultMaterial.Shader);

                for (int i = 0; i < drawcalls.Count; i++)
                {
                    Drawcall drawcall = drawcalls[i];

                    Graphics.UseShader(drawcall.Shader);

                    Graphics.BindTexture(0, drawcall.AlbedoTexture);
                    Graphics.BindTexture(1, drawcall.NormalTexture);
                    Graphics.BindTexture(2, drawcall.RoughnessMetallicTexture);

                    // FIXME: Get this from a skybox object?
                    Graphics.BindTexture(5, SkyboxIrradiance);
                    Graphics.BindTexture(6, SkyboxRadiance);
                    Graphics.BindTexture(7, BrdfLUT);

                    Graphics.BindShaderStorageBlock(0, LightBuffer);
                    Graphics.BindShaderStorageBlockRange(1, drawcall.InstanceData, drawcall.InstanceOffset * sizeof(InstanceData), drawcall.InstanceCount * sizeof(InstanceData));

                    // FIXME: We assume the camera transform has no parent.
                    GL.Uniform3(10, Camera.Transform.LocalPosition);

                    GL.Uniform1(15, SkyBoxExposure);

                    if (drawcall.Shader == DefaultMaterialAlphaCutout.Shader)
                    {
                        // Alpha cutout
                        GL.Uniform1(20, 0.5f);
                    }

                    // FIXME: maybe use the buffer element size here instead of sizeof()?
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

                // Draw skybox
                {
                    Graphics.UseShader(SkyboxMaterial.Shader);

                    Graphics.SetDepthFunc(DepthFunc.PassIfLessOrEqual);

                    Graphics.BindTexture(0, SkyboxMaterial.Albedo);

                    Matrix4 centeredVP = new Matrix4(new Matrix3(viewMatrix)) * projectionMatrix;
                    GL.UniformMatrix4(0, true, ref centeredVP);
                    GL.Uniform1(15, SkyBoxExposure);

                    Graphics.BindVertexAttributeBuffer(TheVAO, 0, CubeMesh.PositionBuffer, 0, sizeof(Vector3h));
                    Graphics.SetElementBuffer(TheVAO, CubeMesh.IndexBuffer);

                    var elementType = CubeMesh.IndexBuffer.Size switch
                    {
                        2 => DrawElementsType.UnsignedShort,
                        4 => DrawElementsType.UnsignedInt,
                        _ => throw new NotSupportedException(),
                    };

                    GL.DrawElements(PrimitiveType.Triangles, CubeMesh.IndexBuffer.Count, elementType, 0);
                }
            }
            GL.PopDebugGroup();

            GL.PushDebugGroup(DebugSourceExternal.DebugSourceApplication, 1, -1, "Debug pass");
            {
                Graphics.SetDepthWrite(false);
                Graphics.SetColorWrite(ColorChannels.All);
                Graphics.SetDepthFunc(DepthFunc.PassIfLessOrEqual);
                Graphics.SetCullMode(CullMode.CullNone);

                GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Line);

                Graphics.UseShader(CubeMesh.Material.Shader);

                Matrix4 view = Camera2.Transform.ParentToLocal;
                Matrix4 proj = Camera2.ProjectionMatrix;
                Matrix4 invVP = (/*view */ proj).Inverted();

                Matrix4 model = Camera2.Transform.LocalToParent;
                Matrix4 mvp = model * vp;
                Matrix3 normal = Matrix3.Transpose(new Matrix3(model).Inverted());

                GL.UniformMatrix4(0, true, ref mvp);
                GL.UniformMatrix4(1, true, ref model);
                GL.UniformMatrix3(2, true, ref normal);
                GL.UniformMatrix4(3, true, ref invVP);

                Graphics.BindVertexAttributeBuffer(TheVAO, 0, CubeMesh.PositionBuffer, 0, sizeof(Vector3h));
                Graphics.BindVertexAttributeBuffer(TheVAO, 1, CubeMesh.AttributeBuffer, 0, sizeof(VertexAttributes));
                Graphics.SetElementBuffer(TheVAO, CubeMesh.IndexBuffer);

                var elementType = CubeMesh.IndexSize switch
                {
                    2 => DrawElementsType.UnsignedShort,
                    4 => DrawElementsType.UnsignedInt,
                    _ => throw new NotSupportedException(),
                };

                GL.DrawElements(PrimitiveType.Triangles, CubeMesh.IndexCount, elementType, CubeMesh.IndexByteOffset);

                GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Fill);
                Graphics.SetCullMode(CullMode.CullBackFacing);
            }
            GL.PopDebugGroup();

            GL.PushDebugGroup(DebugSourceExternal.DebugSourceApplication, 1, -1, "Postprocess");
            {
                // Clean up instance data.
                // FIXME: Do something smarter with the buffer??
                Buffer.DeleteBuffer(instanceDataBuffer);

                GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
                Graphics.UseShader(Tonemap.Shader);

                Graphics.BindTexture(0, HDRFramebuffer.ColorAttachment0);

                Graphics.SetDepthWrite(false);
                Graphics.SetColorWrite(ColorChannels.All);
                Graphics.SetDepthFunc(DepthFunc.AlwaysPass);

                // Do the tonemap
                GL.DrawArrays(PrimitiveType.Triangles, 0, 3);
            }
            GL.PopDebugGroup();

            GL.PushDebugGroup(DebugSourceExternal.DebugSourceApplication, 1, -1, "ImGui");
            {
                ImGuiController.Render();
            }
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

        protected override void OnTextInput(TextInputEventArgs e)
        {
            base.OnTextInput(e);

            ImGuiController.PressChar((char)e.Unicode);
        }

        protected override void OnMouseWheel(MouseWheelEventArgs e)
        {
            base.OnMouseWheel(e);

            ImGuiController.MouseScroll(e.Offset);
        }
    }
}
