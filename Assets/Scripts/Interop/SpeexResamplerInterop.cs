using System;
using System.Runtime.InteropServices;

namespace Interop
{
    // https://github.com/xiph/speexdsp/blob/master/include/speex/speex_resampler.h
    public static class SpeexResamplerInterop
    {
        private const string DllName = "speexdsp";

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr speex_resampler_init(
            uint channels,
            uint inRate,
            uint outRate,
            int quality,
            out int err
        );

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void speex_resampler_destroy(IntPtr state);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int speex_resampler_process_float(
            IntPtr state,
            uint channelIndex,
            float[] input,
            ref uint inLen,
            float[] output,
            ref uint outLen
        );

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int speex_resampler_set_rate(
            IntPtr state,
            uint inRate,
            uint outRate
        );

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void speex_resampler_get_ratio(
            IntPtr state,
            out uint numRate,
            out uint denRate
        );

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int speex_resampler_skip_zeros(IntPtr state);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int speex_resampler_reset_mem(IntPtr state);
    }
}