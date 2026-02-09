using System;

namespace VoIP
{
    public class RnNoiseProcessor : IDisposable
    {
        public const int FrameSize = 480; // required

        private IntPtr _state;
        private bool _disposed;

        public RnNoiseProcessor()
        {
            _state = Interop.RnNoiseInterop.rnnoise_create(IntPtr.Zero);
            if (_state == IntPtr.Zero) throw new Exception("Failed to initialize RNNoise");
        }

        /**
         * Denoise a frame of PCM into the output buffer, returning the probability that the frame contains voice (0-1)
         * Ensure input and output are FrameSize samples
         */
        public float ProcessFrame(float[] input, float[] output)
        {
            if (input.Length != FrameSize || output.Length != FrameSize) throw new ArgumentException($"Input and output must be {FrameSize} samples");

            // float -> int16
            for (int i = 0; i < FrameSize; i++)
                input[i] *= 32768f;

            float vad = Interop.RnNoiseInterop.rnnoise_process_frame(_state, output, input);

            // int16 -> float
            for (int i = 0; i < FrameSize; i++)
                output[i] /= 32768f;

            return vad;
        }

        public void Dispose()
        {
            if (_disposed || _state == IntPtr.Zero) return;

            Interop.RnNoiseInterop.rnnoise_destroy(_state);
            _state = IntPtr.Zero;
            _disposed = true;
        }
    }
}