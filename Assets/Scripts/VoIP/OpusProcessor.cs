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
            _opusEncoder.UseInbandFEC = false; //true requires bitrate < 40kbps
            _opusEncoder.PacketLossPercent = 15; //not used unless above is true

            _opusDecoder = OpusCodecFactory.CreateDecoder(VoipClient.SampleRate, 1);
        }


        public byte[] Encode(float[] pcmFrame)
        {
            byte[] temp = new byte[MaxPacketSize]; // todo change these to be caller allocated so we can use arraypool
            int len = _opusEncoder.Encode(pcmFrame, FrameSize, temp, temp.Length);
            byte[] output = new byte[len];
            Array.Copy(temp, output, len);
            return output;
        }


        public float[] Decode(byte[] opusFrame)
        {
            float[] output = new float[FrameSize]; // todo see above
            int len = _opusDecoder.Decode(opusFrame, output, FrameSize);
            return output;
        }
        

        public float[] DecodePLC()
        {
            float[] output = new float[FrameSize];
            _opusDecoder.Decode(null, output, FrameSize, true);
            return output;
        }

        public void Dispose()
        {
            _opusEncoder.Dispose();
            _opusDecoder.Dispose();
        }
    }
}
