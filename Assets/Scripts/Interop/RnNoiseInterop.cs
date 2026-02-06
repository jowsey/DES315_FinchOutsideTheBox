using System;
using System.Runtime.InteropServices;

namespace Interop
{
    // https://github.com/xiph/rnnoise/blob/master/include/rnnoise.h
    public static class RnNoiseInterop
    {
        private const string DllName = "rnnoise";

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern System.IntPtr rnnoise_create(IntPtr model);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void rnnoise_destroy(IntPtr state);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern float rnnoise_process_frame(IntPtr state, float[] output, float[] input);
    }
}