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
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Text;
using System.Threading.Tasks;
using System.Transactions;

namespace DD2470_Clustered_Volume_Renderer
{
    internal struct PointLight
    {
        public Vector3 Position;
        public float InverseSquareRadius;
        public Vector3 Color;
        public float SquareRadius;

        public PointLight(Vector3 position, float radius, Color4 color, float intensity)
        {
            Position = position;
            InverseSquareRadius = 1 / (radius * radius);
            Color = new Vector3(color.R, color.G, color.B) * intensity;
            SquareRadius = radius * radius;
        }
    }

    internal struct ProjectionData
    {
        public Matrix4 InverseProjection;
        public Vector2i ScreenSize;
        public float NearZ;
        public float FarZ;
    }

    internal struct ViewData
    {
        public Matrix4 InverseProjectionMatrix;
        public Matrix4 InverseViewMatrix;
        public Vector2i ScreenSize;
        public float NearZ;
        public float FarZ;
    }

    enum RenderPath
    {
        ForwardPath,
        ClusteredForwardPath,
    }

    internal class Window : GameWindow
    {
        public Window(GameWindowSettings gameWindowSettings, NativeWindowSettings nativeWindowSettings) : base(gameWindowSettings, nativeWindowSettings)
        {
        }

        public ImGuiController ImGuiController;

        public static RenderPath CurrentRenderpath = RenderPath.ClusteredForwardPath;

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
        public float SkyBoxExposure = 1f;

        public Texture BrdfLUT;

        public Material DebugMaterial;
        public Material AABBDebugMaterial;
        public Material LightDebugMaterial;

        public Material DefaultMaterial;
        public Material DefaultMaterialAlphaCutout;

        public Material Tonemap;

        public Material HiZDepthCopy;
        public Material HiZPass;

        // If these are changed the shaders also need to be modified!
        public static readonly Vector3i ClusterCounts = (16, 9, 24);
        public static readonly int TotalClusters = ClusterCounts.X * ClusterCounts.Y * ClusterCounts.Z;
        public static readonly int MaxLightsPerCluster = 256;
        public static readonly int WorstCaseNumberOfClustersFilled = (int)(TotalClusters * 0.5);
        public Buffer ClusterData;
        public Buffer ProjectionDataBuffer;
        public Buffer LightIndexBuffer;
        public Buffer LightGridBuffer;
        public Buffer AtomicIndexCountBuffer;
        public Buffer DebugBuffer;
        public Material ClusterGenPass;
        public Material LightAssignmentPass;

        // 32 x 18 x 72
        // FIXME: What resolution do we want?
        public static readonly Vector3i VolumeFroxels = ClusterCounts * (10, 10, 4);
        public Texture VolumeScatterExtinctionTexture;
        public Texture VolumeEmissionPhaseTexture;
        public Material VolumeDensityTransferPass;
        public Material VolumeDensityInScatterPass;
        public Material VolumeIntegrationPass;
        public Buffer VolumeViewDataBuffer;

        public float FogBottomHeight = -10;
        public float FogTopHeight = 0;
        // FIXME: Better names...
        public float FogDensity = 1;
        public float FogDensityScale = 0.5f;

        public Camera Camera;
        public Camera Camera2;

        public List<Entity> Entities;

        List<EntityRenderData> RenderEntities;

        public Buffer LightBuffer;
        public List<PointLight> Lights = new List<PointLight>();

        public Mesh2 CubeMesh;
        public Mesh2 LightMesh;

        public bool ShowCamera2Frustum;
        public bool ShowClusterDebug;
        public bool ShowLights;

        public float NearClusterDepth = 0.5f;

        protected override void OnLoad()
        {
            base.OnLoad();

            VSync = VSyncMode.Off;

            ImGuiController = new ImGuiController(FramebufferSize.X, FramebufferSize.Y);
            
            HDRFramebuffer = Framebuffer.CreateHDRFramebuffer("HDR Framebuffer", FramebufferSize.X, FramebufferSize.Y);

            HiZMipFramebuffer = Framebuffer.CreateHiZFramebuffer("HI-Z Framebuffer", FramebufferSize.X, FramebufferSize.Y);

            BrdfLUT = Texture.LoadTexture("./ibl_brdf_lut.png", false, true);

            Shader skyboxShader = Shader.CreateVertexFragment("Skybox Shader", "./Shaders/skybox.vert", "./Shaders/skybox.frag");
            SkyboxMaterial = new Material(skyboxShader, null, skyboxShader, null);
            SkyboxMaterial.Albedo = DDSReader.LoadCubeMapTexture("./Skybox/moonlit_golf/moonlit_golf_cubemap.dds", true, false);

            SkyboxIrradiance = DDSReader.LoadCubeMapTexture("./Skybox/moonlit_golf/moonlit_golf_irradiance_cubemap.dds", true, false);
            SkyboxRadiance = DDSReader.LoadCubeMapTexture("./Skybox/moonlit_golf/moonlit_golf_radiance_cubemap.dds", false, false);

            Shader debugShader = Shader.CreateVertexFragment("Debug Shader", "./Shaders/debug.vert", "./Shaders/debug.frag");
            DebugMaterial = new Material(debugShader, null, debugShader, null);
            Shader aabbDebugShader = Shader.CreateVertexFragment("AABB Debug Shader", "./Shaders/debugAABB.vert", "./Shaders/debugAABB.frag");
            AABBDebugMaterial = new Material(aabbDebugShader, null, aabbDebugShader, null);
            Shader lightDebugShader = Shader.CreateVertexFragment("Light Debug Shader", "./Shaders/debugLight.vert", "./Shaders/debugLight.frag");
            LightDebugMaterial = new Material(lightDebugShader, null, lightDebugShader, null);

            Shader defaultShader = Shader.CreateVertexFragment("Default Shader", "./Shaders/default.vert", "./Shaders/default.frag");
            Shader defaultShaderPrepass = Shader.CreateVertexFragment("Default Shader Prepass", "./Shaders/default.vert", "./Shaders/default_prepass.frag");
            Shader defaultClusteredShader = Shader.CreateVertexFragment("Default Clustered Shader", "./Shaders/default.vert", "./Shaders/defaultClustered.frag");
            Shader defaultClusteredShaderPrepass = Shader.CreateVertexFragment("Default Clustered Shader Prepass", "./Shaders/default.vert", "./Shaders/default_prepass.frag");
            DefaultMaterial = new Material(defaultShader, defaultShaderPrepass, defaultClusteredShader, defaultClusteredShaderPrepass);
            Shader defaultShaderAlphaCutoutPrepass = Shader.CreateVertexFragment("Default Shader Alpha Cutout", "./Shaders/default.vert", "./Shaders/alphaCutout_prepass.frag");
            Shader defaultClusteredShaderAlphaCutoutPrepass = Shader.CreateVertexFragment("Default Clustered Shader Alpha Cutout", "./Shaders/default.vert", "./Shaders/alphaCutout_prepass.frag");
            DefaultMaterialAlphaCutout = new Material(defaultShader, defaultShaderAlphaCutoutPrepass, defaultClusteredShader, defaultClusteredShaderAlphaCutoutPrepass);

            CubeMesh = Model.CreateCube((1, 1, 1), DebugMaterial);

            // FIXME: Make the tonemapping more consistent?
            Shader tonemapShader = Shader.CreateVertexFragment("Tonemap Shader", "./Shaders/fullscreen.vert", "./Shaders/tonemap.frag");
            Tonemap = new Material(tonemapShader, null, tonemapShader, null);

            Shader hiZPass = Shader.CreateVertexFragment("HiZ Pass Shader", "./Shaders/fullscreen.vert", "./Shaders/HiZMip.frag");
            HiZPass = new Material(hiZPass, null, hiZPass, null);

            Shader hiZDepthCopy = Shader.CreateVertexFragment("HiZ copy depth Shader", "./Shaders/fullscreen.vert", "./Shaders/depth_to_texture.frag");
            HiZDepthCopy = new Material(hiZDepthCopy, null, hiZDepthCopy, null);

            ClusterData = Buffer.CreateBuffer("Cluster AABBs", TotalClusters, 8 * sizeof(float), BufferStorageFlags.None);
            Shader clusterGen = Shader.CreateCompute("Cluster generation", "./Shaders/Clustered/build_clusters.comp");
            ClusterGenPass = new Material(clusterGen, null, clusterGen, null);
            Shader lightAssignment = Shader.CreateCompute("Light assignment", "./Shaders/Clustered/assign_lights.comp");
            LightAssignmentPass = new Material(lightAssignment, null, lightAssignment, null);

            // FIXME: Figure out if this should be a mapped buffer or double buffered.
            unsafe
            {
                ProjectionDataBuffer = Buffer.CreateBuffer("Projection Buffer", 1, sizeof(ProjectionData), BufferStorageFlags.DynamicStorageBit);
            }

            // Max 50 lights per cluster.
            LightIndexBuffer = Buffer.CreateBuffer("Light Index Buffer", MaxLightsPerCluster * WorstCaseNumberOfClustersFilled, sizeof(uint), BufferStorageFlags.None);
            LightGridBuffer = Buffer.CreateBuffer("Light Grid Buffer", TotalClusters, 2 * sizeof(uint), BufferStorageFlags.None);
            AtomicIndexCountBuffer = Buffer.CreateBuffer("Atomic Light Index", 1, sizeof(uint), BufferStorageFlags.None);
            DebugBuffer = Buffer.CreateBuffer("Debug buffer", 1, TotalClusters * 26 * 100, BufferStorageFlags.None);

            DefaultAlbedo = Texture.FromColor(Color4.White, true);
            DefaultNormal = Texture.FromColor(new Color4(0.5f, 0.5f, 1f, 1f), false);
            DefaultRoughnessMetallic = Texture.FromColor(new Color4(1,1,1,1), false);

            Camera = new Camera(90, Size.X / (float)Size.Y, 0.1f, 10000f);
            Camera2 = new Camera(70, Size.X / (float)Size.Y, 50f, 1000f);
            Camera2.Transform.LocalPosition += (0, 100, 0);

            // FIXME: Load the light model and display it as an unlit overlay.
            LightMesh = Model.LoadModel("./light.obj", 1, null, null, null, null)[0].Mesh!;

            //Entities = Model.LoadModel("./Sponza/sponza.obj", 0.3f, defaultShader, defaultShaderPrepass, defaultShaderAlphaCutout, defaultShaderAlphaCutoutPrepass);
            //Entities = Model.LoadModel("C:\\Users\\juliu\\Desktop\\temple.glb", defaultShader, defaultShaderAlphaCutout);
            Entities = Model.LoadModel("./temple/temple.gltf", 1.0f, defaultShader, defaultClusteredShader, defaultShader, defaultShaderAlphaCutoutPrepass);
            // Octahedron mapped point light shadows put into a atlas?

            RenderEntities = new List<EntityRenderData>(Entities.Where(static e => e.Mesh != null).Select(static e => new EntityRenderData() { Entity = e }));

            const int NLights = 100;
            Random rand = new Random();
            Vector3 min = new Vector3(-300, 0, -350);
            Vector3 max = new Vector3(300, 40, 250);
            for (int i = 0; i < NLights; i++)
            {
                Lights.Add(
                    new PointLight(
                        rand.NextVector3(min, max),
                        rand.NextSingle() * 1000 + 100f,
                        rand.NextColor4Hue(1, 1),
                        rand.NextSingle() * 100 + 1f));
            }
            // "sun"
            /*Lights.Add(new PointLight(
                new Vector3(0, 300, 0),
                1000,
                Color4.White,
                1_000_000
                ));*/
            Lights.Add(new PointLight(
                new Vector3(0, 10, -15),
                100,
                Color4.Red,
                100
                ));
            LightBuffer = Buffer.CreateBuffer("Point Light buffer", Lights, BufferStorageFlags.None);

            // 32 x 18 x 72
            VolumeScatterExtinctionTexture = Texture.CreateEmpty3D("Volume Scatter Extinction", VolumeFroxels.X, VolumeFroxels.Y, VolumeFroxels.Z, SizedInternalFormat.Rgba16f);
            VolumeScatterExtinctionTexture.SetFilter(TextureMinFilter.Linear, TextureMagFilter.Linear);
            VolumeScatterExtinctionTexture.SetWrap(TextureWrapMode.ClampToEdge, TextureWrapMode.ClampToEdge, TextureWrapMode.ClampToEdge);
            VolumeEmissionPhaseTexture = Texture.CreateEmpty3D("Volume Emission Phase", VolumeFroxels.X, VolumeFroxels.Y, VolumeFroxels.Z, SizedInternalFormat.Rgba16f);
            VolumeEmissionPhaseTexture.SetFilter(TextureMinFilter.Linear, TextureMagFilter.Linear);
            VolumeEmissionPhaseTexture.SetWrap(TextureWrapMode.ClampToEdge, TextureWrapMode.ClampToEdge, TextureWrapMode.ClampToEdge);

            Shader volumeDensityTransfer = Shader.CreateCompute("Density Transfer", "./Shaders/Volume/densityTransfer.comp");
            VolumeDensityTransferPass = new Material(volumeDensityTransfer, null, volumeDensityTransfer, null);
            Shader volumeLightInScatter = Shader.CreateCompute("In-Scatter", "./Shaders/Volume/lightInScatter.comp");
            VolumeDensityInScatterPass = new Material(volumeLightInScatter, null, volumeLightInScatter, null);
            Shader volumeIntegration = Shader.CreateCompute("Volume Integration", "./Shaders/Volume/integrateView.comp");
            VolumeIntegrationPass = new Material(volumeIntegration, null, volumeIntegration, null);

            unsafe
            {
                VolumeViewDataBuffer = Buffer.CreateBuffer("Volume View data", 1, sizeof(ViewData), BufferStorageFlags.DynamicStorageBit);
            }
            
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

            if (ImGui.Begin("Settings"))
            {
                ImGui.PushItemWidth(ImGui.GetWindowWidth() * 0.5f);

                if(ImGui.BeginCombo("Render path", CurrentRenderpath.ToString()))
                {
                    foreach (var item in Enum.GetValues<RenderPath>())
                    {
                        bool is_selected = (item == CurrentRenderpath);
                        if (ImGui.Selectable(item.ToString(), is_selected))
                        {
                            CurrentRenderpath = item;
                        }

                        if (is_selected)
                        {
                            ImGui.SetItemDefaultFocus();
                        }
                    }
                    ImGui.EndCombo();
                }

                ImGui.SeparatorText("Fog settings");

                ImGui.DragFloat("Fog Top Height", ref FogTopHeight);
                ImGui.DragFloat("Fog Base Height", ref FogBottomHeight);
                ImGui.DragFloat("Fog Density", ref FogDensity);
                ImGui.DragFloat("Fog Density Scale", ref FogDensityScale);

                ImGui.SeparatorText("Debug Visualizations");

                ImGui.Checkbox("Show second camera frustum", ref ShowCamera2Frustum);
                ImGui.Checkbox("Show cluster debug", ref ShowClusterDebug);
                ImGui.Checkbox("Show lights", ref ShowLights);

                ImGui.SeparatorText("Cluster settings");

                if (ImGui.DragFloat("Near cluster depth", ref NearClusterDepth, 1, 0.001f, Camera.FarPlane - Camera.NearPlane, null, ImGuiSliderFlags.Logarithmic))
                {
                    NearClusterDepth = float.Clamp(NearClusterDepth, 0.001f, Camera.FarPlane - Camera.NearPlane);
                }

                const float CameraMinY = -80f;
                const float CameraMaxY = 80f;

                ImGui.SeparatorText("Camera 2");
                if (ImGui.DragFloat3("Position", ref Unsafe.As<Vector3, System.Numerics.Vector3>(ref Camera2.Transform.UnsafePosition)))
                    Camera2.Transform.IsDirty = true;
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

                ImGui.Separator();

                bool ctrl = KeyboardState.IsKeyDown(Keys.LeftControl) || KeyboardState.IsKeyDown(Keys.RightControl);

                if (ImGui.Button("Recompile shaders") || (ctrl && KeyboardState.IsKeyPressed(Keys.R)))
                {
                    Shader.RecompileAllShaders();
                }
            }
            ImGui.End();

            if (ImGui.Begin("Timings"))
            {
                ImGui.TextUnformatted($"Total CPU time: {totalTime:0.000}ms");
                ImGui.TextUnformatted($"Transform: {transformTime:0.000}ms ({transformTime/totalTime:0.00}%)");
                ImGui.TextUnformatted($"Frustum culling: {cullingTime:0.000}ms ({cullingTime / totalTime:0.00}%)");
                ImGui.TextUnformatted($"Sorting: {sortingTime:0.000}ms ({sortingTime / totalTime:0.00}%)");
                ImGui.TextUnformatted($"Gen drawcalls: {drawcallGenTime:0.000}ms ({drawcallGenTime / totalTime:0.00}%)");

                ImGui.Separator();

                ImGui.TextUnformatted($"Visible entities: {Entities.Count - entitiesCulled}");
                ImGui.TextUnformatted($"Draw calls: {drawcallsGenerated}");
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
            public Shader PrepassShader;

            public Texture AlbedoTexture;
            public Texture NormalTexture;
            public Texture RoughnessMetallicTexture;

            public int InstanceCount;
            public int BaseVertex;
            public int IndexCount;
            public int IndexByteOffset;
            public DrawElementsType IndexType;

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

        struct EntityRenderData : IComparable<EntityRenderData>
        {
            public bool Culled;
            public Entity Entity;
            public Matrix4 ModelMatrix;

            public int CompareTo(EntityRenderData other)
            {
                if (!this.Culled && !other.Culled)
                    return Material.Compare(this.Entity.Mesh?.Material, other.Entity.Mesh?.Material, Window.CurrentRenderpath);
                else if (this.Culled && other.Culled)
                    return 0;
                else if (this.Culled)
                    return 1;
                else // if (other.Culled)
                    return -1;
            }
        }

        // FIXME: Update these at a more reasoable rate.
        double transformTime;
        double cullingTime;
        double sortingTime;
        double drawcallGenTime;
        double totalTime;

        int entitiesCulled;
        int drawcallsGenerated;

        protected unsafe override void OnRenderFrame(FrameEventArgs args)
        {
            base.OnRenderFrame(args);
            long start = Stopwatch.GetTimestamp();

            Matrix4 viewMatrix = Camera.Transform.ParentToLocal;
            Matrix4 projectionMatrix = Camera.ProjectionMatrix;
            Matrix4 vp = viewMatrix * projectionMatrix;

            ProjectionData projectionData;
            projectionData.InverseProjection = Matrix4.Invert(Camera.ProjectionMatrix);
            projectionData.ScreenSize = FramebufferSize;
            projectionData.NearZ = Camera.NearPlane;
            projectionData.FarZ = Camera.FarPlane;
            Buffer.UpdateSubData<ProjectionData>(ProjectionDataBuffer, stackalloc ProjectionData[1] { projectionData }, 0);

            ViewData viewData;
            viewData.InverseProjectionMatrix = Matrix4.Invert(projectionMatrix);
            viewData.InverseViewMatrix = Matrix4.Invert(viewMatrix);
            viewData.ScreenSize = FramebufferSize;
            viewData.NearZ = Camera.NearPlane;
            viewData.FarZ = Camera.FarPlane;
            Buffer.UpdateSubData<ViewData>(VolumeViewDataBuffer, stackalloc ViewData[1] { viewData }, 0);

            Frustum frustum = Frustum.FromCamera(Camera);

            Stopwatch watch = Stopwatch.StartNew();

            {
                // Calculate transformation matrices
                Span<EntityRenderData> RenderEntitiesSpan = CollectionsMarshal.AsSpan(RenderEntities);
                for (int i = 0; i < RenderEntities.Count; i++)
                {
                    RenderEntitiesSpan[i].Culled = RenderEntitiesSpan[i].Entity.Mesh == null;
                    RenderEntitiesSpan[i].ModelMatrix = GetLocalToWorldTransform2(RenderEntities[i].Entity);
                }
            }

            transformTime = watch.Elapsed.TotalMilliseconds;
            watch.Restart();

            {
                entitiesCulled = 0;
                // Add a debug visualization for this?
                Span<EntityRenderData> RenderEntitiesSpan = CollectionsMarshal.AsSpan(RenderEntities);
                Frustum debugFrustum = Frustum.FromCamera(Camera2);
                for (int i = 0; i < RenderEntities.Count; i++)
                {
                    ref EntityRenderData renderData = ref RenderEntitiesSpan[i];
                    if (renderData.Entity.Mesh == null)
                        continue;

                    // FIXME: Something weird is going on...
                    //Box3 AABB = RecalculateAABB(renderData.Entity.Mesh.AABB, renderData.ModelMatrix);
                    Box3 AABB = RecalculateAABBSse(renderData.Entity.Mesh.AABB, renderData.ModelMatrix);
                    // sse4.1 implementation is slower than the naïve scalar version...
                    //if (Frustum.IntersectsAABBSse41(frustum, AABB) == false)
                    if (Frustum.IntersectsAABB(frustum, AABB) == false)
                    {
                        renderData.Culled = true;
                        entitiesCulled++;
                    }

                    /*
                    // FIXME: split the matrix and make the intersect function use both matrices.
                    Matrix4 mvp = renderData.ModelMatrix * vp;
                    if (Frustum.IntersectsAABBAvx(mvp, renderData.Entity.Mesh.AABB))
                    {
                        renderData.Culled = true;
                        entitiesCulled++;
                    }
                    */

                    // http://www.realtimerendering.com/resources/GraphicsGems/gems/TransBox.c
                    static Box3 RecalculateAABB(Box3 AABB, Matrix4 localToWorld)
                    {
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

                    // FIXME: Create fallback?
                    static Box3 RecalculateAABBSse(Box3 AABB, Matrix4 localToWorld)
                    {
                        // http://www.realtimerendering.com/resources/GraphicsGems/gems/TransBox.c

                        Span<Vector128<float>> Rows = new Span<Vector128<float>>(Unsafe.AsPointer(ref Unsafe.As<Matrix4, Vector128<float>>(ref localToWorld)), 4);

                        Vector128<float> Amin = AABB.Min.ToVector128();
                        Vector128<float> Amax = AABB.Max.ToVector128();
                        Vector128<float> Bmin = Rows[3];
                        Vector128<float> Bmax = Rows[3];

                        for (int i = 0; i < 3; i++)
                        {
                            var a = Sse.Multiply(Rows[i], Amin);
                            var b = Sse.Multiply(Rows[i], Amax);

                            Bmin = Sse.Add(Bmin, Sse.Min(a, b));
                            Bmax = Sse.Add(Bmax, Sse.Max(a, b));
                        }

                        return new Box3(Bmin.AsVector3(), Bmax.AsVector3());
                    }
                }
            }

            cullingTime = watch.Elapsed.TotalMilliseconds;
            watch.Restart();

            // How to avoid this sort? Have a separate list that stores the order?
            // Make the instancing ignore the culled instances?
            /*RenderEntities.Sort(static (e1, e2) => {
                    if (!e1.Culled && !e2.Culled)
                        return Material.Compare(e1.Entity.Mesh?.Material, e2.Entity.Mesh?.Material);
                    else if (e1.Culled && e2.Culled)
                        return 0;
                    else if (e1.Culled)
                        return 1;
                    else // if (e2.Culled)
                        return -1;
                });*/

            // Remove all culled entities from the list
            //RenderEntities.RemoveAll(e => e.Culled);

            sortingTime = watch.Elapsed.TotalMilliseconds;
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

                if (baseRenderData.Culled)
                    continue;

                int offset = 1;
                int instanceCount = 1;
                while (i + offset < RenderEntities.Count && CanInstance(baseRenderData.Entity, RenderEntities[i + offset].Entity, CurrentRenderpath))
                {
                    if (RenderEntities[i + offset].Culled == false)
                    {
                        instanceCount++;
                    }

                    offset++;
                }

                // FIXME: Sort the instanced items by distance from AABB center.
                
                Span<EntityRenderData> instanceEntities = CollectionsMarshal.AsSpan(RenderEntities).Slice(i, instanceCount);

                // Sort by distance to the near plane
                /*instanceEntities.Sort((e1, e2) => 
                            MathF.Sign(
                                System.Numerics.Plane.Dot(frustum.Near, e1.ModelMatrix.Row3.AsNumerics()) - 
                                System.Numerics.Plane.Dot(frustum.Near, e2.ModelMatrix.Row3.AsNumerics())
                                ));*/

                //var test = instanceEntities.ToArray().Select(e => System.Numerics.Plane.Dot(frustum.Near, e.ModelMatrix.Row3.AsNumerics())).ToArray();

                InstanceData[] instanceData = new InstanceData[instanceCount];
                int instance = 0;
                for (int index = 0; index < offset; index++)
                {
                    if (RenderEntities[i + index].Culled)
                        continue;

                    Matrix4 modelMatrix = RenderEntities[i + index].ModelMatrix;
                    Matrix4 mvp = modelMatrix * vp;
                    Matrix3 normalMatrix = Matrix3.Transpose(new Matrix3(modelMatrix).Inverted());

                    instanceData[instance].ModelMatrix = modelMatrix;
                    instanceData[instance].MVP = mvp;
                    instanceData[instance].NormalMatrix = new Matrix4(normalMatrix);
                    instance++;
                }

                Buffer.UpdateSubData<InstanceData>(instanceDataBuffer, instanceData, i);

                Stopwatch watch2 = Stopwatch.StartNew();
                Drawcall drawcall = new Drawcall
                {
                    PositionBuffer = baseRenderData.Entity.Mesh.PositionBuffer,
                    AttributeBuffer = baseRenderData.Entity.Mesh.AttributeBuffer,
                    IndexBuffer = baseRenderData.Entity.Mesh.IndexBuffer,
                    Shader = baseRenderData.Entity.Mesh.Material.GetShader(CurrentRenderpath),
                    PrepassShader = baseRenderData.Entity.Mesh.Material.GetPrepassShader(CurrentRenderpath),
                    AlbedoTexture = baseRenderData.Entity.Mesh.Material.Albedo ?? DefaultAlbedo,
                    NormalTexture = baseRenderData.Entity.Mesh.Material.Normal ?? DefaultNormal,
                    RoughnessMetallicTexture = baseRenderData.Entity.Mesh.Material.RoughnessMetallic ?? DefaultRoughnessMetallic,
                    InstanceCount = instanceCount,
                    BaseVertex = baseRenderData.Entity.Mesh.BaseVertex,
                    IndexCount = baseRenderData.Entity.Mesh.IndexCount,
                    IndexByteOffset = baseRenderData.Entity.Mesh.IndexByteOffset,
                    IndexType = baseRenderData.Entity.Mesh.IndexType,

                    InstanceData = instanceDataBuffer,
                    InstanceOffset = i,
                };
                drawcalls.Add(drawcall);
                thingsToRender += instanceCount;

                //Console.WriteLine($"Upload buffer data: {watch2.Elapsed.TotalMilliseconds}ms");
                watch2.Stop();

                i += offset - 1;

                static bool CanInstance(Entity @base, Entity entity, RenderPath renderPath)
                {
                    if (@base.Mesh == null || entity.Mesh == null) return false;

                    if (@base.Mesh.PositionBuffer == entity.Mesh.PositionBuffer &&
                        @base.Mesh.AttributeBuffer == entity.Mesh.AttributeBuffer &&
                        @base.Mesh.IndexBuffer == entity.Mesh.IndexBuffer &&
                        @base.Mesh.BaseVertex == entity.Mesh.BaseVertex &&
                        @base.Mesh.IndexCount == entity.Mesh.IndexCount &&
                        @base.Mesh.IndexType == entity.Mesh.IndexType &&
                        @base.Mesh.Material.GetShader(renderPath) == entity.Mesh.Material.GetShader(renderPath))
                    {
                        return true;
                    }
                    else
                    {
                        return false;
                    }
                }
            }
            drawcallsGenerated = drawcalls.Count;

            drawcallGenTime = watch.Elapsed.TotalMilliseconds;
            watch.Stop();

            System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
            //Console.WriteLine($"{cullingTime:0.000}");

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

                    Graphics.UseShader(drawcall.PrepassShader);

                    // FIXME: Better way to detect cutout shader...
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

                    GL.DrawElementsInstancedBaseVertex(PrimitiveType.Triangles, drawcall.IndexCount, drawcall.IndexType, drawcall.IndexByteOffset, drawcall.InstanceCount, drawcall.BaseVertex);
                }
            }
            GL.PopDebugGroup();

            if (CurrentRenderpath == RenderPath.ClusteredForwardPath)
            {
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
                    {
                        Graphics.UseShader(ClusterGenPass.Shader);
                        Graphics.BindShaderStorageBlock(0, ClusterData);
                        Graphics.BindUniformBuffer(1, ProjectionDataBuffer);

                        // FIXME: Some way to parameterize the number of clusters...
                        GL.DispatchCompute(ClusterCounts.X, ClusterCounts.Y, ClusterCounts.Z);
                    }

                    {
                        Graphics.UseShader(LightAssignmentPass.Shader);

                        // FIXME: this is unecessary?
                        Graphics.BindShaderStorageBlock(0, ClusterData);

                        Graphics.BindShaderStorageBlock(1, LightBuffer);
                        Graphics.BindShaderStorageBlock(2, LightIndexBuffer);
                        Graphics.BindShaderStorageBlock(3, LightGridBuffer);
                        Graphics.BindShaderStorageBlock(4, DebugBuffer);

                        Graphics.BindAtomicCounterBuffer(0, AtomicIndexCountBuffer);

                        // FIXME: Easy way to switch between camera and camera2 for this...
                        GL.UniformMatrix4(0, true, ref viewMatrix);

                        // FIXME: Some way to parameterize the number of clusters...
                        GL.DispatchCompute(ClusterCounts.X / 16, ClusterCounts.Y / 9, ClusterCounts.Z / 4);
                    }

                }
                GL.PopDebugGroup();
            }

            GL.PushDebugGroup(DebugSourceExternal.DebugSourceApplication, 1, -1, "Volume pass");
            {
                {
                    Graphics.UseShader(VolumeDensityTransferPass.Shader);

                    Graphics.BindImage(0, VolumeScatterExtinctionTexture, TextureAccess.WriteOnly);
                    Graphics.BindImage(1, VolumeEmissionPhaseTexture, TextureAccess.WriteOnly);

                    Graphics.BindUniformBuffer(0, VolumeViewDataBuffer);

                    GL.Uniform1(0, FogTopHeight);
                    GL.Uniform1(1, FogBottomHeight);
                    GL.Uniform1(2, FogDensity);
                    GL.Uniform1(3, FogDensityScale);

                    GL.DispatchCompute(VolumeFroxels.X / 16, VolumeFroxels.Y / 9, VolumeFroxels.Z / 4);
                }

                {
                    Graphics.UseShader(VolumeDensityInScatterPass.Shader);

                    Graphics.BindImage(0, VolumeScatterExtinctionTexture, TextureAccess.ReadWrite);
                    Graphics.BindImage(1, VolumeEmissionPhaseTexture, TextureAccess.ReadWrite);

                    Graphics.BindUniformBuffer(0, VolumeViewDataBuffer);

                    Graphics.BindShaderStorageBlock(1, LightBuffer);
                    Graphics.BindShaderStorageBlock(2, LightIndexBuffer);
                    Graphics.BindShaderStorageBlock(3, LightGridBuffer);

                    GL.Uniform3(0, Camera.Transform.LocalPosition);

                    GL.DispatchCompute(VolumeFroxels.X / 16, VolumeFroxels.Y / 9, VolumeFroxels.Z / 4);
                }

                {
                    Graphics.UseShader(VolumeIntegrationPass.Shader);

                    // FIXME: Do we want a separate texture for this?
                    Graphics.BindImage(0, VolumeScatterExtinctionTexture, TextureAccess.ReadWrite);

                    Graphics.BindUniformBuffer(0, VolumeViewDataBuffer);

                    GL.DispatchCompute(VolumeFroxels.X / 16, VolumeFroxels.Y / 9, 1);
                }
            }
            GL.PopDebugGroup();

            GL.PushDebugGroup(DebugSourceExternal.DebugSourceApplication, 1, -1, "Color pass");
            {
                Graphics.SetDepthWrite(false);
                Graphics.SetColorWrite(ColorChannels.All);
                Graphics.SetDepthFunc(DepthFunc.PassIfEqual);

                Graphics.SetClearColor(Camera.ClearColor);
                Graphics.Clear(ClearMask.Color);

                for (int i = 0; i < drawcalls.Count; i++)
                {
                    Drawcall drawcall = drawcalls[i];

                    // FIXME: Change shader!
                    Graphics.UseShader(drawcall.Shader);

                    Graphics.BindTexture(0, drawcall.AlbedoTexture);
                    Graphics.BindTexture(1, drawcall.NormalTexture);
                    Graphics.BindTexture(2, drawcall.RoughnessMetallicTexture);

                    // FIXME: Get this from a skybox object?
                    Graphics.BindTexture(5, SkyboxIrradiance);
                    Graphics.BindTexture(6, SkyboxRadiance);
                    Graphics.BindTexture(7, BrdfLUT);

                    Graphics.BindTexture(10, VolumeScatterExtinctionTexture);

                    Graphics.BindShaderStorageBlock(0, LightBuffer);
                    Graphics.BindShaderStorageBlockRange(1, drawcall.InstanceData, drawcall.InstanceOffset * sizeof(InstanceData), drawcall.InstanceCount * sizeof(InstanceData));

                    // FIXME: Make this into a UBO that we bind instead...
                    // FIXME: We assume the camera transform has no parent.
                    GL.Uniform3(10, Camera.Transform.LocalPosition);
                    GL.Uniform1(11, Camera.NearPlane);
                    GL.Uniform1(12, Camera.FarPlane);
                    // See: https://www.aortiz.me/2018/12/21/CG.html#light-culling-methods
                    GL.Uniform1(13, ClusterCounts.Z / float.Log2(Camera.FarPlane / Camera.NearPlane));
                    GL.Uniform1(14, -ClusterCounts.Z * float.Log2(Camera.NearPlane) / float.Log2(Camera.FarPlane / Camera.NearPlane));
                    GL.Uniform1(15, SkyBoxExposure);

                    GL.Uniform3(20, (uint)ClusterCounts.X, (uint)ClusterCounts.Y, (uint)ClusterCounts.Z);
                    GL.Uniform2(21, (uint)FramebufferSize.X, (uint)FramebufferSize.Y);
                    //GL.Uniform3(20, ClusterCounts);

                    // FIXME: maybe use the buffer element size here instead of sizeof()?
                    Graphics.BindVertexAttributeBuffer(TheVAO, 0, drawcall.PositionBuffer, 0, sizeof(Vector3h));
                    Graphics.BindVertexAttributeBuffer(TheVAO, 1, drawcall.AttributeBuffer, 0, sizeof(VertexAttributes));
                    Graphics.SetElementBuffer(TheVAO, drawcall.IndexBuffer);

                    GL.DrawElementsInstancedBaseVertex(PrimitiveType.Triangles, drawcall.IndexCount, drawcall.IndexType, drawcall.IndexByteOffset, drawcall.InstanceCount, drawcall.BaseVertex);
                }

                // Draw skybox
                {
                    Graphics.UseShader(SkyboxMaterial.Shader);

                    Graphics.SetDepthFunc(DepthFunc.PassIfLessOrEqual);

                    Graphics.BindTexture(0, SkyboxMaterial.Albedo);

                    Matrix4 centeredVP = new Matrix4(new Matrix3(viewMatrix)) * projectionMatrix;
                    GL.UniformMatrix4(0, true, ref centeredVP);
                    GL.Uniform1(15, SkyBoxExposure);

                    GL.Uniform2(21, (uint)FramebufferSize.X, (uint)FramebufferSize.Y);

                    Graphics.BindVertexAttributeBuffer(TheVAO, 0, CubeMesh.PositionBuffer, 0, sizeof(Vector3h));
                    Graphics.SetElementBuffer(TheVAO, CubeMesh.IndexBuffer);

                    GL.DrawElements(PrimitiveType.Triangles, CubeMesh.IndexBuffer.Count, CubeMesh.IndexType, 0);
                }
            }
            GL.PopDebugGroup();

            GL.PushDebugGroup(DebugSourceExternal.DebugSourceApplication, 1, -1, "Debug pass");
            {
                //Matrix4 view = Camera2.Transform.ParentToLocal;
                Matrix4 proj = Camera2.ProjectionMatrix;
                Matrix4 invP = proj.Inverted();

                Matrix4 model = Camera2.Transform.LocalToParent;
                Matrix4 mvp = model * vp;
                Matrix3 normal = Matrix3.Transpose(new Matrix3(model).Inverted());

                Matrix4 ident = Matrix4.Identity;

                if (ShowCamera2Frustum)
                {
                    Graphics.SetDepthWrite(false);
                    Graphics.SetColorWrite(ColorChannels.All);
                    Graphics.SetDepthFunc(DepthFunc.PassIfLessOrEqual);
                    Graphics.SetCullMode(CullMode.CullNone);

                    GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Line);

                    Graphics.UseShader(CubeMesh.Material.Shader);

                    GL.UniformMatrix4(0, true, ref mvp);
                    GL.UniformMatrix4(1, true, ref model);
                    GL.UniformMatrix3(2, true, ref normal);
                    GL.UniformMatrix4(3, true, ref invP);

                    Graphics.BindVertexAttributeBuffer(TheVAO, 0, CubeMesh.PositionBuffer, 0, sizeof(Vector3h));
                    Graphics.BindVertexAttributeBuffer(TheVAO, 1, CubeMesh.AttributeBuffer, 0, sizeof(VertexAttributes));
                    Graphics.SetElementBuffer(TheVAO, CubeMesh.IndexBuffer);

                    GL.DrawElements(PrimitiveType.Triangles, CubeMesh.IndexCount, CubeMesh.IndexType, CubeMesh.IndexByteOffset);

                    GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Fill);
                }

                Graphics.SetCullMode(CullMode.CullBackFacing);
                Graphics.SetDepthWrite(true);

                if (ShowClusterDebug)
                {
                    Graphics.UseShader(AABBDebugMaterial.Shader);

                    Graphics.BindVertexAttributeBuffer(TheVAO, 2, ClusterData, 0, 2 * sizeof(Vector4));
                    GL.VertexArrayBindingDivisor(TheVAO.Handle, 2, 1);
                    Graphics.LinkAttributeBufferBinding(TheVAO, 4, 2);
                    Graphics.LinkAttributeBufferBinding(TheVAO, 5, 2);
                    Graphics.SetVertexAttribute(TheVAO, 4, true, 3, VertexAttribType.Float, false, 0);
                    Graphics.SetVertexAttribute(TheVAO, 5, true, 3, VertexAttribType.Float, false, sizeof(Vector4));

                    GL.UniformMatrix4(0, true, ref projectionMatrix);
                    GL.UniformMatrix4(1, true, ref ident);
                    GL.UniformMatrix3(2, true, ref normal);
                    GL.UniformMatrix4(3, true, ref ident);

                    Graphics.BindShaderStorageBlock(3, LightGridBuffer);

                    GL.DrawElementsInstanced(PrimitiveType.Triangles, CubeMesh.IndexCount, CubeMesh.IndexType, CubeMesh.IndexByteOffset, TotalClusters);

                    GL.VertexArrayBindingDivisor(TheVAO.Handle, 2, 0);
                }

                if (ShowLights)
                {
                    Graphics.SetCullMode(CullMode.CullNone);

                    Graphics.UseShader(LightDebugMaterial.Shader);

                    Graphics.BindVertexAttributeBuffer(TheVAO, 0, LightMesh.PositionBuffer, 0, sizeof(Vector3h));
                    Graphics.BindVertexAttributeBuffer(TheVAO, 1, LightMesh.AttributeBuffer, 0, sizeof(VertexAttributes));
                    Graphics.SetElementBuffer(TheVAO, LightMesh.IndexBuffer);

                    Graphics.BindVertexAttributeBuffer(TheVAO, 2, LightBuffer, 0, 2 * sizeof(Vector4));
                    GL.VertexArrayBindingDivisor(TheVAO.Handle, 2, 1);
                    Graphics.LinkAttributeBufferBinding(TheVAO, 4, 2);
                    Graphics.LinkAttributeBufferBinding(TheVAO, 5, 2);
                    Graphics.SetVertexAttribute(TheVAO, 4, true, 4, VertexAttribType.Float, false, 0);
                    Graphics.SetVertexAttribute(TheVAO, 5, true, 3, VertexAttribType.Float, false, sizeof(Vector4));

                    GL.UniformMatrix4(0, true, ref vp);
                    GL.UniformMatrix4(1, true, ref model);
                    GL.UniformMatrix3(2, true, ref normal);
                    GL.UniformMatrix4(3, true, ref ident);

                    GL.DrawElementsInstanced(PrimitiveType.Triangles, LightMesh.IndexCount, LightMesh.IndexType, LightMesh.IndexByteOffset, Lights.Count);

                    Graphics.SetCullMode(CullMode.CullBackFacing);
                }
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

            long end = Stopwatch.GetTimestamp();
            totalTime = Stopwatch.GetElapsedTime(start, end).TotalMilliseconds;

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

            static Matrix4 GetLocalToWorldTransform2(Entity entity)
            {
                // Turns out that 
                System.Numerics.Matrix4x4 matrix = System.Numerics.Matrix4x4.Identity;
                do
                {
                    var localToParent = entity.Transform.LocalToParent;
                    matrix = matrix * Unsafe.As<Matrix4, System.Numerics.Matrix4x4>(ref localToParent);
                    entity = entity.Parent!;
                } while (entity != null);
                return Unsafe.As<System.Numerics.Matrix4x4, Matrix4>(ref matrix);
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
