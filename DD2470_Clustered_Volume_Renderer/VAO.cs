using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DD2470_Clustered_Volume_Renderer
{
    internal struct BufferBinding
    {
        public Buffer? Buffer;
        public int Offset;
        public int Stride;
    }

    internal class VAO
    {
        public int Handle;

        public Buffer? ElementBuffer;

        public Graphics.VertexAttribute[] Attributes = new Graphics.VertexAttribute[Graphics.MinSupportedVertexAttributes];
        public BufferBinding[] BufferBindings = new BufferBinding[Graphics.MinSupportedVertexAttributeBindings];

        public int[] AttributeToBufferLinks = new int[Graphics.MinSupportedVertexAttributeBindings];

        public VAO(int handle)
        {
            Handle = handle;
        }
    }
}
