using Assimp;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics.X86;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace AssetProcessor
{
    public static unsafe partial class MeshOptimizer
    {
        public struct Stream
        {
            public void* data;
            public ulong size;
            public ulong stride;
        }

        static MeshOptimizer()
        {
            DllResolver.InitLoader();
        }

        /// <summary>
        /// Generates a vertex remap table from the vertex buffer and an optional index buffer and returns number of unique vertices
        /// As a result, all vertices that are binary equivalent map to the same(new) location, with no gaps in the resulting sequence.
        /// Resulting remap table maps old vertices to new vertices and can be used in meshopt_remapVertexBuffer/meshopt_remapIndexBuffer.
        /// Note that binary equivalence considers all vertex_size bytes, including padding which should be zero-initialized.
        /// </summary>
        /// <param name="destination">destination must contain enough space for the resulting remap table (vertex_count elements)</param>
        /// <param name="indices">indices can be NULL if the input is unindexed</param>
        /// <param name="index_count"></param>
        /// <param name="vertices"></param>
        /// <param name="vertex_count"></param>
        /// <param name="vertex_size"></param>
        /// <returns></returns>
        [LibraryImport("meshoptimizer", EntryPoint = "meshopt_generateVertexRemap")]
        public static partial ulong GenerateVertexRemap(uint* destination, /* const */ uint* indices, ulong index_count, /* const */ void* vertices, ulong vertex_count, ulong vertex_size);

        /// <inheritdoc cref="GenerateVertexRemap(uint*, uint*, ulong, void*, ulong, ulong)"/>
        public static unsafe ulong GenerateVertexRemap<TVert>(Span<uint> destination, ReadOnlySpan<uint> indices, /* const */ ReadOnlySpan<TVert> vertices) where TVert : unmanaged
        {
            ulong index_count = (ulong)indices.Length;
            ulong vertex_count = (ulong)vertices.Length;
            ulong vertex_size = (ulong)sizeof(TVert);

            fixed (uint* destinationPtr = destination)
            fixed (uint* indicesPtr = indices)
            fixed (void* verticesPtr = vertices)
            {
                return GenerateVertexRemap(destinationPtr, indicesPtr, index_count, verticesPtr, vertex_count, vertex_size);
            }
        }

        /// <summary>
        /// Generates a vertex remap table from multiple vertex streams and an optional index buffer and returns number of unique vertices
        /// As a result, all vertices that are binary equivalent map to the same (new) location, with no gaps in the resulting sequence.
        /// Resulting remap table maps old vertices to new vertices and can be used in meshopt_remapVertexBuffer/meshopt_remapIndexBuffer.
        /// To remap vertex buffers, you will need to call meshopt_remapVertexBuffer for each vertex stream.
        /// Note that binary equivalence considers all size bytes in each stream, including padding which should be zero-initialized.
        /// </summary>
        /// <param name="destination">destination must contain enough space for the resulting remap table (vertex_count elements)</param>
        /// <param name="indices">indices can be NULL if the input is unindexed</param>
        /// <param name="index_count"></param>
        /// <param name="vertex_count"></param>
        /// <param name="streams"></param>
        /// <param name="stream_count">stream_count must be <= 16</param>
        /// <returns></returns>
        [LibraryImport("meshoptimizer", EntryPoint = "meshopt_generateVertexRemapMulti")]
        public static partial ulong GenerateVertexRemapMulti(uint* destination, /* const */ uint* indices, ulong index_count, ulong vertex_count, /* const */ Stream* streams, ulong stream_count);

        /// <inheritdoc cref="GenerateVertexRemapMulti(uint*, uint*, ulong, ulong, Stream*, ulong)"/>
        public static unsafe ulong GenerateVertexRemapMulti(Span<uint> destination, ReadOnlySpan<uint> indices, ulong vertex_count, ReadOnlySpan<Stream> streams)
        {
            ulong index_count = (ulong)indices.Length;
            ulong stream_count = (ulong)streams.Length;

            fixed (uint* destinationPtr = destination)
            fixed (uint* indicesPtr = indices)
            fixed (Stream* streamsPtr = streams)
            {
                return GenerateVertexRemapMulti(destinationPtr, indicesPtr, index_count, vertex_count, streamsPtr, stream_count);
            }
        }

        /// <inheritdoc cref="GenerateVertexRemapMulti(uint*, uint*, ulong, ulong, Stream*, ulong)"/>
        public static unsafe ulong GenerateVertexRemapMulti<TVert0, TVert1>(Span<uint> destination, ReadOnlySpan<uint> indices, ulong vertex_count, Span<TVert0> stream0, Span<TVert1> stream1)
            where TVert0 : unmanaged
            where TVert1 : unmanaged
        {
            ulong index_count = (ulong)indices.Length;
            ulong stream_count = 2;

            fixed (uint* destinationPtr = destination)
            fixed (uint* indicesPtr = indices)
            fixed (TVert0* stream0Ptr = stream0)
            fixed (TVert1* stream1Ptr = stream1)
            {
                Stream* streams = stackalloc Stream[2];
                streams[0].data = stream0Ptr;
                streams[0].size = (ulong)sizeof(TVert0);
                streams[0].stride = (ulong)sizeof(TVert0);

                streams[1].data = stream1Ptr;
                streams[1].size = (ulong)sizeof(TVert1);
                streams[1].stride = (ulong)sizeof(TVert1);

                return GenerateVertexRemapMulti(destinationPtr, indicesPtr, index_count, vertex_count, streams, stream_count);
            }
        }

        /// <summary>
        /// Generates vertex buffer from the source vertex buffer and remap table generated by meshopt_generateVertexRemap
        /// </summary>
        /// <param name="destination">destination must contain enough space for the resulting vertex buffer (unique_vertex_count elements, returned by meshopt_generateVertexRemap)</param>
        /// <param name="vertices"></param>
        /// <param name="vertex_count">vertex_count should be the initial vertex count and not the value returned by meshopt_generateVertexRemap</param>
        /// <param name="vertex_size"></param>
        /// <param name="remap"></param>
        [LibraryImport("meshoptimizer", EntryPoint = "meshopt_remapVertexBuffer")]
        public static partial void RemapVertexBuffer(void* destination, /* const */ void* vertices, ulong vertex_count, ulong vertex_size, /* const */ uint* remap);

        /// <inheritdoc cref="RemapVertexBuffer(void*, void*, ulong, ulong, uint*)"/>
        public static unsafe void RemapVertexBuffer<TVert>(Span<TVert> destination, ReadOnlySpan<TVert> vertices, ReadOnlySpan<uint> remap) where TVert : unmanaged
        {
            ulong vertex_count = (ulong)vertices.Length;
            ulong vertex_size = (ulong)sizeof(TVert);

            fixed (void* destinationPtr = destination)
            fixed (void* verticesPtr = vertices)
            fixed (uint* remapPtr = remap)
            {
                RemapVertexBuffer(destinationPtr, verticesPtr, vertex_count, vertex_size, remapPtr);
            }
        }

        /// <summary>
        /// Generate index buffer from the source index buffer and remap table generated by meshopt_generateVertexRemap
        /// </summary>
        /// <param name="destination">destination must contain enough space for the resulting index buffer (index_count elements)</param>
        /// <param name="indices">indices can be NULL if the input is unindexed</param>
        /// <param name="index_count"></param>
        /// <param name="remap"></param>
        [LibraryImport("meshoptimizer", EntryPoint = "meshopt_remapIndexBuffer")]
        public static partial void RemapIndexBuffer(uint* destination, /* const */ uint* indices, ulong index_count, /* const */ uint* remap);

        /// <inheritdoc cref="RemapIndexBuffer(uint*, uint*, ulong, uint*)"/>
        public static unsafe void RemapIndexBuffer(Span<uint> destination, ReadOnlySpan<uint> indices, ReadOnlySpan<uint> remap)
        {
            ulong index_count = (ulong)indices.Length;

            fixed (uint* destinationPtr = destination)
            fixed (uint* indicesPtr = indices)
            fixed (uint* remapPtr = remap)
            {
                RemapIndexBuffer(destinationPtr, indicesPtr, index_count, remapPtr);
            }
        }

        /// <summary>
        /// Vertex transform cache optimizer
        /// Reorders indices to reduce the number of GPU vertex shader invocations
        /// If index buffer contains multiple ranges for multiple draw calls, this functions needs to be called on each range individually.
        /// </summary>
        /// <param name="destination">destination must contain enough space for the resulting index buffer (index_count elements)</param>
        /// <param name="indices"></param>
        /// <param name="index_count"></param>
        /// <param name="vertex_count"></param>
        [LibraryImport("meshoptimizer", EntryPoint = "meshopt_optimizeVertexCache")]
        public static partial void OptimizeVertexCache(uint* destination, /* const */ uint* indices, ulong index_count, ulong vertex_count);

        /// <inheritdoc cref="OptimizeVertexCache(uint*, uint*, ulong, ulong)"/>
        public static unsafe void OptimizeVertexCache(Span<uint> destination, ReadOnlySpan<uint> indices, ulong vertex_count)
        {
            ulong index_count = (ulong)indices.Length;
            fixed (uint* destinationPtr = destination)
            fixed (uint* indicesPtr = indices)
            {
                OptimizeVertexCache(destinationPtr, indicesPtr, index_count, vertex_count);
            }
        }

        /// <summary>
        /// Vertex fetch cache optimizer
        /// Reorders vertices and changes indices to reduce the amount of GPU memory fetches during vertex processing
        /// Returns the number of unique vertices, which is the same as input vertex count unless some vertices are unused
        /// This functions works for a single vertex stream; for multiple vertex streams, use meshopt_optimizeVertexFetchRemap + meshopt_remapVertexBuffer for each stream.
        /// </summary>
        /// <param name="destination">destination must contain enough space for the resulting vertex buffer (vertex_count elements)</param>
        /// <param name="indices">indices is used both as an input and as an output index buffer</param>
        /// <param name="index_count"></param>
        /// <param name="vertices"></param>
        /// <param name="vertex_count"></param>
        /// <param name="vertex_size"></param>
        /// <returns></returns>
        [LibraryImport("meshoptimizer", EntryPoint = "meshopt_optimizeVertexFetch")]
        public static partial ulong OptimizeVertexFetch(void* destination, uint* indices, ulong index_count, /* const */ void* vertices, ulong vertex_count, ulong vertex_size);

        /// <inheritdoc cref="OptimizeVertexFetch(void*, uint*, ulong, void*, ulong, ulong)"/>
        public static unsafe ulong OptimizeVertexFetch<TVert>(Span<TVert> destination, Span<uint> indices, ReadOnlySpan<TVert> vertices)
            where TVert : unmanaged
        {
            ulong index_count = (ulong)indices.Length;
            ulong vertex_count = (ulong)vertices.Length;
            ulong vertex_size = (ulong)sizeof(TVert);
            fixed (void* destinationPtr = destination)
            fixed (uint* indicesPtr = indices)
            fixed (void* verticesPtr = vertices)
            {
                return OptimizeVertexFetch(destinationPtr, indicesPtr, index_count, verticesPtr, vertex_count, vertex_size);
            }
        }

        /// <summary>
        /// Vertex fetch cache optimizer
        /// Generates vertex remap to reduce the amount of GPU memory fetches during vertex processing
        /// Returns the number of unique vertices, which is the same as input vertex count unless some vertices are unused
        /// The resulting remap table should be used to reorder vertex/index buffers using meshopt_remapVertexBuffer/meshopt_remapIndexBuffer
        /// </summary>
        /// <param name="destination">destination must contain enough space for the resulting remap table(vertex_count elements)</param>
        /// <param name="indices"></param>
        /// <param name="index_count"></param>
        /// <param name="vertex_count"></param>
        /// <returns></returns>
        [LibraryImport("meshoptimizer", EntryPoint = "meshopt_optimizeVertexFetchRemap")]
        public static partial ulong OptimizeVertexFetchRemap(uint* destination, /* const */ uint* indices, ulong index_count, ulong vertex_count);

        /// <inheritdoc cref="OptimizeVertexFetchRemap(uint*, uint*, ulong, ulong)"/>
        public static unsafe ulong OptimizeVertexFetchRemap(Span<uint> destination, ReadOnlySpan<uint> indices, ulong vertex_count)
        {
            ulong index_count = (ulong)indices.Length;
            fixed(uint* destinationPtr = destination)
            fixed (uint* indicesPtr = indices)
            {
                return OptimizeVertexFetchRemap(destinationPtr, indicesPtr, index_count, vertex_count);
            }
        }


        public struct VertexCacheStatistics
        {
            public uint vertices_transformed;
            public uint warps_executed;
            public float acmr; /* transformed vertices / triangle count; best case 0.5, worst case 3.0, optimum depends on topology */
            public float atvr; /* transformed vertices / vertex count; best case 1.0, worst case 6.0, optimum is 1.0 (each vertex is transformed once) */
        };

        /// <summary>
        /// Vertex transform cache analyzer
        /// Returns cache hit statistics using a simplified FIFO model
        /// Results may not match actual GPU performance
        /// </summary>
        [LibraryImport("meshoptimizer", EntryPoint = "meshopt_analyzeVertexCache")]
        public static partial VertexCacheStatistics AnalyzeVertexCache(/* const */ uint* indices, ulong index_count, ulong vertex_count, uint cache_size, uint warp_size, uint primgroup_size);

        /// <inheritdoc cref="AnalyzeVertexCache(uint*, ulong, ulong, uint, uint, uint)"/>
        public static unsafe VertexCacheStatistics AnalyzeVertexCache(ReadOnlySpan<uint> indices, ulong vertex_count, uint cache_size, uint warp_size, uint primgroup_size)
        {
            ulong indices_count = (ulong)indices.Length;
            fixed (uint* indicesPtr = indices)
            {
                return AnalyzeVertexCache(indicesPtr, indices_count, vertex_count, cache_size, warp_size, primgroup_size);
            }
        }

        public struct VertexFetchStatistics
        {
            public uint bytes_fetched;
            public float overfetch; /* fetched bytes / vertex buffer size; best case 1.0 (each byte is fetched once) */
        };

        /**
         * Vertex fetch cache analyzer
         * Returns cache hit statistics using a simplified direct mapped model
         * Results may not match actual GPU performance
         */
        [LibraryImport("meshoptimizer", EntryPoint = "meshopt_analyzeVertexFetch")]
        public static partial VertexFetchStatistics AnalyzeVertexFetch(/* const */ uint* indices, ulong index_count, ulong vertex_count, ulong vertex_size);

        /// <inheritdoc cref="AnalyzeVertexFetch(uint*, ulong, ulong, ulong)"/>
        public static unsafe VertexFetchStatistics AnalyzeVertexFetch(ReadOnlySpan<uint> indices, ulong vertex_count, ulong vertex_size)
        {
            ulong index_count = (ulong)indices.Length;
            fixed (uint* indicesPtr = indices)
            {
                return AnalyzeVertexFetch(indicesPtr, index_count, vertex_count, vertex_size);
            }
        }
    }
}
