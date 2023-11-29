using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using StbImageSharp;
using System;
using System.Collections.Generic;
using System.IO;
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


        public static Texture LoadTexture(string path, bool srgb, bool generateMipmap)
        {
            StbImage.stbi_set_flip_vertically_on_load(1);
            ImageResult result = ImageResult.FromStream(File.OpenRead(path), ColorComponents.RedGreenBlueAlpha);

            int mipmapLevels = generateMipmap ?
                MathF.ILogB(Math.Max(result.Width, result.Height)) :
                1;

            SizedInternalFormat format = srgb ? SizedInternalFormat.Srgb8Alpha8 : SizedInternalFormat.Rgba8;

            PixelFormat pixelFormat = PixelFormat.Rgba;
            PixelType pixelType = PixelType.UnsignedByte;

            GL.CreateTextures(TextureTarget.Texture2D, 1, out int texture);

            string name = Path.GetRelativePath(Directory.GetCurrentDirectory(), path);
            GL.ObjectLabel(ObjectLabelIdentifier.Texture, texture, -1, name);

            GL.TextureStorage2D(texture, mipmapLevels, format, result.Width, result.Height);
            GL.TextureSubImage2D(texture, 0, 0, 0, result.Width, result.Height, pixelFormat, pixelType, result.Data);

            if (generateMipmap)
            {
                GL.GenerateTextureMipmap(texture);
            }
            else
            {
                // FIXME: Set the texture filtering properties for non-mipmap textures!
            }
            
            return new Texture(texture, result.Width, result.Height, 1, format);
        }

        public static unsafe Texture FromColor(Color4 color, bool srgb)
        {
            SizedInternalFormat format = srgb ? SizedInternalFormat.Srgb8Alpha8 : SizedInternalFormat.Rgba8;

            PixelFormat pixelFormat = PixelFormat.Rgba;
            PixelType pixelType = PixelType.Float;

            GL.CreateTextures(TextureTarget.Texture2D, 1, out int texture);
            GL.TextureStorage2D(texture, 1, format, 1, 1);
            GL.TextureSubImage2D(texture, 0, 0, 0, 1, 1, pixelFormat, pixelType, (nint)(Color4*)&color);

            return new Texture(texture, 1, 1, 1, format);
        }
    }
}
