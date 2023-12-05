using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Assimp;
using OpenTK.Graphics.ES11;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

namespace DD2470_Clustered_Volume_Renderer
{

    internal class Entity
    {
        public string Name;
        public Transform Transform;
        public Entity? Parent;
        public List<Entity> Children;

        public Mesh? Mesh;
        public Buffer IndexBuffer;

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

    internal struct VertexAttributes
    {
        public Vector3 Normal;
        public Vector3 Tangent;
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

    internal static class Model
    {
        public static List<Entity> LoadModel(string modelPath, Shader shader)
        {
            AssimpContext context = new AssimpContext();
            string directory = Path.GetDirectoryName(modelPath)!;

            Stopwatch watch = Stopwatch.StartNew();
            Scene scene = context.ImportFile(modelPath, PostProcessSteps.Triangulate | PostProcessSteps.CalculateTangentSpace);

            watch.Stop();
            Console.WriteLine($"Loading model took: {watch.Elapsed.TotalMilliseconds:0.000}ms");
            Console.WriteLine("Started loading textures...");
            watch.Restart();

            List<Material> materials = new List<Material>();
            foreach (var material in scene.Materials)
            {
                // FIXME: Copy over color stuff.

                Material m = new Material(shader);
                if (material.HasTextureDiffuse)
                {
                    // FIXME: Set filter settings!
                    string path = material.TextureDiffuse.FilePath;
                    string compressed_file = path.Replace("textures", "textures_compressed");
                    compressed_file = Path.ChangeExtension(compressed_file, "dds");
                    m.Albedo = DDSReader.LoadTexture(Path.Combine(directory, compressed_file), true, true);

                    //m.Albedo = Texture.LoadTexture(Path.Combine(directory, material.TextureDiffuse.FilePath), true, true);
                }

                if (material.HasTextureNormal)
                {
                    // FIXME: Set filter settings!
                    string path = material.TextureNormal.FilePath;
                    string compressed_file = path.Replace("textures", "textures_compressed");
                    compressed_file = Path.ChangeExtension(compressed_file, "dds");
                    m.Normal = DDSReader.LoadTexture(Path.Combine(directory, compressed_file), true, false);
                    
                    //m.Normal = Texture.LoadTexture(Path.Combine(directory, material.TextureNormal.FilePath), false, true);
                }
                // FIXME: the sponza we load puts normal maps as dispacement maps...
                else if (material.HasTextureDisplacement)
                {
                    string path = material.TextureDisplacement.FilePath;
                    string compressed_file = path.Replace("textures", "textures_compressed");
                    compressed_file = Path.ChangeExtension(compressed_file, "dds");
                    m.Normal = DDSReader.LoadTexture(Path.Combine(directory, compressed_file), true, false);

                    //m.Normal = Texture.LoadTexture(Path.Combine(directory, material.TextureDisplacement.FilePath), false, true);
                }

                materials.Add(m);
            }

            watch.Stop();
            Console.WriteLine($"Loading textures took: {watch.Elapsed.TotalMilliseconds:0.000}ms");

            List<Mesh> meshes = new List<Mesh>();
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

                Span<Vector3D> positions = CollectionsMarshal.AsSpan(mesh.Vertices);
                Buffer postionBuffer = Buffer.CreateBuffer($"{mesh.Name}_position", positions, BufferStorageFlags.None);

                Span<Vector3D> normals = CollectionsMarshal.AsSpan(mesh.Normals);
                // FIXME: Store the sign of the bitangent in there as well?
                Span<Vector3D> tangents = CollectionsMarshal.AsSpan(mesh.Tangents);
                Span<Vector3D> UVs = CollectionsMarshal.AsSpan(mesh.TextureCoordinateChannels[0]);
                Buffer attributeBuffer = InterleaveBuffers($"{mesh.Name}_vertexattributes", normals, tangents, UVs);

                Span<ushort> indices = MemoryMarshal.Cast<short, ushort>(mesh.GetShortIndices().AsSpan());
                Buffer indexBuffer = Buffer.CreateBuffer($"{mesh.Name}_indices", indices, BufferStorageFlags.None);

                Mesh m = new Mesh(postionBuffer, attributeBuffer, indexBuffer, materials[mesh.MaterialIndex]);
                meshes.Add(m);
                
                static Buffer InterleaveBuffers(string name, Span<Vector3D> normals, Span<Vector3D> tangents, Span<Vector3D> UVs)
                {
                    VertexAttributes[] attributes = new VertexAttributes[normals.Length];

                    for (int i = 0; i < attributes.Length; i++)
                    {
                        attributes[i].Normal = Unsafe.As<Vector3D, Vector3>(ref normals[i]);
                        attributes[i].Tangent = Unsafe.As<Vector3D, Vector3>(ref tangents[i]);
                        attributes[i].UVs = Unsafe.As<Vector3D, Vector3>(ref UVs[i]).Xy;
                    }

                    return Buffer.CreateBuffer(name, attributes, BufferStorageFlags.None);
                }
            }

            List<Entity> entities = new List<Entity>();
            ProcessNode(scene.RootNode, null, entities, meshes);
            return entities;

            static Entity ProcessNode(Node node, Entity? parent, List<Entity> entities, List<Mesh> meshes)
            {
                //Console.WriteLine($"Name: {node.Name}");
                //Console.WriteLine($"  Has mesh: {node.HasMeshes}{(node.HasMeshes ? $" ({string.Join(", ", node.MeshIndices)})" : "")}");
                //Console.WriteLine($"  Children: {node.ChildCount}");
                var nodeTransform = node.Transform;
                Matrix4 matrix = Unsafe.As<Matrix4x4, Matrix4>(ref nodeTransform);
                //matrix.Transpose();
                //Console.WriteLine($"  Transform: \n{matrix}");

                Vector3 position = matrix.ExtractTranslation();
                Vector3 scale = matrix.ExtractScale() * (1/2f);
                OpenTK.Mathematics.Quaternion rotation = matrix.ExtractRotation();

                Transform transform = new Transform(rotation, position, scale);

                // For now we only support one mesh per node
                Debug.Assert(node.MeshCount <= 1);

                Mesh? mesh = null;
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
    }
}
