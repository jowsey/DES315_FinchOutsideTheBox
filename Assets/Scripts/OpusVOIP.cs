using Concentus;
using Concentus.Enums;
using Concentus.Structs;
using System;
using System.Text;

public class OpusVOIP
{
    private IOpusEncoder _opusEncoder;
    private IOpusDecoder _opusDecoder;

    //Constructor parameters
    private int _bitrate;
    private int _sampleRate;
    private int _channels;
    private int _frameDurationMs;

    private int _samplesPerFrame;

    public OpusVOIP(int bitrate, int sampleRate, int channels, int frameDurationMs)
    {
        _bitrate = bitrate;
        _sampleRate = sampleRate;
        _channels = channels;
        _frameDurationMs = frameDurationMs;

        _samplesPerFrame = (int)(_sampleRate * _frameDurationMs / 1000.0f);

        _opusEncoder = OpusCodecFactory.CreateEncoder(sampleRate, channels, OpusApplication.OPUS_APPLICATION_VOIP);
        _opusEncoder.Bitrate = _bitrate;
        _opusEncoder.Complexity = 5;
        _opusEncoder.UseInbandFEC = false; //true requires bitrate < 40kbps
        _opusEncoder.PacketLossPercent = 15;

        _opusDecoder = OpusCodecFactory.CreateDecoder(sampleRate, channels);
    }


    public byte[] Encode(float[] pcmFrame)
    {
        byte[] temp = new byte[1275]; //max opus frame-packet size
        int len = _opusEncoder.Encode(pcmFrame, _samplesPerFrame, temp, temp.Length);
        byte[] output = new byte[len];
        Array.Copy(temp, output, len);
        return output;
    }


    public float[] Decode(byte[] opusFrame)
    {
        float[] output = new float[_samplesPerFrame];
        int len = _opusDecoder.Decode(opusFrame, output, _samplesPerFrame);
        return output;
    }

    public float[] DecodePLC()
    {
        float[] output = new float[_samplesPerFrame];
        _opusDecoder.Decode(null, output, _samplesPerFrame, true);
        return output;
    }
}
