using OpenTK.Graphics.OpenGL4;
using StbImageSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DD2470_Clustered_Volume_Renderer
{
    internal class Texture
    {
        public int Handle;
        public int Width, Height, Depth;
        public SizedInternalFormat Format;

        // FIXME: sRGB? mipmap details? etc

        public Texture(int handle, int width, int height, int depth, SizedInternalFormat format)
        {
            Handle = handle;
            Width = width;
            Height = height;
            Depth = depth;
            Format = format;
        }


        public static Texture LoadTexture(string path, bool hasAlpha, bool srgb)
        {
            ImageResult result = ImageResult.FromStream(File.OpenRead(path), hasAlpha ? ColorComponents.RedGreenBlueAlpha : ColorComponents.RedGreenBlue);

            SizedInternalFormat format;
            switch ((hasAlpha, srgb))
            {
                case (true, true):
                    format = SizedInternalFormat.Srgb8Alpha8;
                    break;
                case (true, false):
                    format = SizedInternalFormat.Rgba8;
                    break;
                case (false, true):
                    format = SizedInternalFormat.Srgb8;
                    break;
                case (false, false):
                    format = SizedInternalFormat.Rgb8;
                    break;
                default: 
                    throw new Exception();
            }

            PixelFormat pixelFormat = hasAlpha ? PixelFormat.Rgba : PixelFormat.Rgb;
            PixelType pixelType = PixelType.UnsignedByte;

            GL.CreateTextures(TextureTarget.Texture2D, 1, out int texture);
            GL.TextureStorage2D(texture,  /*todo!*/1, format, result.Width, result.Height);
            GL.TextureSubImage2D(texture, 0, 0, 0, result.Width, result.Height, pixelFormat, pixelType, result.Data);

            return new Texture(texture, result.Width, result.Height, 1, format);
        }
    }
}
