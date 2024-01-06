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

        public Shader ClusteredShader;
        public Shader? ClusteredPrepassShader;

        // FIXME: Uniform data stuff..

        public Texture? Albedo;
        public Texture? Normal;
        public Texture? RoughnessMetallic;

        public Material(Shader shader, Shader? prepassShader, Shader clusteredShader, Shader? clusteredPrepassShader)
        {
            Shader = shader;
            PrepassShader = prepassShader;
            ClusteredShader = clusteredShader;
            ClusteredPrepassShader = clusteredPrepassShader;
        }

        public Shader GetShader(RenderPath renderpath)
        {
            switch (renderpath)
            {
                case RenderPath.ForwardPath:
                    return Shader;
                case RenderPath.ClusteredForwardPath:
                    return ClusteredShader;
                default: 
                    throw new Exception();
            }
        }

        public Shader GetPrepassShader(RenderPath renderpath)
        {
            switch (renderpath)
            {
                case RenderPath.ForwardPath:
                    return PrepassShader ?? Shader;
                case RenderPath.ClusteredForwardPath:
                    return ClusteredPrepassShader ?? ClusteredShader;
                default:
                    throw new Exception();
            }
        }

        public static int Compare(Material? mat1, Material? mat2, RenderPath renderpath)
        {
            if (mat1 == null && mat2 == null) return 0;
            if (mat1 == null) return 1;
            if (mat2 == null) return -1;

            int comp;

            comp = mat1.GetShader(renderpath).Handle.CompareTo(mat2.GetShader(renderpath));
            if (comp != 0) return comp;

            comp = mat1.GetPrepassShader(renderpath).Handle.CompareTo(mat2.GetPrepassShader(renderpath));
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
