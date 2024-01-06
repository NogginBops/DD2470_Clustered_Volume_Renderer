using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Assimp;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using StbImageSharp;

namespace DD2470_Clustered_Volume_Renderer
{
    internal class Entity
    {
        public string Name;
        public Transform Transform;
        public Entity? Parent;
        public List<Entity> Children;

        public Mesh2? Mesh;
        
        public Entity(string name, Transform transform, Entity? parent)
        {
            Name = name;
            Transform = transform;
            Parent = parent;
            Children = new List<Entity>();
        }

        public override string ToString()
        {
            return $"Entity - {Name}";
        }
    }

    /// <summary>
    /// A 2_10_10_10 packed normal
    /// </summary>
    public struct PackedNormal
    {
        public uint Normal;

        public PackedNormal(uint normal)
        {
            Normal = normal;
        }

        public static PackedNormal Pack(Vector3 normal)
        {
            return new PackedNormal(PackNormal(normal, true));
        }

        public static PackedNormal Pack(Vector3 normal, bool positiveW)
        {
            return new PackedNormal(PackNormal(normal, positiveW));
        }

        public static uint PackNormal(Vector3 normal, bool positiveW)
        {
            // FIXME: Make full use of the -2^9, 2^9 - 1 range?
            uint x = (uint)((int)(normal.X * 511) & 0x000003FF);
            uint y = (uint)((int)(normal.Y * 511) & 0x000003FF);
            uint z = (uint)((int)(normal.Z * 511) & 0x000003FF);
            uint w = positiveW ? (uint)0b01 : (uint)0b11;
            return w << 30 | z << 20 | y << 10 | x;
        }

        public static Vector4 UnpackNormal(uint normal)
        {
            // FIXME: Make full use of the -2^9, 2^9 - 1 range?
            int x = ((int)(((normal >> 00) & 0x03FF) << 22)) >> 22;
            int y = ((int)(((normal >> 10) & 0x03FF) << 22)) >> 22;
            int z = ((int)(((normal >> 20) & 0x03FF) << 22)) >> 22;
            int w = ((int)(((normal >> 30) & 0x03FF) << 30)) >> 30;

            return new Vector4(x / 511f, y / 511f, z / 511f, w);
        }

        public override string ToString()
        {
            return $"{UnpackNormal(Normal)}";
        }
    }

    internal struct VertexAttributes
    {
        public PackedNormal Normal;
        public PackedNormal Tangent;
        public Vector2 UVs;
    }

    internal class Mesh
    {
        public Buffer PositionBuffer;
        public Buffer AttributeBuffer;

        public Buffer IndexBuffer;

        public Material Material;

        // FIXME:
        //public Material Material;

        public Mesh(Buffer positionBuffer, Buffer attributeBuffer, Buffer indexBuffer, Material material)
        {
            PositionBuffer = positionBuffer;
            AttributeBuffer = attributeBuffer;
            IndexBuffer = indexBuffer;
            Material = material;
        }
    }

    internal class Mesh2
    {
        public int BaseVertex;
        public int IndexCount;
        public DrawElementsType IndexType;
        public int IndexByteOffset;

        public Material Material;

        public Buffer PositionBuffer;
        public Buffer AttributeBuffer;

        public Buffer IndexBuffer;

        public Box3 AABB;

        public Mesh2(int baseVertex, int indexCount, DrawElementsType indexType, int indexByteOffset, Material material)
        {
            BaseVertex = baseVertex;
            IndexCount = indexCount;
            IndexType = indexType;
            IndexByteOffset = indexByteOffset;
            Material = material;
        }
    }

    internal static class Model
    {
        public static List<Entity> LoadModel(string modelPath, float scale, Shader shader, Shader clusteredShader, Shader prepass, Shader prepassCutout)
        {
            AssimpContext context = new AssimpContext();
            string directory = Path.GetDirectoryName(modelPath)!;

            Stopwatch watch = Stopwatch.StartNew();
            Scene scene = context.ImportFile(modelPath, PostProcessSteps.Triangulate | PostProcessSteps.CalculateTangentSpace | PostProcessSteps.GenerateBoundingBoxes);

            watch.Stop();
            Console.WriteLine($"Loading model took: {watch.Elapsed.TotalMilliseconds:0.000}ms");
            Console.WriteLine("Started loading textures...");
            watch.Restart();

            List<Material> materials = new List<Material>();
            foreach (var material in scene.Materials)
            {
                // FIXME: Copy over color stuff.
                Material m;
                if (material.HasColorTransparent || material.Name == "Grass_Diffuse")
                {
                    m = new Material(shader, prepassCutout, clusteredShader, prepassCutout);
                }
                else
                {
                    m = new Material(shader, prepass, clusteredShader, prepass);
                }

                if (material.HasTextureDiffuse)
                {
                    // FIXME: Set filter settings!
                    string path = material.TextureDiffuse.FilePath;

                    // For now we just load it as is...
                    //EmbeddedTexture texture = scene.GetEmbeddedTexture(path);
                    //StbImage.stbi_set_flip_vertically_on_load(1);
                    //ImageResult result = ImageResult.FromMemory(texture.CompressedData, ColorComponents.RedGreenBlueAlpha);
                    //m.Albedo = Texture.FromImage(path, result, true, true);

                    string compressed_file = path.Replace("textures", "textures_compressed");
                    compressed_file = Path.ChangeExtension(compressed_file, "dds");
                    m.Albedo = DDSReader.LoadTexture(Path.Combine(directory, compressed_file), true, true);

                    m.Albedo.SetFilter(OpenTK.Graphics.OpenGL4.TextureMinFilter.LinearMipmapLinear, OpenTK.Graphics.OpenGL4.TextureMagFilter.Linear);
                    m.Albedo.SetAniso(16);
                    //m.Albedo = Texture.LoadTexture(Path.Combine(directory, material.TextureDiffuse.FilePath), true, true);
                }

                if (material.HasTextureNormal)
                {
                    //string path = material.TextureNormal.FilePath;
                    // For now we just load it as is...
                    //EmbeddedTexture texture = scene.GetEmbeddedTexture(path);
                    //StbImage.stbi_set_flip_vertically_on_load(1);
                    //ImageResult result = ImageResult.FromMemory(texture.CompressedData, ColorComponents.RedGreenBlueAlpha);
                    //m.Normal = Texture.FromImage(path, result, false, true);

                    string path = material.TextureNormal.FilePath;
                    string compressed_file = path.Replace("textures", "textures_compressed");
                    compressed_file = Path.ChangeExtension(compressed_file, "dds");
                    m.Normal = DDSReader.LoadTexture(Path.Combine(directory, compressed_file), true, false);

                    //m.Normal = Texture.LoadTexture(Path.Combine(directory, material.TextureNormal.FilePath), false, true);
                    
                    m.Normal.SetFilter(OpenTK.Graphics.OpenGL4.TextureMinFilter.LinearMipmapLinear, OpenTK.Graphics.OpenGL4.TextureMagFilter.Linear);
                    m.Normal.SetAniso(16);
                }
                // FIXME: the sponza we load puts normal maps as dispacement maps...
                else if (material.HasTextureDisplacement)
                {
                    string path = material.TextureDisplacement.FilePath;
                    string compressed_file = path.Replace("textures", "textures_compressed");
                    compressed_file = Path.ChangeExtension(compressed_file, "dds");
                    m.Normal = DDSReader.LoadTexture(Path.Combine(directory, compressed_file), true, false);

                    //m.Normal = Texture.LoadTexture(Path.Combine(directory, material.TextureDisplacement.FilePath), false, true);

                    m.Normal.SetFilter(OpenTK.Graphics.OpenGL4.TextureMinFilter.LinearMipmapLinear, OpenTK.Graphics.OpenGL4.TextureMagFilter.Linear);
                    m.Normal.SetAniso(16);
                }

                if (material.PBR.HasTextureRoughness)
                {
                    string path = material.PBR.TextureRoughness.FilePath;

                    // For now we just load it as is...
                    //EmbeddedTexture texture = scene.GetEmbeddedTexture(path);
                    //StbImage.stbi_set_flip_vertically_on_load(1);
                    //ImageResult result = ImageResult.FromMemory(texture.CompressedData, ColorComponents.RedGreenBlueAlpha);
                    //m.RoughnessMetallic = Texture.FromImage(path, result, true, false);

                    string compressed_file = path.Replace("textures", "textures_compressed");
                    compressed_file = Path.ChangeExtension(compressed_file, "dds");
                    m.RoughnessMetallic = DDSReader.LoadTexture(Path.Combine(directory, compressed_file), true, false);

                    m.RoughnessMetallic.SetFilter(OpenTK.Graphics.OpenGL4.TextureMinFilter.LinearMipmapLinear, OpenTK.Graphics.OpenGL4.TextureMagFilter.Linear);
                    m.RoughnessMetallic.SetAniso(16);

                    //m.RoughnessMetallic = Texture.LoadTexture(Path.Combine(directory, material.TextureDiffuse.FilePath), true, false);
                }
                else
                {
                    // Create textures from the roughness and metallness parameters
                    // alternatively make make it a completely white texture, that way we can have tint parameters.

                    m.RoughnessMetallic = Texture.FromColor(new Color4(1.0f, 0.2f, 0.0f, 1.0f), false);
                }

                materials.Add(m);
            }

            watch.Stop();
            Console.WriteLine($"Loading textures took: {watch.Elapsed.TotalMilliseconds:0.000}ms");

            List<Vector3h> meshPositions = new List<Vector3h>();
            List<VertexAttributes> meshAttributes = new List<VertexAttributes>();

            // FIXME: Split meshes into 32-bit indices and 16-bit indices!
            List<byte> meshElements = new List<byte>();

            List<Mesh2> meshes = new List<Mesh2>();
            foreach (var mesh in scene.Meshes)
            {
                /*
                Console.WriteLine($"Mesh: {mesh.Name}");
                Console.WriteLine($"  Has positions: {mesh.HasVertices}");
                Console.WriteLine($"  Has normals: {mesh.HasNormals}");
                Console.WriteLine($"  Has tangent: {mesh.HasTangentBasis}");
                for (int i = 0; i < mesh.TextureCoordinateChannelCount; i++)
                {
                    Console.WriteLine($"  Has UV{i}: {mesh.HasTextureCoords(i)}");
                }
                */

                Span<Vector3> positions = MemoryMarshal.Cast<Vector3D, Vector3>(CollectionsMarshal.AsSpan(mesh.Vertices));
                Vector3h[] halfPositons = new Vector3h[positions.Length];
                for (int i = 0; i < positions.Length; i++)
                {
                    halfPositons[i] = (Vector3h)positions[i];
                }
                int baseVertex = meshPositions.Count;
                meshPositions.AddRange(halfPositons);
                //Buffer postionBuffer = Buffer.CreateBuffer($"{mesh.Name}_position", halfPositons, BufferStorageFlags.None);

                Span<Vector3D> normals = CollectionsMarshal.AsSpan(mesh.Normals);
                // FIXME: Store the sign of the bitangent in there as well?
                Span<Vector3D> tangents = CollectionsMarshal.AsSpan(mesh.Tangents);
                Span<Vector3D> bitangents = CollectionsMarshal.AsSpan(mesh.BiTangents);
                Span<Vector3D> UVs = CollectionsMarshal.AsSpan(mesh.TextureCoordinateChannels[0]);
                VertexAttributes[] attribs = new VertexAttributes[normals.Length];

                InterleaveBuffers(attribs, normals, tangents, bitangents, UVs);
                meshAttributes.AddRange(attribs);

                int index_count;
                DrawElementsType index_size;
                int index_offset;
                if (mesh.FaceCount * 3 > ushort.MaxValue)
                {
                    uint[] indices = mesh.GetUnsignedIndices();

                    index_count = indices.Length;
                    index_size = DrawElementsType.UnsignedInt;
                    index_offset = meshElements.Count;
                    meshElements.AddRange(MemoryMarshal.Cast<uint, byte>(indices));
                }
                else
                {
                    short[] indices = mesh.GetShortIndices();

                    index_count = indices.Length;
                    index_size = DrawElementsType.UnsignedShort;
                    index_offset = meshElements.Count;
                    meshElements.AddRange(MemoryMarshal.Cast<short, byte>(indices));
                }

                //Mesh m = new Mesh(postionBuffer, attributeBuffer, indexBuffer, materials[mesh.MaterialIndex]);
                Mesh2 m = new Mesh2(baseVertex, index_count, index_size, index_offset, materials[mesh.MaterialIndex]);
                BoundingBox bounds = mesh.BoundingBox;
                m.AABB = new Box3(Unsafe.As<Vector3D, Vector3>(ref bounds.Min), Unsafe.As<Vector3D, Vector3>(ref bounds.Max));
                meshes.Add(m);
                
                static void InterleaveBuffers(Span<VertexAttributes> interleaved, Span<Vector3D> normals, Span<Vector3D> tangents, Span<Vector3D> bitangents, Span<Vector3D> UVs)
                {
                    for (int i = 0; i < interleaved.Length; i++)
                    {
                        bool positiveW = Vector3D.Dot(Vector3D.Cross(normals[i], tangents[i]), bitangents[i]) > 0;
                        
                        interleaved[i].Normal = PackedNormal.Pack(Unsafe.As<Vector3D, Vector3>(ref normals[i]));
                        interleaved[i].Tangent = PackedNormal.Pack(Unsafe.As<Vector3D, Vector3>(ref tangents[i]), positiveW);
                        interleaved[i].UVs = Unsafe.As<Vector3D, Vector3>(ref UVs[i]).Xy;
                    }
                }
            }

            Buffer positionBuffer = Buffer.CreateBuffer($"{scene.Name}_positions", meshPositions, BufferStorageFlags.None);
            Buffer attributeBuffer = Buffer.CreateBuffer($"{scene.Name}_vertexattributes", meshAttributes, BufferStorageFlags.None);
            Buffer indexBuffer = Buffer.CreateBuffer($"{scene.Name}_indices", meshElements, BufferStorageFlags.None);

            foreach (Mesh2 mesh in meshes)
            {
                mesh.PositionBuffer = positionBuffer;
                mesh.AttributeBuffer = attributeBuffer;
                mesh.IndexBuffer = indexBuffer;
            }

            List<Entity> entities = new List<Entity>();
            Entity rootEntity = ProcessNode(scene.RootNode, null, entities, meshes);
            rootEntity.Transform.LocalScale *= scale;
            return entities;

            static Entity ProcessNode(Node node, Entity? parent, List<Entity> entities, List<Mesh2> meshes)
            {
                //Console.WriteLine($"Name: {node.Name}");
                //Console.WriteLine($"  Has mesh: {node.HasMeshes}{(node.HasMeshes ? $" ({string.Join(", ", node.MeshIndices)})" : "")}");
                //Console.WriteLine($"  Children: {node.ChildCount}");
                var nodeTransform = node.Transform;
                Matrix4 matrix = Unsafe.As<Matrix4x4, Matrix4>(ref nodeTransform);
                matrix.Transpose();
                //Console.WriteLine($"  Transform: \n{matrix}");

                Vector3 position = matrix.ExtractTranslation();
                Vector3 scale = matrix.ExtractScale();// * (1/2f);
                OpenTK.Mathematics.Quaternion rotation = matrix.ExtractRotation();

                Transform transform = new Transform(rotation, position, scale);

                // For now we only support one mesh per node
                Debug.Assert(node.MeshCount <= 1);

                Mesh2? mesh = null;
                if (node.MeshCount > 0)
                {
                    mesh = meshes[node.MeshIndices[0]];
                }
                
                // FIXME: Decide if need really need to make an entity here.
                // FIXME: Parent
                Entity ent = new Entity(node.Name, transform, parent);
                ent.Mesh = mesh;
                
                foreach (Node child in node.Children)
                {
                    Entity childEnt = ProcessNode(child, ent, entities, meshes);
                    ent.Children.Add(childEnt);
                }

                entities.Add(ent);
                return ent;
            }
        }

        public static Mesh2 CreateCube(Vector3 halfSize, Material material)
        {
            // front & back wrong winding
            // bottom wrong winding.


            Span<Vector3h> positions = stackalloc Vector3h[]
            {
                // Front
                (Vector3h)(Vector3)(-halfSize.X, -halfSize.Y, +halfSize.Z),
                (Vector3h)(Vector3)(+halfSize.X, -halfSize.Y, +halfSize.Z),
                (Vector3h)(Vector3)(+halfSize.X, +halfSize.Y, +halfSize.Z),
                (Vector3h)(Vector3)(-halfSize.X, +halfSize.Y, +halfSize.Z),

                // Back
                (Vector3h)(Vector3)(-halfSize.X, -halfSize.Y, -halfSize.Z),
                (Vector3h)(Vector3)(-halfSize.X, +halfSize.Y, -halfSize.Z),
                (Vector3h)(Vector3)(+halfSize.X, +halfSize.Y, -halfSize.Z),
                (Vector3h)(Vector3)(+halfSize.X, -halfSize.Y, -halfSize.Z),

                // Left
                (Vector3h)(Vector3)(-halfSize.X, -halfSize.Y, +halfSize.Z),
                (Vector3h)(Vector3)(-halfSize.X, +halfSize.Y, +halfSize.Z),
                (Vector3h)(Vector3)(-halfSize.X, +halfSize.Y, -halfSize.Z),
                (Vector3h)(Vector3)(-halfSize.X, -halfSize.Y, -halfSize.Z),

                // Right
                (Vector3h)(Vector3)(+halfSize.X, -halfSize.Y, -halfSize.Z),
                (Vector3h)(Vector3)(+halfSize.X, +halfSize.Y, -halfSize.Z),
                (Vector3h)(Vector3)(+halfSize.X, +halfSize.Y, +halfSize.Z),
                (Vector3h)(Vector3)(+halfSize.X, -halfSize.Y, +halfSize.Z),

                // Top
                (Vector3h)(Vector3)(+halfSize.X, +halfSize.Y, +halfSize.Z),
                (Vector3h)(Vector3)(+halfSize.X, +halfSize.Y, -halfSize.Z),
                (Vector3h)(Vector3)(-halfSize.X, +halfSize.Y, -halfSize.Z),
                (Vector3h)(Vector3)(-halfSize.X, +halfSize.Y, +halfSize.Z),

                // Bottom
                (Vector3h)(Vector3)(-halfSize.X, -halfSize.Y, -halfSize.Z),
                (Vector3h)(Vector3)(+halfSize.X, -halfSize.Y, -halfSize.Z),
                (Vector3h)(Vector3)(+halfSize.X, -halfSize.Y, +halfSize.Z),
                (Vector3h)(Vector3)(-halfSize.X, -halfSize.Y, +halfSize.Z),
            };

            Span<VertexAttributes> attribs = stackalloc VertexAttributes[]
            {
                // Front
                new VertexAttributes() { Normal = PackedNormal.Pack((0, 0, +1)), Tangent = PackedNormal.Pack((-1, 0, 0)), UVs = (0, 0) },
                new VertexAttributes() { Normal = PackedNormal.Pack((0, 0, +1)), Tangent = PackedNormal.Pack((-1, 0, 0)), UVs = (1, 0) },
                new VertexAttributes() { Normal = PackedNormal.Pack((0, 0, +1)), Tangent = PackedNormal.Pack((-1, 0, 0)), UVs = (1, 1) },
                new VertexAttributes() { Normal = PackedNormal.Pack((0, 0, +1)), Tangent = PackedNormal.Pack((-1, 0, 0)), UVs = (0, 1) },

                // Back
                new VertexAttributes() { Normal = PackedNormal.Pack((0, 0, -1)), Tangent = PackedNormal.Pack((+1, 0, 0)), UVs = (1, 0) },
                new VertexAttributes() { Normal = PackedNormal.Pack((0, 0, -1)), Tangent = PackedNormal.Pack((+1, 0, 0)), UVs = (1, 1) },
                new VertexAttributes() { Normal = PackedNormal.Pack((0, 0, -1)), Tangent = PackedNormal.Pack((+1, 0, 0)), UVs = (0, 1) },
                new VertexAttributes() { Normal = PackedNormal.Pack((0, 0, -1)), Tangent = PackedNormal.Pack((+1, 0, 0)), UVs = (0, 0) },

                // Left
                new VertexAttributes() { Normal = PackedNormal.Pack((+1, 0, 0)), Tangent = PackedNormal.Pack((0, 0, +1)), UVs = (1, 0) },
                new VertexAttributes() { Normal = PackedNormal.Pack((+1, 0, 0)), Tangent = PackedNormal.Pack((0, 0, +1)), UVs = (1, 1) },
                new VertexAttributes() { Normal = PackedNormal.Pack((+1, 0, 0)), Tangent = PackedNormal.Pack((0, 0, +1)), UVs = (0, 1) },
                new VertexAttributes() { Normal = PackedNormal.Pack((+1, 0, 0)), Tangent = PackedNormal.Pack((0, 0, +1)), UVs = (0, 0) },

                // Right
                new VertexAttributes() { Normal = PackedNormal.Pack((-1, 0, 0)), Tangent = PackedNormal.Pack((0, 0, -1)), UVs = (1, 0) },
                new VertexAttributes() { Normal = PackedNormal.Pack((-1, 0, 0)), Tangent = PackedNormal.Pack((0, 0, -1)), UVs = (1, 1) },
                new VertexAttributes() { Normal = PackedNormal.Pack((-1, 0, 0)), Tangent = PackedNormal.Pack((0, 0, -1)), UVs = (0, 1) },
                new VertexAttributes() { Normal = PackedNormal.Pack((-1, 0, 0)), Tangent = PackedNormal.Pack((0, 0, -1)), UVs = (0, 0) },

                // Top
                new VertexAttributes() { Normal = PackedNormal.Pack((0, +1, 0)), Tangent = PackedNormal.Pack((-1, 0, 0)), UVs = (1, 0) },
                new VertexAttributes() { Normal = PackedNormal.Pack((0, +1, 0)), Tangent = PackedNormal.Pack((-1, 0, 0)), UVs = (1, 1) },
                new VertexAttributes() { Normal = PackedNormal.Pack((0, +1, 0)), Tangent = PackedNormal.Pack((-1, 0, 0)), UVs = (0, 1) },
                new VertexAttributes() { Normal = PackedNormal.Pack((0, +1, 0)), Tangent = PackedNormal.Pack((-1, 0, 0)), UVs = (0, 0) },

                // Bottom
                new VertexAttributes() { Normal = PackedNormal.Pack((0, -1, 0)), Tangent = PackedNormal.Pack((-1, 0, 0)), UVs = (0, 0) },
                new VertexAttributes() { Normal = PackedNormal.Pack((0, -1, 0)), Tangent = PackedNormal.Pack((-1, 0, 0)), UVs = (1, 0) },
                new VertexAttributes() { Normal = PackedNormal.Pack((0, -1, 0)), Tangent = PackedNormal.Pack((-1, 0, 0)), UVs = (1, 1) },
                new VertexAttributes() { Normal = PackedNormal.Pack((0, -1, 0)), Tangent = PackedNormal.Pack((-1, 0, 0)), UVs = (0, 1) },
            };

            Span<ushort> indices = stackalloc ushort[]
            {
                // Front
                0, 1, 2, 2, 3, 0,

                // Back
                4, 5, 6, 6, 7, 4,

                // Left
                8, 9, 10, 10, 11, 8,

                // Right
                12, 13, 14, 14, 15, 12,

                // Top
                16, 17, 18, 18, 19, 16,

                // Bottom 
                20, 21, 22, 22, 23, 20,
            };

            Buffer positionBuffer = Buffer.CreateBuffer("Cube_positions", positions, BufferStorageFlags.None);
            Buffer attribBuffer = Buffer.CreateBuffer("Cube_vertexattribs", attribs, BufferStorageFlags.None);
            Buffer indexBuffer = Buffer.CreateBuffer("Cube_index", indices, BufferStorageFlags.None);

            Mesh2 m = new Mesh2(0, indices.Length, DrawElementsType.UnsignedShort, 0, material);
            m.PositionBuffer = positionBuffer;
            m.AttributeBuffer = attribBuffer;
            m.IndexBuffer = indexBuffer;

            m.AABB = new Box3(-halfSize, halfSize);

            return m;
        }
    }
}
