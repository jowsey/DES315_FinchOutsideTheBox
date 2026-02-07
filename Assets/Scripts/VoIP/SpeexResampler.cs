using System;
using Interop;
using UnityEngine;

namespace VoIP
{
    public class SpeexResampler : IDisposable
    {
        private const int Quality = 5; //0-10, higher is better quality but more CPU

        private IntPtr _state;

        private readonly uint _inRate;
        private readonly uint _outRate;

        public SpeexResampler(uint inRate, uint outRate)
        {
            _inRate = inRate;
            _outRate = outRate;

            _state = SpeexResamplerInterop.speex_resampler_init(1, inRate, outRate, Quality, out var err);
            if (err != 0) throw new Exception($"Failed to initialize SpeexResampler: {err}");

            SpeexResamplerInterop.speex_resampler_skip_zeros(_state);
        }

        // Get maximum possible size the resampled frame could be, given the input frame size
        public int GetResampledSize(int inputFrameSize)
        {
            var ratio = (float)_outRate / _inRate;
            return Mathf.CeilToInt(inputFrameSize * ratio);
        }

        // Resample a frame, writing to the provided buffer
        public int Resample(float[] input, int inputLength, float[] output)
        {
            var inLen = (uint)inputLength;
            var outLen = (uint)output.Length;

            var err = SpeexResamplerInterop.speex_resampler_process_float(_state, 0, input, ref inLen, output, ref outLen);
            if (err != 0) throw new Exception($"Failed to resample audio: {err}");

            return (int)outLen;
        }

        public void Dispose()
        {
            if (_state == IntPtr.Zero) return;
            SpeexResamplerInterop.speex_resampler_destroy(_state);
            _state = IntPtr.Zero;
        }
    }
}