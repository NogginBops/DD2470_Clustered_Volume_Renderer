using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Assimp;
using OpenTK.Graphics.ES11;
using OpenTK.Mathematics;

namespace DD2470_Clustered_Volume_Renderer
{
    internal class Transform
    {
        public OpenTK.Mathematics.Quaternion Rotation;
        public Vector3 Position;
        public Vector3 Scale;

        // FIXME: Parent and child relations...

        public Transform(OpenTK.Mathematics.Quaternion rotation, Vector3 position, Vector3 scale)
        {
            Rotation = rotation;
            Position = position;
            Scale = scale;
        }

        public override string ToString()
        {
            return $"T: {Position}, R: ({Rotation}), S: {Scale}";
        }
    }

    internal class Entity
    {
        public string Name;
        public Transform Transform;
        public Entity? Parent;
        public List<Entity> Children;

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
        public Vector2 UVs;
    }

    // Contains vertex data
    internal class Mesh
    {
        public Buffer PositionBuffer;
        public Buffer AttributeBuffer;

        public Mesh(Buffer positionBuffer, Buffer attributeBuffer)
        {
            PositionBuffer = positionBuffer;
            AttributeBuffer = attributeBuffer;
        }
    }

    internal static class Model
    {
        public static List<Entity> LoadModel(string modelPath)
        {
            AssimpContext context = new AssimpContext();

            Scene scene = context.ImportFile(modelPath, PostProcessSteps.Triangulate);

            List<Mesh> meshes = new List<Mesh>();
            foreach (var mesh in scene.Meshes)
            {
                Console.WriteLine($"Mesh: {mesh.Name}");
                Console.WriteLine($"  Has positions: {mesh.HasVertices}");
                Console.WriteLine($"  Has normals: {mesh.HasNormals}");
                Console.WriteLine($"  Has tangent: {mesh.HasTangentBasis}");
                for (int i = 0; i < mesh.TextureCoordinateChannelCount; i++)
                {
                    Console.WriteLine($"  Has UV{i}: {mesh.HasTextureCoords(i)}");
                }

                Span<Vector3D> positions = CollectionsMarshal.AsSpan(mesh.Vertices);
                Buffer postionBuffer = Buffer.CreateBuffer(positions, OpenTK.Graphics.OpenGL4.BufferStorageFlags.None);

                Span<Vector3D> normals = CollectionsMarshal.AsSpan(mesh.Normals);
                // FIXME: Tangents?
                //Span<Vector3D> tangents = CollectionsMarshal.AsSpan(mesh.Tangents);
                Span<Vector3D> UVs = CollectionsMarshal.AsSpan(mesh.TextureCoordinateChannels[0]);
                Buffer attributeBuffer = InterleaveBuffers(normals, UVs);

                Mesh m = new Mesh(postionBuffer, attributeBuffer);
                meshes.Add(m);

                static Buffer InterleaveBuffers(Span<Vector3D> normals, Span<Vector3D> UVs)
                {
                    VertexAttributes[] attributes = new VertexAttributes[normals.Length];

                    for (int i = 0; i < attributes.Length; i++)
                    {
                        attributes[i].Normal = Unsafe.As<Vector3D, Vector3>(ref normals[i]);
                        attributes[i].UVs = Unsafe.As<Vector3D, Vector3>(ref UVs[i]).Xy;
                    }

                    return Buffer.CreateBuffer(attributes, OpenTK.Graphics.OpenGL4.BufferStorageFlags.None);
                }
            }

            List<Entity> entities = new List<Entity>();
            ProcessNode(scene.RootNode, null, entities);
            return entities;

            static Entity ProcessNode(Node node, Entity? parent, List<Entity> entities)
            {
                Console.WriteLine($"Name: {node.Name}");
                Console.WriteLine($"  Has mesh: {node.HasMeshes}{(node.HasMeshes ? $" ({string.Join(", ", node.MeshIndices)})" : "")}");
                Console.WriteLine($"  Children: {node.ChildCount}");
                var nodeTransform = node.Transform;
                Matrix4 matrix = Unsafe.As<Matrix4x4, Matrix4>(ref nodeTransform);
                Console.WriteLine($"  Transform: \n{matrix}");

                Vector3 position = matrix.ExtractTranslation();
                Vector3 scale = matrix.ExtractScale();
                OpenTK.Mathematics.Quaternion rotation = matrix.ExtractRotation();

                Transform transform = new Transform(rotation, position, scale);
                
                

                // FIXME: Decide if need really need to make an entity here.
                // FIXME: Parent
                Entity ent = new Entity(node.Name, transform, parent);

                foreach (Node child in node.Children)
                {
                    Entity childEnt = ProcessNode(child, ent, entities);
                    ent.Children.Add(childEnt);
                }

                entities.Add(ent);
                return ent;
            }
        }
    }
}
