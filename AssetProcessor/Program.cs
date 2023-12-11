using Assimp;
using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace AssetProcessor
{
    internal class Program
    {
        public struct VertexAttributes
        {
            public Vector3 Normal;
            public Vector3 Tangent;
            public Vector2 UVs;
        }

        public struct VertexAttributes2
        {
            public PackedNormal Normal;
            public PackedNormal Tangent;
            public Vector2 UVs;
        }

        /// <summary>
        /// A 2_10_10_10 packed normal
        /// </summary>
        public struct PackedNormal
        {
            public uint Normal;

            public PackedNormal(Vector3 normal)
            {
                Normal = PackNormal(normal);
            }

            public static uint PackNormal(Vector3 normal)
            {
                // FIXME: Make full use of the -2^9, 2^9 - 1 range?
                uint x = (uint)((int)(normal.X * 511) & 0x000003FF);
                uint y = (uint)((int)(normal.Y * 511) & 0x000003FF);
                uint z = (uint)((int)(normal.Z * 511) & 0x000003FF);
                uint w = 0b01;
                return w << 30 | z << 20 | y << 10 | x;
            }

            public static Vector3 UnpackNormal(uint normal)
            {
                // FIXME: Make full use of the -2^9, 2^9 - 1 range?
                int x = ((int)(((normal >> 00) & 0x03FF) << 22)) >> 22;
                int y = ((int)(((normal >> 10) & 0x03FF) << 22)) >> 22;
                int z = ((int)(((normal >> 20) & 0x03FF) << 22)) >> 22;
                
                return new Vector3(x / 511f, y / 511f, z / 511f);
            }

            public override string ToString()
            {
                return $"{UnpackNormal(Normal)}";
            }
        }

        public struct MeshData
        {
            public Vector3h[] Positions;
            public VertexAttributes[] Attribs;

            public MeshData(Vector3h[] positions, VertexAttributes[] attribs)
            {
                Positions = positions;
                Attribs = attribs;
            }
        }

        public static unsafe void Main()
        {
            /*
            rdo_bc_params @params = new rdo_bc_params();
            @params.m_y_flip = true;
            @params.m_generate_mipmaps = true;
            @params.m_status_output = true;

            bc7enc_error error = Bc7Enc.compress_image_from_file("C:\\Users\\juliu\\Documents\\GitHub\\KTH\\DD2470_Clustered_Volume_Renderer\\DD2470_Clustered_Volume_Renderer\\Assets\\Sponza\\textures_png\\background.png", @params, out encode_output output);

            Console.WriteLine($"Error: {error}");
            if (error == bc7enc_error.success)
            {
                Console.WriteLine($"Width: {output.width}, Height: {output.height}");
                Console.WriteLine($"Mips: {output.mipmap_count}");
                Console.WriteLine($"Total blocks: {output.num_blocks}");
                Console.WriteLine($"Block size: {output.bytes_per_block}");
            }
            */

            /*Vector3[] vectors = new Vector3[12];
            Random rand = new Random();
            for (int i = 0; i < vectors.Length; i++)
            {
                vectors[i].X = i % 3;
                vectors[i].Y = i % 2;
            }

            int[] attrib = new int[vectors.Length];
            attrib[3] = 3;
            attrib[7] = 1;

            uint[] remap = new uint[vectors.Length];

            ulong count = MeshOptimizer.GenerateVertexRemapMulti<Vector3, int>(remap, new ReadOnlySpan<uint>(null, vectors.Length), (ulong)vectors.Length, vectors, attrib);

            ;*/

            //

            //ConvertObj("C:\\Users\\juliu\\Documents\\GitHub\\KTH\\DD2470_Clustered_Volume_Renderer\\DD2470_Clustered_Volume_Renderer\\Assets\\temple\\temple.gltf");
            ConvertObj("C:\\Users\\juliu\\Documents\\GitHub\\KTH\\DD2470_Clustered_Volume_Renderer\\DD2470_Clustered_Volume_Renderer\\Assets\\Sponza\\sponza.obj");
        }

        public static unsafe void ConvertObj(string path)
        {
            AssimpContext context = new AssimpContext();

            Scene scene = context.ImportFile(path, PostProcessSteps.Triangulate | PostProcessSteps.CalculateTangentSpace);

            foreach (var mesh in scene.Meshes)
            {
                Vector3h[] positionsHalf = new Vector3h[mesh.VertexCount];
                Span<Vector3D> data = CollectionsMarshal.AsSpan(mesh.Vertices);
                for (int i = 0; i < data.Length; i++)
                {
                    // FIXME: Is just a cast what we want to do here?
                    positionsHalf[i] = (Vector3h)Unsafe.As<Vector3D, Vector3>(ref data[i]);
                }

                Span<Vector3D> normals = CollectionsMarshal.AsSpan(mesh.Normals);
                // FIXME: Store the sign of the bitangent in there as well?
                Span<Vector3D> tangents = CollectionsMarshal.AsSpan(mesh.Tangents);
                Span<Vector3D> UVs = CollectionsMarshal.AsSpan(mesh.TextureCoordinateChannels[0]);
                VertexAttributes2[] attributes2 = InterleaveBuffers2(normals, tangents, UVs);

                uint[] indices = mesh.GetUnsignedIndices();
                var fetchStatsUnopt = MeshOptimizer.AnalyzeVertexFetch(indices, (ulong)positionsHalf.Length, (ulong)(sizeof(Vector3h) + sizeof(VertexAttributes2)));

                Vector3h[] newPositions = new Vector3h[positionsHalf.Length];
                VertexAttributes2[] newAttributes = new VertexAttributes2[positionsHalf.Length];
                uint[] remapTable = new uint[positionsHalf.Length];
                ulong unique_vertices = MeshOptimizer.OptimizeVertexFetchRemap(remapTable, indices, (ulong)positionsHalf.Length);
                MeshOptimizer.RemapVertexBuffer<Vector3h>(newPositions, positionsHalf, remapTable);
                MeshOptimizer.RemapVertexBuffer<VertexAttributes2>(newAttributes, attributes2, remapTable);

                uint[] newIndices = new uint[indices.Length];
                MeshOptimizer.RemapIndexBuffer(newIndices, indices, remapTable);

                var fetchStatsOpt = MeshOptimizer.AnalyzeVertexFetch(newIndices, (ulong)newPositions.Length, (ulong)(sizeof(Vector3h) + sizeof(VertexAttributes2)));

                Console.WriteLine($"Mesh: {mesh.Name}");
                Console.WriteLine($"Bytes fetched: {fetchStatsUnopt.bytes_fetched} (unopt)");
                Console.WriteLine($"Overfetched: {fetchStatsUnopt.overfetch} (unopt)");
                Console.WriteLine($"Bytes fetched: {fetchStatsOpt.bytes_fetched} (opt)");
                Console.WriteLine($"Overfetched: {fetchStatsOpt.overfetch} (opt)");
                Console.WriteLine();
            }
        }

        public static VertexAttributes[] InterleaveBuffers(Span<Vector3D> normals, Span<Vector3D> tangents, Span<Vector3D> UVs)
        {
            VertexAttributes[] attributes = new VertexAttributes[normals.Length];

            for (int i = 0; i < attributes.Length; i++)
            {
                attributes[i].Normal = Unsafe.As<Vector3D, Vector3>(ref normals[i]);
                attributes[i].Tangent = Unsafe.As<Vector3D, Vector3>(ref tangents[i]);
                attributes[i].UVs = Unsafe.As<Vector3D, Vector3>(ref UVs[i]).Xy;
            }

            return attributes;
        }

        public static VertexAttributes2[] InterleaveBuffers2(Span<Vector3D> normals, Span<Vector3D> tangents, Span<Vector3D> UVs)
        {
            VertexAttributes2[] attributes = new VertexAttributes2[normals.Length];

            for (int i = 0; i < attributes.Length; i++)
            {
                attributes[i].Normal = new PackedNormal(Unsafe.As<Vector3D, Vector3>(ref normals[i]));
                attributes[i].Tangent = new PackedNormal(Unsafe.As<Vector3D, Vector3>(ref tangents[i]));
                attributes[i].UVs = Unsafe.As<Vector3D, Vector3>(ref UVs[i]).Xy;
            }

            return attributes;
        }
    }
}
