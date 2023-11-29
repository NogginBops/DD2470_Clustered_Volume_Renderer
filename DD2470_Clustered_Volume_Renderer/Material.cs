using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DD2470_Clustered_Volume_Renderer
{
    internal class Material
    {
        public Shader Shader;
        // FIXME: Uniform data stuff..

        public Texture? Albedo;
        public Texture? Normal;
        public Texture? Roughness;

        public Material(Shader shader)
        {
            Shader = shader;
        }
    }
}
