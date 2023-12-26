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
        public Shader? PrepassShader;
        // FIXME: Uniform data stuff..

        public Texture? Albedo;
        public Texture? Normal;
        public Texture? RoughnessMetallic;

        public Material(Shader shader, Shader? prepassShader)
        {
            Shader = shader;
            PrepassShader = prepassShader;
        }

        public static int Compare(Material? mat1, Material? mat2)
        {
            if (mat1 == null && mat2 == null) return 0;
            if (mat1 == null) return 1;
            if (mat2 == null) return -1;

            int comp = mat1.Shader.Handle.CompareTo(mat2.Shader.Handle);
            if (comp != 0) return comp;

            comp = (mat1.PrepassShader?.Handle ?? 0).CompareTo(mat2.PrepassShader?.Handle ?? 0);
            if (comp != 0) return comp;

            comp = (mat1.Albedo?.Handle ?? 0).CompareTo(mat2.Albedo?.Handle ?? 0);
            if (comp != 0) return comp;

            comp = (mat1.Normal?.Handle ?? 0).CompareTo(mat2.Normal?.Handle ?? 0);
            if (comp != 0) return comp;

            comp = (mat1.RoughnessMetallic?.Handle ?? 0).CompareTo(mat2.RoughnessMetallic?.Handle ?? 0);
            if (comp != 0) return comp;

            return 0;
        }
    }
}
