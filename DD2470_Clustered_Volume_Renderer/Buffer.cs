using OpenTK.Graphics.OpenGL4;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DD2470_Clustered_Volume_Renderer
{
    internal class Buffer
    {
        public int Handle;

        public Buffer(int handle)
        {
            Handle = handle;
        }

        public override string ToString()
        {
            return $"Buffer - {Handle}";
        }

        public static Buffer CreateBuffer(int count, int size, BufferStorageFlags flags)
        {
            GL.CreateBuffers(1, out int buffer);
            GL.NamedBufferStorage(buffer, count * size, 0, flags);
            return new Buffer(buffer);
        }

        public static unsafe Buffer CreateBuffer<T>(int count, BufferStorageFlags flags)
            where T : unmanaged
        {
            GL.CreateBuffers(1, out int buffer);
            GL.NamedBufferStorage(buffer, count * sizeof(T), 0, flags);
            return new Buffer(buffer);
        }

        public static unsafe Buffer CreateBuffer<T>(ReadOnlySpan<T> data, BufferStorageFlags flags)
            where T : unmanaged
        {
            GL.CreateBuffers(1, out int buffer);
            fixed (T* dataPtr = data)
            {
                GL.NamedBufferStorage(buffer, data.Length * sizeof(T), (nint)dataPtr, flags);
            }
            return new Buffer(buffer);
        }

        public static unsafe Buffer CreateBuffer<T>(Span<T> data, BufferStorageFlags flags)
            where T : unmanaged
        {
            return CreateBuffer((ReadOnlySpan<T>)data, flags);
        }

        public static unsafe Buffer CreateBuffer<T>(T[] data, BufferStorageFlags flags)
            where T : unmanaged
        {
            return CreateBuffer((ReadOnlySpan<T>)data, flags);
        }
    }
}
