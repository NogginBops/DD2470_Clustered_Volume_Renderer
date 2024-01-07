using OpenTK.Audio.OpenAL;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using StbImageSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace DD2470_Clustered_Volume_Renderer
{
    internal class Texture
    {
        private static ReadOnlySpan<DDSCubemapFaces> OpenGLCubemapFaceOrder => [
            DDSCubemapFaces.PosX, 
            DDSCubemapFaces.NegX,
            DDSCubemapFaces.PosY,
            DDSCubemapFaces.NegY,
            DDSCubemapFaces.PosZ,
            DDSCubemapFaces.NegZ,
        ];

        public int Handle;
        public int Width, Height, Depth;
        public SizedInternalFormat Format;
        public int MipCount;

        // FIXME: sRGB? mipmap details? etc

        public Texture(int handle, int width, int height, int depth, int mipCount, SizedInternalFormat format)
        {
            Handle = handle;
            Width = width;
            Height = height;
            Depth = depth;
            MipCount = mipCount;
            Format = format;
        }


        public static Texture LoadTexture(string path, bool srgb, bool generateMipmap)
        {
            StbImage.stbi_set_flip_vertically_on_load(1);
            ImageResult result = ImageResult.FromStream(File.OpenRead(path), ColorComponents.RedGreenBlueAlpha);

            int mipmapLevels = generateMipmap ?
                MathF.ILogB(Math.Max(result.Width, result.Height)) + 1 :
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
            
            return new Texture(texture, result.Width, result.Height, 1, mipmapLevels, format);
        }

        public static Texture LoadHDRITexture(string path, bool generateMipmap)
        {
            StbImage.stbi_set_flip_vertically_on_load(1);
            ImageResultFloat result = ImageResultFloat.FromStream(File.OpenRead(path), ColorComponents.RedGreenBlue);

            int mipmapLevels = generateMipmap ?
                MathF.ILogB(Math.Max(result.Width, result.Height)) + 1 :
                1;

            // FIXME: Make the texture 16F format, then compress it using BC6H...
            // Could also look at rgb9e5 format.
            SizedInternalFormat format = SizedInternalFormat.Rgb32f;

            PixelFormat pixelFormat = PixelFormat.Rgb;
            PixelType pixelType = PixelType.Float;

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

            return new Texture(texture, result.Width, result.Height, 1, mipmapLevels, format);
        }

        public static Texture LoadHDRICubeMapTexture(string directory, bool generateMipmap)
        {
            // This is the OpenGL layer order for cubemaps.
            ReadOnlySpan<string> faces = ["px", "nx", "py", "ny", "pz", "nz"];

            // To get the faces in the correct orientation we do this..?
            StbImage.stbi_set_flip_vertically_on_load(0);

            GL.CreateTextures(TextureTarget.TextureCubeMap, 1, out int texture);
            string name = Path.GetRelativePath(Directory.GetCurrentDirectory(), directory);
            GL.ObjectLabel(ObjectLabelIdentifier.Texture, texture, -1, name);

            bool hasStorage = false;

            // FIXME: Make the texture 16F format, then compress it using BC6H...
            // Could also look at rgb9e5 format.
            SizedInternalFormat format = SizedInternalFormat.Rgb32f;

            PixelFormat pixelFormat = PixelFormat.Rgb;
            PixelType pixelType = PixelType.Float;

            int mipmapLevels = 1;
            int width = 0;
            int height = 0;

            for (int i = 0; i < faces.Length; i++)
            {
                string face = faces[i];

                string path = Path.Combine(directory, $"{face}.hdr");

                using Stream stream = File.OpenRead(path);
                ImageResultFloat result = ImageResultFloat.FromStream(stream, ColorComponents.RedGreenBlue);

                // FIXME: Assert that the dimentions don't change between faces?
                width = result.Width;
                height = result.Height;

                if (hasStorage == false)
                {
                    mipmapLevels = generateMipmap ? MathF.ILogB(Math.Max(result.Width, result.Height)) + 1 : 1;
                    GL.TextureStorage2D(texture, mipmapLevels, format, result.Width, result.Height);
                    hasStorage = true;
                }

                GL.TextureSubImage3D(texture, 0, 0, 0, i, result.Width, result.Height, 1, pixelFormat, pixelType, result.Data);
            }

            if (generateMipmap)
            {
                GL.GenerateTextureMipmap(texture);
            }
            else
            {
                // FIXME: Set the texture filtering properties for non-mipmap textures!
            }

            return new Texture(texture, width, height, 6, mipmapLevels, format);
        }

        // FIXME: Get name from path?
        public static Texture LoadTexture(string name, DDSImage image, bool generateMipmap)
        {
            DDSImageRef imageRef;
            imageRef.Width = image.Width;
            imageRef.Height = image.Height;
            imageRef.MipmapCount = image.MipmapCount;
            imageRef.Format = image.Format;
            imageRef.Type = image.Type;
            imageRef.Faces = image.Faces;

            imageRef.AllData = image.AllData;
            Texture texture = LoadTexture(name, imageRef, generateMipmap);
            return texture;
        }

        public static unsafe Texture LoadTexture(string name, DDSImageRef image, bool generateMipmap)
        {
            int mipmapLevels = generateMipmap ?
                MathF.ILogB(Math.Max(image.Width, image.Height)) + 1 :
                image.MipmapCount;
            
            int imageMips = Math.Min(mipmapLevels, image.MipmapCount);

            int depth;
            TextureTarget target;
            switch (image.Type)
            {
                case DDSImageType.Texture2D:
                    target = TextureTarget.Texture2D;
                    depth = 1;
                    break;
                case DDSImageType.CubeMap:
                    target = TextureTarget.TextureCubeMap;
                    depth = 6;
                    break;
                default:
                    throw new Exception($"Unknown texture type: '{image.Type}'");
            }

            int bytesPerPixel;
            SizedInternalFormat internalFormat;
            PixelFormat pixelFormat = 0;
            PixelType pixelType = 0;
            bool compressed;
            switch (image.Format)
            {
                case DDSImageFormat.BC5_UNORM:
                    // FIXME: Should it be unsigned or signed?
                    internalFormat = (SizedInternalFormat)All.CompressedRgRgtc2;
                    compressed = true;
                    bytesPerPixel = 1;
                    break;
                case DDSImageFormat.BC7_UNORM:
                    // FIXME: Is this supposed to be sRGB?
                    internalFormat = (SizedInternalFormat)All.CompressedRgbaBptcUnorm;
                    compressed = true;
                    bytesPerPixel = 1;
                    break;
                case DDSImageFormat.BC7_UNORM_SRGB:
                    // FIXME: Is this supposed to be sRGB?
                    internalFormat = (SizedInternalFormat)All.CompressedSrgbAlphaBptcUnorm;
                    compressed = true;
                    bytesPerPixel = 1;
                    break;
                case DDSImageFormat.RGBA16F:
                    internalFormat = SizedInternalFormat.Rgba16f;
                    pixelFormat = PixelFormat.Rgba;
                    pixelType = PixelType.HalfFloat;
                    compressed = false;
                    bytesPerPixel = 8;
                    break;
                case DDSImageFormat.RGBA32F:
                    internalFormat = SizedInternalFormat.Rgba32f;
                    pixelFormat = PixelFormat.Rgba;
                    pixelType = PixelType.Float;
                    compressed = false;
                    bytesPerPixel = 16;
                    break;
                default:
                    throw new Exception();
            }

            GL.CreateTextures(target, 1, out int texture);
            GL.ObjectLabel(ObjectLabelIdentifier.Texture, texture, -1, name);

            GL.TextureStorage2D(texture, mipmapLevels, internalFormat, image.Width, image.Height);

            if (image.Type == DDSImageType.CubeMap)
            {
                int dataOffset = 0;
                int layer = -1;
                foreach (var face in OpenGLCubemapFaceOrder)
                {
                    layer++;
                    if (image.Faces.HasFlag(face) == false)
                        continue;

                    int mipWidth = image.Width;
                    int mipHeight = image.Height;
                    for (int i = 0; i < imageMips; i++)
                    {
                        int dataLength = mipWidth * mipHeight * bytesPerPixel;
                        if (compressed)
                        {
                            GL.CompressedTextureSubImage3D(texture, i, 0, 0, layer, mipWidth, mipHeight, 1, (PixelFormat)internalFormat, dataLength, (nint)Unsafe.AsPointer(ref image.AllData[dataOffset]));
                        }
                        else
                        {
                            GL.TextureSubImage3D(texture, i, 0, 0, layer, mipWidth, mipHeight, 1, pixelFormat, pixelType, (nint)Unsafe.AsPointer(ref image.AllData[dataOffset]));
                        }
                        dataOffset += dataLength;
                        mipWidth = Math.Max(1, mipWidth / 2);
                        mipHeight = Math.Max(1, mipHeight / 2);
                    }
                }
            }
            else
            {
                int dataOffset = 0;
                int mipWidth = image.Width;
                int mipHeight = image.Height;
                for (int i = 0; i < imageMips; i++)
                {
                    int dataLength = mipWidth * mipHeight;
                    if (compressed)
                    {
                        GL.CompressedTextureSubImage2D(texture, i, 0, 0, mipWidth, mipHeight, (PixelFormat)internalFormat, dataLength, (nint)Unsafe.AsPointer(ref image.AllData[dataOffset]));
                    }
                    else
                    {
                        GL.TextureSubImage2D(texture, i, 0, 0, mipWidth, mipHeight, pixelFormat, pixelType, (nint)Unsafe.AsPointer(ref image.AllData[dataOffset]));
                    }
                    dataOffset += mipWidth * mipHeight;
                    mipWidth = Math.Max(1, mipWidth / 2);
                    mipHeight = Math.Max(1, mipHeight / 2);
                }
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

            return new Texture(texture, image.Width, image.Height, depth, mipmapLevels, internalFormat);
        }

        public static unsafe Texture FromColor(Color4 color, bool srgb)
        {
            SizedInternalFormat format = srgb ? SizedInternalFormat.Srgb8Alpha8 : SizedInternalFormat.Rgba8;

            PixelFormat pixelFormat = PixelFormat.Rgba;
            PixelType pixelType = PixelType.Float;

            GL.CreateTextures(TextureTarget.Texture2D, 1, out int texture);
            GL.TextureStorage2D(texture, 1, format, 1, 1);
            GL.TextureSubImage2D(texture, 0, 0, 0, 1, 1, pixelFormat, pixelType, (nint)(Color4*)&color);

            return new Texture(texture, 1, 1, 1, 1, format);
        }

        public static unsafe Texture FromImage(string name, ImageResult image, bool srgb, bool generateMipmap)
        {
            int mipmapLevels = generateMipmap ?
                MathF.ILogB(Math.Max(image.Width, image.Height)) + 1 :
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

            return new Texture(texture, image.Width, image.Height, 1, mipmapLevels, format);
        }

        public static Texture CreateEmpty2D(string name, int width, int height, SizedInternalFormat format, bool hasMips)
        {
            GL.CreateTextures(TextureTarget.Texture2D, 1, out int texture);
            GL.ObjectLabel(ObjectLabelIdentifier.Texture, texture, -1, name);

            int mipmapLevels = hasMips ? MathF.ILogB(Math.Max(width, height)) + 1 : 1;

            GL.TextureStorage2D(texture, mipmapLevels, format, width, height);

            return new Texture(texture, width, height, 1, mipmapLevels, format);
        }

        public static Texture CreateEmpty3D(string name, int width, int height, int depth, SizedInternalFormat format)
        {
            GL.CreateTextures(TextureTarget.Texture3D, 1, out int texture);
            GL.ObjectLabel(ObjectLabelIdentifier.Texture, texture, -1, name);

            GL.TextureStorage3D(texture, 1, format, width, height, depth);

            return new Texture(texture, width, height, depth, 1, format);
        }

        public void SetFilter(TextureMinFilter minFilter, TextureMagFilter magFilter)
        {
            GL.TextureParameter(Handle, TextureParameterName.TextureMinFilter, (int)minFilter);
            GL.TextureParameter(Handle, TextureParameterName.TextureMagFilter, (int)magFilter);
        }

        public void SetWrap(TextureWrapMode wrapR, TextureWrapMode wrapS, TextureWrapMode wrapT)
        {
            GL.TextureParameter(Handle, TextureParameterName.TextureWrapR, (int)wrapR);
            GL.TextureParameter(Handle, TextureParameterName.TextureWrapS, (int)wrapS);
            GL.TextureParameter(Handle, TextureParameterName.TextureWrapT, (int)wrapT);
        }

        public void SetAniso(float maxAniso)
        {
            GL.TextureParameter(Handle, TextureParameterName.TextureMaxAnisotropy, maxAniso);
        }
    }
}
