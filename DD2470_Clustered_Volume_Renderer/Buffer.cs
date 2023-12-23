using OpenTK.Graphics.OpenGL4;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace DD2470_Clustered_Volume_Renderer
{
    internal class Buffer
    {
        public int Handle;
        public int Count;
        public int Size;

        public int SizeInBytes => Count * Size;

        public Buffer(int handle, int count, int size)
        {
            Handle = handle;
            Count = count;
            Size = size;
        }

        public override string ToString()
        {
            return $"Buffer - {Handle}";
        }

        public static Buffer CreateBuffer(string name, int count, int size, BufferStorageFlags flags)
        {
            GL.CreateBuffers(1, out int buffer);
            GL.ObjectLabel(ObjectLabelIdentifier.Buffer, buffer, -1, name);
            GL.NamedBufferStorage(buffer, count * size, 0, flags);
            return new Buffer(buffer, count, size);
        }

        public static unsafe Buffer CreateBuffer<T>(string name, int count, BufferStorageFlags flags)
            where T : unmanaged
        {
            GL.CreateBuffers(1, out int buffer);
            GL.ObjectLabel(ObjectLabelIdentifier.Buffer, buffer, -1, name);
            GL.NamedBufferStorage(buffer, count * sizeof(T), 0, flags);
            return new Buffer(buffer, count, sizeof(T));
        }

        public static unsafe Buffer CreateBuffer<T>(string name, ReadOnlySpan<T> data, BufferStorageFlags flags)
            where T : unmanaged
        {
            GL.CreateBuffers(1, out int buffer);
            GL.ObjectLabel(ObjectLabelIdentifier.Buffer, buffer, -1, name);
            fixed (T* dataPtr = data)
            {
                GL.NamedBufferStorage(buffer, data.Length * sizeof(T), (nint)dataPtr, flags);
            }
            return new Buffer(buffer, data.Length, sizeof(T));
        }

        public static unsafe Buffer CreateBuffer<T>(string name, Span<T> data, BufferStorageFlags flags)
            where T : unmanaged
        {
            return CreateBuffer(name, (ReadOnlySpan<T>)data, flags);
        }

        public static unsafe Buffer CreateBuffer<T>(string name, T[] data, BufferStorageFlags flags)
            where T : unmanaged
        {
            return CreateBuffer(name, (ReadOnlySpan<T>)data, flags);
        }

        public static unsafe Buffer CreateBuffer<T>(string name, List<T> data, BufferStorageFlags flags)
            where T : unmanaged
        {
            return CreateBuffer(name, CollectionsMarshal.AsSpan(data), flags);
        }

        public static unsafe void DeleteBuffer(Buffer buffer)
        {
            // FIXME: Potentially do some work to unbind this buffer?
            GL.DeleteBuffer(buffer.Handle);
            buffer.Handle = 0;
        }

        public static unsafe void UpdateSubData<T>(Buffer buffer, ReadOnlySpan<T> span, int elementOffset) 
            where T : unmanaged
        {
            fixed(T* ptr = span)
            {
                GL.NamedBufferSubData(buffer.Handle, elementOffset * buffer.Size, span.Length * buffer.Size, (IntPtr)ptr);
            }
        }
    }
}
