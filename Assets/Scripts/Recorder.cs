using Mirror;
using Sirenix.OdinInspector;
using System;
using System.Collections.Concurrent;
using UnityEngine;

public class Recorder : NetworkBehaviour
{
    // [ValueDropdown("@UnityEngine.Microphone.devices")]
    // [ShowInInspector]
    private string _device;
    [SerializeField] private int _frequency = 48000;
    [SerializeField] private AudioSource _source;

    private float _samplesBufferSizeSeconds = 1.0f;
    
    [SyncVar]
    private bool _isStreaming = false;

    //Mic clip
    private AudioClip _micClip; //clip the mic will record into (loops)
    private int _micClipSizeSeconds = 5;
    private int _micReadPos = 0;

    //Samples buffer
    [SyncVar]
    private float[] _samplesBuffer;
    private int _samplesBufferWritePos = 0;
    private int _samplesBufferReadPos = 0;


    public void Start()
    {
        _device = Microphone.devices[0];

        _samplesBuffer = new float[(int)(_frequency * _samplesBufferSizeSeconds)];


        if (isLocalPlayer)
        {
            StartStreaming();
        }
        else
        {
            //To initiate OnAudioFilterRead()
            AudioClip dummy = AudioClip.Create("dummy", _frequency, 1, _frequency, false);
            float[] silence = new float[_frequency];
            dummy.SetData(silence, 0);

            _source.loop = true;
            _source.clip = dummy;
            _source.Play();
        }
    }


    public void StartStreaming()
    {
        if (_isStreaming || Microphone.IsRecording(_device)) { return; }
        _isStreaming = true;
        
        _micClip = Microphone.Start(_device, true, _micClipSizeSeconds, _frequency);
        
        while (Microphone.GetPosition(_device) <= 0)
        {
            //busy wait while microphone initialises
        }

        Debug.Log("Streaming started.");
        Debug.Log($"Actual mic frequency: {_micClip.frequency} (requested {_frequency})");
        Debug.Log($"Audio output sample rate: {AudioSettings.outputSampleRate}");
    }


    public void StopStreaming()
    {
        if (!_isStreaming || !Microphone.IsRecording(_device)) { return; }
        _isStreaming = false;
        
        Microphone.End(_device);
        Debug.Log("Streaming stopped.");
    }


    public void Update()
    {
        if (!_isStreaming || !Microphone.IsRecording(_device)) { return; }

        int micClipSizeSamples = _micClipSizeSeconds * _frequency;

        //Get available samples
        int numAvailableSamples = 0;
        int micWritePos = Microphone.GetPosition(_device);
        if (micWritePos >= _micReadPos)
        {
            numAvailableSamples = micWritePos - _micReadPos;
        }
        else
        {
            //Wraparound
            numAvailableSamples = micWritePos + micClipSizeSamples - _micReadPos;
        }
        if (numAvailableSamples == 0) { return; }


        //There are samples in the mic clip that can be copied into the samples buffer
        float[] temp = new float[numAvailableSamples]; //todo: this triggers garbage collection, should probs preallocate
        if (_micReadPos + numAvailableSamples <= micClipSizeSamples)
        {
            //Simple copy
            _micClip.GetData(temp, _micReadPos);
        }
        else
        {
            //Wraparound
            //Copy region spanning read pos to clip end
            float[] first = new float[micClipSizeSamples - _micReadPos];
            _micClip.GetData(first, _micReadPos);
            System.Array.Copy(first, 0, temp, 0, first.Length);

            //Copy region spanning from clip start to write pos
            float[] second = new float[micWritePos];
            _micClip.GetData(second, 0);
            System.Array.Copy(second, 0, temp, 0, second.Length);
        }
        _micReadPos = micWritePos;

        for (int i = 0; i < temp.Length; ++i)
        {
            _samplesBuffer[_samplesBufferWritePos] = temp[i];
            _samplesBufferWritePos = (_samplesBufferWritePos + 1) % _samplesBuffer.Length;

            //If we're writing faster than the audio thread is reading, sacrifice the oldest unread sample by advancing the read position forward
            if (_samplesBufferWritePos == _samplesBufferReadPos)
            {
                _samplesBufferReadPos = (_samplesBufferReadPos + 1) % _samplesBuffer.Length;
            }
        }
    }


    void OnAudioFilterRead(float[] data, int channels)
    {
        if (!_isStreaming)
        {
            //Not streaming, output silence
            System.Array.Clear(data, 0, data.Length);
            return;
        }

        //Copy data in _samplesBuffer to data
        //Buffer is interleaved by channel. e.g.:
        //Mono: [C0, C1, C2, ...]
        //Stereo: [L0, R0, L1, R1, L2, R2, ...]
        //Hence, step by number of channels between each iteration
        for (int i = 0; i < data.Length; i += channels)
        {
            //If read pos = write pos, there's no data available in the ring buffer
            if (_samplesBufferReadPos != _samplesBufferWritePos)
            {
                //There is data in the buffer, copy this sample to all channels
                float sample = _samplesBuffer[_samplesBufferReadPos];
                for (int c = 0; c < channels; ++c)
                {
                    data[i + c] = sample;
                }
                _samplesBufferReadPos = (_samplesBufferReadPos + 1) % _samplesBuffer.Length;
            }
            else
            {
                //There is no data in the buffer (we're consuming faster than we're producing)
                //This probably means either Update() can't keep up or there's a sample rate mismatch
                //Just output silence
                for (int c = 0; c < channels; ++c)
                {
                    data[i + c] = 0.0f;
                }
            }
        }
    }
}
