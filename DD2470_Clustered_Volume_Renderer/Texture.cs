using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using StbImageSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
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

        // FIXME: Get name from path?
        public static Texture LoadTexture(string name, DDSImage image, bool generateMipmap)
        {
            DDSImageRef imageRef;
            imageRef.Width = image.Width;
            imageRef.Height = image.Height;
            imageRef.MipmapCount = image.MipmapCount;
            imageRef.Format = image.Format;
            imageRef.AllData = image.AllData;
            Texture texture = LoadTexture(name, imageRef, generateMipmap);
            return texture;
        }

        public static unsafe Texture LoadTexture(string name, DDSImageRef image, bool generateMipmap)
        {
            int mipmapLevels = generateMipmap ?
                MathF.ILogB(Math.Max(image.Width, image.Height)) :
                image.MipmapCount;

            int imageMips = Math.Min(mipmapLevels, image.MipmapCount);

            SizedInternalFormat format;
            switch (image.Format)
            {
                case DDSImageFormat.BC5_SNORM:
                    format = (SizedInternalFormat)All.CompressedRgRgtc2;
                    break;
                case DDSImageFormat.BC7_UNORM:
                    // FIXME: Is this supposed to be sRGB?
                    format = (SizedInternalFormat)All.CompressedRgbaBptcUnorm;
                    break;
                case DDSImageFormat.BC7_UNORM_SRGB:
                    // FIXME: Is this supposed to be sRGB?
                    format = (SizedInternalFormat)All.CompressedSrgbAlphaBptcUnorm;
                    break;
                default:
                    throw new Exception();
            }

            GL.CreateTextures(TextureTarget.Texture2D, 1, out int texture);

            GL.ObjectLabel(ObjectLabelIdentifier.Texture, texture, -1, name);

            GL.TextureStorage2D(texture, mipmapLevels, format, image.Width, image.Height);

            int dataOffset = 0;
            int mipWidth = image.Width;
            int mipHeight = image.Height;
            for (int i = 0; i < imageMips; i++)
            {
                int dataLength = mipWidth * mipHeight;
                GL.CompressedTextureSubImage2D(texture, i, 0, 0, mipWidth, mipHeight, (PixelFormat)format, dataLength, (nint)Unsafe.AsPointer(ref image.AllData[dataOffset]));
                dataOffset += mipWidth * mipHeight;
                mipWidth = Math.Max(1, mipWidth / 2);
                mipHeight = Math.Max(1, mipHeight / 2);
            }

            // Only generate mipmaps of the texture didn't have some levels
            if (generateMipmap && mipmapLevels != imageMips)
            {
                GL.GenerateTextureMipmap(texture);
            }
            else
            {
                // FIXME: Set the texture filtering properties for non-mipmap textures!
            }

            return new Texture(texture, image.Width, image.Height, 1, format);
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

        public static unsafe Texture FromImage(string name, ImageResult image, bool srgb, bool generateMipmap)
        {
            int mipmapLevels = generateMipmap ?
                MathF.ILogB(Math.Max(image.Width, image.Height)) :
                1;

            SizedInternalFormat format = srgb ? SizedInternalFormat.Srgb8Alpha8 : SizedInternalFormat.Rgba8;

            PixelFormat pixelFormat = PixelFormat.Rgba;
            PixelType pixelType = PixelType.UnsignedByte;

            GL.CreateTextures(TextureTarget.Texture2D, 1, out int texture);
            GL.ObjectLabel(ObjectLabelIdentifier.Texture, texture, -1, name);

            GL.TextureStorage2D(texture, mipmapLevels, format, image.Width, image.Height);
            GL.TextureSubImage2D(texture, 0, 0, 0, image.Width, image.Height, pixelFormat, pixelType, image.Data);

            if (generateMipmap)
            {
                GL.GenerateTextureMipmap(texture);
            }
            else
            {
                // FIXME: Set the texture filtering properties for non-mipmap textures!
            }

            return new Texture(texture, image.Width, image.Height, 1, format);
        }

        public static Texture CreateEmpty2D(string name, int width, int height, SizedInternalFormat format)
        {
            GL.CreateTextures(TextureTarget.Texture2D, 1, out int texture);
            GL.ObjectLabel(ObjectLabelIdentifier.Texture, texture, -1, name);

            GL.TextureStorage2D(texture, 1, format, width, height);

            return new Texture(texture, width, height, 1, format);
        }

        public void SetFilter(TextureMinFilter minFilter, TextureMagFilter magFilter)
        {
            GL.TextureParameter(Handle, TextureParameterName.TextureMinFilter, (int)minFilter);
            GL.TextureParameter(Handle, TextureParameterName.TextureMagFilter, (int)magFilter);
        }
    }
}
