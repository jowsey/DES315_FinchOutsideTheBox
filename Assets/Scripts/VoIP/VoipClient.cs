using System;
using System.Buffers;
using System.Linq;
using JetBrains.Annotations;
using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;
using VOIP.Util;

namespace VoIP
{
    public class VoipClient : NetworkBehaviour
    {
        public const int SampleRate = 48000;

        private readonly OpusProcessor _opus = new();
        [CanBeNull] private SpeexResampler _resampler;

        [ShowInInspector] private string _device;
        [SerializeField] private AudioSource _source;

        [SerializeField] private InputActionReference _pushToTalkAction;

        //Mic clip
        private AudioClip _micClip; //clip the mic will record into (loops)
        private int _micClipLengthSeconds = 5;
        private int _micReadPos;
        private int _micSampleRate;

        private bool _isRecording;

        //Buffer for accumulating audio ready for encoding, post-resampling if necessary
        private readonly RingBuffer<float> _accumulationBuffer = new(OpusProcessor.FrameSize * 2);

        //Buffer for incoming decoded audio samples
        private readonly RingBuffer<float> _receiveBuffer = new(OpusProcessor.FrameSize * 10);

        public void Start()
        {
            if (isLocalPlayer)
            {
                if (string.IsNullOrWhiteSpace(_device))
                {
                    _device = Microphone.devices[0];
                    Debug.LogWarning($"No mic set, defaulting to: \"{_device}\"");
                }
                else if (!Microphone.devices.Contains(_device))
                {
                    var partialMatch = Microphone.devices.FirstOrDefault(d => d.ToLower().Contains(_device.ToLower()));
                    if (partialMatch != null)
                    {
                        Debug.LogWarning($"Mic \"{_device}\" not found, using partial match: \"{partialMatch}\".");
                        _device = partialMatch;
                    }
                    else
                    {
                        var newDevice = Microphone.devices[0];
                        Debug.LogWarning($"Mic \"{_device}\" not found, defaulting to: \"{newDevice}\"");
                        _device = newDevice;
                    }
                }

                Microphone.GetDeviceCaps(_device, out _, out var maxFreq);
                _micSampleRate = maxFreq != 0 && maxFreq < SampleRate ? maxFreq : SampleRate;

                if (_micSampleRate != SampleRate)
                {
                    _resampler = new SpeexResampler((uint)maxFreq, SampleRate);
                    Debug.Log($"Mic will be resampled from {maxFreq / 1000f}kHz to {SampleRate / 1000}kHz.");
                }

                StartMic();
            }
            else
            {
                Debug.Log($"Speaker sample rate: {AudioSettings.outputSampleRate}");

                if (AudioSettings.outputSampleRate != SampleRate)
                {
                    _resampler = new SpeexResampler(SampleRate, (uint)AudioSettings.outputSampleRate);
                    Debug.Log($"Received audio will be resampled from {SampleRate / 1000f}kHz to {AudioSettings.outputSampleRate / 1000f}kHz.");
                }

                //To initiate OnAudioFilterRead()
                AudioClip clip = AudioClip.Create("VoIP Playback", SampleRate, 1, SampleRate, false);
                float[] silence = new float[SampleRate];
                clip.SetData(silence, 0); // todo necessary?

                _source.clip = clip;
                _source.loop = true;
                _source.Play();
            }
        }

        private void OnDestroy()
        {
            StopMic();

            _opus?.Dispose();
            _resampler?.Dispose();
        }

        public void StartMic()
        {
            if (_isRecording || Microphone.IsRecording(_device)) return;
            _isRecording = true;

            _micClip = Microphone.Start(_device, true, _micClipLengthSeconds, _micSampleRate);

            if (!_micClip)
            {
                Debug.LogWarning("Failed to start microphone.");
                _isRecording = false;
                return;
            }

            while (Microphone.GetPosition(_device) <= 0)
            {
                //busy wait while microphone initialises
                System.Threading.Thread.Sleep(1);
            }

            Debug.Log("Streaming started.");
        }

        public void StopMic()
        {
            if (!_isRecording || !Microphone.IsRecording(_device)) return;
            _isRecording = false;

            Microphone.End(_device);
            Debug.Log("Streaming stopped.");
        }

        public void Update()
        {
            if (!isLocalPlayer || !_isRecording || !Microphone.IsRecording(_device)) return;

            if (!_pushToTalkAction.action.IsPressed()) return;

            //Get available samples
            int numAvailableSamples;
            int micWritePos = Microphone.GetPosition(_device);
            if (micWritePos >= _micReadPos)
            {
                numAvailableSamples = micWritePos - _micReadPos;
            }
            else
            {
                //Wraparound
                numAvailableSamples = micWritePos + _micClip.samples - _micReadPos;
            }

            if (numAvailableSamples <= 0) return;

            //Copy new samples into resampled buffer
            float[] micSamples = ArrayPool<float>.Shared.Rent(numAvailableSamples);
            _micClip.GetData(micSamples.AsSpan(0, numAvailableSamples), _micReadPos);
            _micReadPos = (_micReadPos + numAvailableSamples) % _micClip.samples;

            if (_resampler != null)
            {
                float[] resampled = ArrayPool<float>.Shared.Rent(_resampler.GetResampledSize(numAvailableSamples));
                int newSampleCount = _resampler.Resample(micSamples, resampled);
                _accumulationBuffer.Write(resampled, newSampleCount);
                ArrayPool<float>.Shared.Return(resampled);
            }
            else
            {
                _accumulationBuffer.Write(micSamples, numAvailableSamples);
            }

            ArrayPool<float>.Shared.Return(micSamples);

            while (_accumulationBuffer.Available >= OpusProcessor.FrameSize)
            {
                float[] frameBuffer = ArrayPool<float>.Shared.Rent(OpusProcessor.FrameSize);
                _accumulationBuffer.ReadInto(frameBuffer, OpusProcessor.FrameSize);
                byte[] opusFrame = _opus.Encode(frameBuffer);
                ArrayPool<float>.Shared.Return(frameBuffer);

                CmdSendAudio(opusFrame);
            }
        }

        [Command(channel = Channels.Unreliable)]
        void CmdSendAudio(byte[] opusFrame)
        {
            RpcReceiveAudio(opusFrame);
        }

        [ClientRpc(channel = Channels.Unreliable, includeOwner = false)]
        void RpcReceiveAudio(byte[] opusFrame)
        {
            float[] samples = _opus.Decode(opusFrame);

            if (_resampler != null)
            {
                float[] resampled = ArrayPool<float>.Shared.Rent(_resampler.GetResampledSize(samples.Length));
                int newSampleCount = _resampler.Resample(samples, resampled);
                _receiveBuffer.Write(resampled, newSampleCount);
                ArrayPool<float>.Shared.Return(resampled);
            }
            else
            {
                _receiveBuffer.Write(samples);
            }
        }

        void OnAudioFilterRead(float[] data, int channels)
        {
            if (isLocalPlayer) return;

            int samplesNeeded = data.Length / channels;
            float[] samples = ArrayPool<float>.Shared.Rent(samplesNeeded);
            int samplesRead = _receiveBuffer.ReadInto(samples, samplesNeeded);

            //If not enough samples, fill the rest with silence
            if (samplesRead < samplesNeeded)
            {
                Debug.LogWarning($"Not enough samples available? Wanted {samplesNeeded} but only got {samplesRead}");
                Array.Clear(samples, samplesRead, samplesNeeded - samplesRead);
            }

            //Copy samples into data, duplicating for each channel
            for (int s = 0; s < samplesRead; s++)
            {
                for (int c = 0; c < channels; c++)
                {
                    data[s * channels + c] = samples[s];
                }
            }

            ArrayPool<float>.Shared.Return(samples);
        }
    }
}