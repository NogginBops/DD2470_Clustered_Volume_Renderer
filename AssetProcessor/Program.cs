using Assimp;
using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AssetProcessor
{
    internal class Program
    {
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

            Vector3[] vectors = new Vector3[12];
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

            ;
        }

        public void ConvertObj(string path)
        {
            AssimpContext context = new AssimpContext();

            Scene scene = context.ImportFile(path, PostProcessSteps.Triangulate | PostProcessSteps.CalculateTangentSpace);

            
        }
    }
}
