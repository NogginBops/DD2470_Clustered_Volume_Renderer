using System;
using System.Reflection;
using System.Runtime.InteropServices;

#pragma warning disable IDE1006 // Naming Styles
namespace AssetProcessor
{
    public unsafe struct encode_output
    {
        public int width;
        public int height;
        public int mipmap_count;
        public int dxgi_format;
        public char* blocks;
        public int num_blocks;
        public int bytes_per_block;
    }

    public enum bc7enc_error
    {
        success = 0,
        no_source_file_name,
        null_output_pointer,
        could_not_load_source_file,
        could_not_initialize_encoder,
        could_not_encode_image,
        out_of_memory,
    };

    public enum mipmap_generation_method : uint
    {
        LinearBox,
        sRGBBox,
        NormalMap,
    }

    public enum bc1_approx_mode
    {
        // The default mode. No rounding for 4-color colors 2,3. My older tools/compressors use this mode. 
        // This matches the D3D10 docs on BC1.
        Ideal = 0,

		// NVidia GPU mode.
		NVidia = 1,

		// AMD GPU mode.
		AMD = 2,
		
		// This mode matches AMD Compressonator's output. It rounds 4-color colors 2,3 (not 3-color color 2).
		// This matches the D3D9 docs on DXT1.
		IdealRound4 = 3
	}

    public struct rdo_bc_params
    {
        const int BC7ENC_MAX_PARTITIONS = 64;

        const int rgbcx_MAX_LEVEL = 18;
        const uint rgbcx_BC4_USE_ALL_MODES = 3;

        const int DXGI_FORMAT_BC7_UNORM = 98;

        public int m_bc7_uber_level = 6;
        public int m_bc7enc_max_partitions_to_scan = BC7ENC_MAX_PARTITIONS;
        public bool m_perceptual = false;
        public bool m_y_flip = false;
        public uint m_bc45_channel0 = 0;
        public uint m_bc45_channel1 = 1;

        public bool m_generate_mipmaps = false;
        public mipmap_generation_method m_mipmap_method = mipmap_generation_method.LinearBox;

        public bc1_approx_mode m_bc1_mode = bc1_approx_mode.Ideal;
        public bool m_use_bc1_3color_mode = true;

        public bool m_use_bc1_3color_mode_for_black = true;

        public int m_bc1_quality_level = rgbcx_MAX_LEVEL;

        // FIXME:
        public int /* DXGI_FORMAT */ m_dxgi_format = DXGI_FORMAT_BC7_UNORM;

        public float m_rdo_lambda = 0;
        public bool m_rdo_debug_output = false;
        public float m_rdo_smooth_block_error_scale = 15.0f;
        public bool m_custom_rdo_smooth_block_error_scale = false;
        public uint m_lookback_window_size = 128;
        public bool m_custom_lookback_window_size = false;
        public bool m_bc7enc_rdo_bc7_quant_mode6_endpoints = true;
        public bool m_bc7enc_rdo_bc7_weight_modes = true;
        public bool m_bc7enc_rdo_bc7_weight_low_frequency_partitions = true;
        public bool m_bc7enc_rdo_bc7_pbit1_weighting = true;
        public float m_rdo_max_smooth_block_std_dev = 18.0f;
        public bool m_rdo_allow_relative_movement = false;
        public bool m_rdo_try_2_matches = true;
        public bool m_rdo_ultrasmooth_block_handling = true;

        public bool m_use_hq_bc345 = true;
        public int m_bc345_search_rad = 5;
        public uint m_bc345_mode_mask = rgbcx_BC4_USE_ALL_MODES;

        public bool m_bc7enc_mode6_only = false;
        public bool m_rdo_multithreading = true;

        public bool m_bc7enc_reduce_entropy = false;

        public bool m_use_bc7e = true;
        public bool m_status_output = false;

        public uint m_rdo_max_threads = 128;

        public rdo_bc_params()
        {
        }
    }

    public static partial class Bc7Enc
    {
        static Bc7Enc()
        {
            DllResolver.InitLoader();
        }

        /// <param name="data">RGBA image data.</param>
        public static unsafe bc7enc_error compress_image_from_memory<T>(int width, int height, T[] data, rdo_bc_params @params, out encode_output output)
            where T : unmanaged
        {
            fixed (void* ptr = data)
            {
                return compress_image_from_memory(width, height, ptr, @params, out output);
            }
        }

        /// <param name="data">RGBA image data.</param>
        public static unsafe bc7enc_error compress_image_from_memory<T>(int width, int height, ReadOnlySpan<T> data, rdo_bc_params @params, out encode_output output)
            where T : unmanaged
        {
            fixed (void* ptr = data)
            {
                return compress_image_from_memory(width, height, ptr, @params, out output);
            }
        }

        [LibraryImport("bc7enc")]
        public static unsafe partial bc7enc_error compress_image_from_memory(int width, int height, void* data, rdo_bc_params @params, out encode_output output);

        [LibraryImport("bc7enc", StringMarshalling = StringMarshalling.Utf8)]
        public static unsafe partial bc7enc_error compress_image_from_file(string image_path, rdo_bc_params @params, out encode_output output);

        [LibraryImport("bc7enc")]
        public static unsafe partial void free_encode_output(in encode_output output);

    }
}
#pragma warning restore IDE1006 // Naming Styles