using System;
using Concentus;
using Concentus.Enums;

namespace VoIP
{
    public class OpusProcessor : IDisposable
    {
        public const int FrameSize = 960;
        public const int MaxPacketSize = 1275;
        public const int Bitrate = 24000;

        private readonly IOpusEncoder _opusEncoder;
        private readonly IOpusDecoder _opusDecoder;

        public OpusProcessor()
        {
            _opusEncoder = OpusCodecFactory.CreateEncoder(VoipClient.SampleRate, 1, OpusApplication.OPUS_APPLICATION_VOIP);
            _opusEncoder.Bitrate = Bitrate;
            _opusEncoder.Complexity = 5;

            _opusDecoder = OpusCodecFactory.CreateDecoder(VoipClient.SampleRate, 1);
        }

        /**
         * Encode a frame of PCM into the output buffer, returning the number of bytes written
         * Ensure pcmFrame is FrameSize samples, and outputBuffer is MaxPacketSize bytes
         */
        public int Encode(Span<float> pcmFrame, byte[] outputBuffer)
        {
            return _opusEncoder.Encode(pcmFrame, FrameSize, outputBuffer, MaxPacketSize);
        }

        /**
         * Decode an Opus packet into the output buffer, returning the number of samples written
         * Ensure outputBuffer is FrameSize samples
         */
        public int Decode(ReadOnlySpan<byte> opusPacket, float[] outputBuffer)
        {
            return _opusDecoder.Decode(opusPacket, outputBuffer, FrameSize);
        }

        // todo use
        // public float[] DecodePLC()
        // {
        //     float[] output = new float[FrameSize];
        //     _opusDecoder.Decode(null, output, FrameSize, true);
        //     return output;
        // }

        public void ResetDecoderState()
        {
            _opusDecoder.ResetState();
        }

        public void Dispose()
        {
            _opusEncoder.Dispose();
            _opusDecoder.Dispose();
        }
    }
}