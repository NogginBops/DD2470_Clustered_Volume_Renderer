using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DD2470_Clustered_Volume_Renderer
{
    [Flags] public enum ColorChannels : byte { None = 0, Red = 1, Green = 2, Blue = 4, Alpha = 8, All = 0x0F }

    public enum DepthFunc
    {
        AlwaysPass = 0,
        NeverPass = 1,
        PassIfLessOrEqual = 2,
        PassIfEqual = 3,
    }

    [Flags]
    public enum ClearMask
    {
        Color = 1 << 0,
        Depth = 1 << 1,
        Stencil = 1 << 2,
    }

    public enum CullMode
    {
        CullNone = 0,
        CullFrontFacing = 1,
        CullBackFacing = 2,
        CullFrontAndBackFacing = 3,
    }

    internal static class Graphics
    {
        public static Shader? CurrentShader;
        public static void UseShader(Shader? shader)
        {
            if (shader != CurrentShader)
            {
                GL.UseProgram(shader?.Handle ?? 0);
                CurrentShader = shader;
            }
        }

        private static bool CullEnabled = false;
        private static CullMode CurrentCullMode = CullMode.CullNone;
        public static void SetCullMode(CullMode cullMode)
        {
            if (CurrentCullMode != cullMode)
            {
                CullFaceMode glCullMode = cullMode switch
                {
                    CullMode.CullNone => 0,
                    CullMode.CullFrontFacing => CullFaceMode.Front,
                    CullMode.CullBackFacing => CullFaceMode.Back,
                    CullMode.CullFrontAndBackFacing => CullFaceMode.FrontAndBack,
                    _ => throw new ArgumentException($"Unknown cull mode: {cullMode}", nameof(cullMode)),
                };

                if (glCullMode == 0 && CullEnabled)
                {
                    GL.Disable(EnableCap.CullFace);
                    CullEnabled = false;
                }
                else if (glCullMode != 0 && CullEnabled == false)
                {
                    GL.Enable(EnableCap.CullFace);
                    CullEnabled = true;
                }

                if (glCullMode != 0)
                {
                    GL.CullFace(glCullMode);
                }

                CurrentCullMode = cullMode;
            }
        }

        private static bool DepthWrite;
        public static void SetDepthWrite(bool write)
        {
            if (DepthWrite != write)
            {
                GL.DepthMask(write);
                DepthWrite = write;
            }
        }


        private static ColorChannels ColorWrite;
        public static void SetColorWrite(ColorChannels flags)
        {
            if (ColorWrite != flags)
            {
                GL.ColorMask(
                    flags.HasFlag(ColorChannels.Red),
                    flags.HasFlag(ColorChannels.Green),
                    flags.HasFlag(ColorChannels.Blue),
                    flags.HasFlag(ColorChannels.Alpha));

                ColorWrite = flags;
            }
        }


        private static DepthFunc CurrentDepthFunc;
        public static void SetDepthFunc(DepthFunc func)
        {
            if (CurrentDepthFunc != func)
            {
                DepthFunction glFunc = func switch
                {
                    DepthFunc.AlwaysPass => DepthFunction.Always,
                    DepthFunc.NeverPass => DepthFunction.Never,
                    DepthFunc.PassIfLessOrEqual => DepthFunction.Lequal,
                    DepthFunc.PassIfEqual => DepthFunction.Equal,
                    _ => throw new ArgumentException($"Unknown enum value: {func}", nameof(func)),
                };
                GL.DepthFunc(glFunc);
                CurrentDepthFunc = func;
            }
        }



        public static Color4 ClearColor;
        public static void SetClearColor(Color4 color)
        {
            if (ClearColor != color)
            {
                GL.ClearColor(color);
                ClearColor = color;
            }
        }

        public static void Clear(ClearMask mask)
        {
            if (mask.HasFlag(ClearMask.Color) && ColorWrite == ColorChannels.None)
                Debug.WriteLine("[Warning] Trying to clear color with color write disabled!");

            if (mask.HasFlag(ClearMask.Depth) && DepthWrite == false)
                Debug.WriteLine("[Warning] Trying to clear depth with depth write disabled!");

            ClearBufferMask glMask = 0;
            if (mask.HasFlag(ClearMask.Color))
                glMask |= ClearBufferMask.ColorBufferBit;
            if (mask.HasFlag(ClearMask.Depth))
                glMask |= ClearBufferMask.DepthBufferBit;
            if (mask.HasFlag(ClearMask.Stencil))
                glMask |= ClearBufferMask.StencilBufferBit;

            GL.Clear(glMask);
        }


        public const int MinSupportedVertexAttributes = 16;
        public const int MinSupportedVertexAttributeBindings = 16;

        public struct VertexAttribute
        {
            public bool Active;
            public int Size;
            public VertexAttribType Type;
            public bool Normalized;
            public int Offset;

            public override string ToString()
            {
                return $"Active: {Active} {Type} {Size} Offset: {Offset} Normalized: {Normalized}";
            }
        }

        public static VAO SetupVAO(string name)
        {
            GL.CreateVertexArrays(1, out int VAO);
            GL.ObjectLabel(ObjectLabelIdentifier.VertexArray, VAO, -1, name);

            VAO vao = new VAO(VAO);
            
            for (int i = 0; i < vao.Attributes.Length; i++)
            {
                vao.Attributes[i].Active = false;
                vao.Attributes[i].Size = 0;
                vao.Attributes[i].Type = 0;
                vao.Attributes[i].Normalized = false;
            }

            for (int i = 0; i < vao.AttributeToBufferLinks.Length; i++)
            {
                vao.AttributeToBufferLinks[i] = i;
            }

            return vao;
        }

        public static VAO? CurrentVAO;
        public static void BindVertexArray(VAO vao)
        {
            if (CurrentVAO != vao)
            {
                GL.BindVertexArray(vao.Handle);

                CurrentVAO = vao;
            }
        }

        public static void SetVertexAttribute(VAO vao, int index, bool active, int size, VertexAttribType type, bool normalized, int relativeOffset)
        {
            ref VertexAttribute attrib = ref vao.Attributes[index];

            if (attrib.Size != size ||
                attrib.Type != type ||
                attrib.Normalized != normalized ||
                attrib.Offset != relativeOffset)
            {
                GL.VertexArrayAttribFormat(vao.Handle, index, size, type, normalized, relativeOffset);

                attrib.Size = size;
                attrib.Type = type;
                attrib.Normalized = normalized;
                attrib.Offset = relativeOffset;
            }

            if (attrib.Active != active)
            {
                if (active)
                    GL.EnableVertexArrayAttrib(vao.Handle, index);
                else GL.DisableVertexArrayAttrib(vao.Handle, index);

                attrib.Active = active;
            }
        }

        public static void LinkAttributeBufferBinding(VAO vao, int index, int binding)
        {
            if (vao.AttributeToBufferLinks[index] != binding)
            {
                GL.VertexArrayAttribBinding(vao.Handle, index, binding);

                vao.AttributeToBufferLinks[index] = binding;
            }

        }

        public static void BindVertexAttributeBuffer(VAO vao, int binding, Buffer? buffer, int offset, int stride)
        {
            ref BufferBinding bufferBinding = ref vao.BufferBindings[binding];
            if (bufferBinding.Buffer != buffer ||
                bufferBinding.Offset != offset ||
                bufferBinding.Stride != stride)
            {
                GL.VertexArrayVertexBuffer(vao.Handle, binding, buffer?.Handle ?? 0, offset, stride);

                bufferBinding.Buffer = buffer;
                bufferBinding.Offset = offset;
                bufferBinding.Stride = stride;
            }
        }

        public static void SetElementBuffer(VAO vao, Buffer? ebo)
        {
            if (vao.ElementBuffer != ebo)
            {
                GL.VertexArrayElementBuffer(vao.Handle, ebo?.Handle ?? 0);

                vao.ElementBuffer = ebo;
            }
        }



        public const int MinTextureUnits = 16;

        public static Texture?[] BoundTextures = new Texture[MinTextureUnits];

        public static void BindTexture(int unit, Texture? texture)
        {
            if (BoundTextures[unit] != texture)
            {
                GL.BindTextureUnit(unit, texture?.Handle ?? 0);

                BoundTextures[unit] = texture;
            }
        }

        // GL_MAX_SHADER_STORAGE_BUFFER_BINDINGS is at least 8
        public const int MinShaderStorageBufferBindings = 8;

        public struct BindingRange : IEquatable<BindingRange>
        {
            public static readonly BindingRange Empty = new BindingRange(0, 0);

            public int Offset;
            public int Size;

            public BindingRange(int offset, int size)
            {
                Offset = offset;
                Size = size;
            }

            public override bool Equals(object? obj)
            {
                return obj is BindingRange range && Equals(range);
            }

            public bool Equals(BindingRange other)
            {
                return Offset == other.Offset &&
                       Size == other.Size;
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(Offset, Size);
            }

            public static bool operator ==(BindingRange left, BindingRange right)
            {
                return left.Equals(right);
            }

            public static bool operator !=(BindingRange left, BindingRange right)
            {
                return !(left == right);
            }
        }

        public static Buffer?[] BoundShaderStorageBuffers = new Buffer[MinShaderStorageBufferBindings];
        public static BindingRange[] BoundShaderStorageBufferRanges = new BindingRange[MinShaderStorageBufferBindings];

        public static void BindShaderStorageBlock(int index, Buffer? buffer)
        {
            if (BoundShaderStorageBuffers[index] != buffer || BoundShaderStorageBufferRanges[index] != BindingRange.Empty)
            {
                GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, index, buffer?.Handle ?? 0);

                BoundShaderStorageBuffers[index] = buffer;
                BoundShaderStorageBufferRanges[index] = BindingRange.Empty;
            }
        }

        public static void BindShaderStorageBlockRange(int index, Buffer? buffer, int offset, int size)
        {
            BindingRange range = new BindingRange(offset, size);
            if (BoundShaderStorageBuffers[index] != buffer || BoundShaderStorageBufferRanges[index] != range)
            {
                GL.BindBufferRange(BufferRangeTarget.ShaderStorageBuffer, index, buffer?.Handle ?? 0, offset, size);

                BoundShaderStorageBuffers[index] = buffer;
                BoundShaderStorageBufferRanges[index] = range;
            }
        }

        public const int MinUniformBufferBindings = 36;

        public static Buffer?[] BoundUniformBuffers = new Buffer[MinUniformBufferBindings];

        public static void BindUniformBuffer(int index, Buffer? buffer)
        {
            if (BoundUniformBuffers[index] != buffer)
            {
                GL.BindBufferBase(BufferRangeTarget.UniformBuffer, index, buffer?.Handle ?? 0);
                BoundUniformBuffers[index] = buffer;
            }
        }
    }
}
