using System;
using System.Buffers;
using System.Linq;
using JetBrains.Annotations;
using Mirror;
using UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using VoIP.Util;

namespace VoIP
{
    public class VoipClient : NetworkBehaviour
    {
        public const int SampleRate = 48000;

        //Number of available samples before we start playing, gives some leeway for latency changes
        public const int JitterBufferSamples = OpusProcessor.FrameSize * 3;

        //Number of samples we store before dropping old ones, prevents lagging too far behind head
        public const int ReceiveBufferSamples = OpusProcessor.FrameSize * 10;

        //Max number of PLC frames to be generated after reaching end of receive buffer
        public const int MaxPlcFrames = 3;

        private readonly OpusProcessor _opus = new();
        [CanBeNull] private SpeexResampler _resampler;
        [CanBeNull] private RnNoiseProcessor _denoiser;

        [SerializeField] private string _device;
        [SerializeField] private AudioSource _source;

        [SerializeField] private InputActionReference _pushToTalkAction;

        [SerializeField] private Image _vcIcon;
        [SerializeField] private Sprite _vcActiveIcon;
        [SerializeField] private Sprite _vcInactiveIcon;

        //Mic clip
        private AudioClip _micClip; //clip the mic will record into (loops)
        private int _micReadPos;
        private int _micSampleRate;

        private bool _isRecording;
        private volatile bool _playbackActive;

        //Buffer for accumulating audio ready for encoding, post-resampling if necessary
        private readonly RingBuffer<float> _accumulationBuffer = new(SampleRate);

        //Buffer for incoming decoded audio samples
        private readonly RingBuffer<float> _receiveBuffer = new(ReceiveBufferSamples);

        //Number of PLC frames generated since last received audio
        private uint _plcFramesGenerated;

        //Latest sequence number received/sent for dropping out-of-order unreliable packets
        private uint _lastReceivedSequence;
        private uint _lastSentSequence;

        private readonly float[] _denoiseBuffer = new float[RnNoiseProcessor.FrameSize];
        private readonly float[] _opusFrameBuffer = new float[OpusProcessor.FrameSize];
        private readonly byte[] _opusPacketBuffer = new byte[OpusProcessor.MaxPacketSize];

        public void Start()
        {
            if (isLocalPlayer)
            {
                _denoiser = new RnNoiseProcessor();
                if (SettingsManager.ActiveSettings.InputDevice != null)
                {
                    SetMic(SettingsManager.ActiveSettings.InputDevice);
                    StartMic();
                }
            }
            else
            {
                int outputRate = AudioSettings.outputSampleRate;
                if (outputRate != SampleRate)
                {
                    _resampler = new SpeexResampler(SampleRate, (uint)outputRate);
                    Debug.Log($"Received audio will be resampled from {SampleRate / 1000f}kHz to {outputRate / 1000f}kHz.");
                }

                //To initiate OnAudioFilterRead()
                AudioClip clip = AudioClip.Create("VoIP Playback", outputRate, 1, outputRate, false);

                _source.clip = clip;
                _source.loop = true;
                _source.Play();

                _vcIcon.sprite = _vcInactiveIcon;
            }
        }

        public void SetDevice(string inputDevice)
        {
            StopMic();
            SetMic(inputDevice);
            if (_device != null)
            {
                StartMic();
            }
        }

        private void SetMic(string deviceName)
        {
            _device = deviceName;
            if (string.IsNullOrWhiteSpace(_device)) return;

            if (!Microphone.devices.Contains(_device))
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
        }

        private void OnDestroy()
        {
            StopMic();

            _resampler?.Dispose();
            _denoiser?.Dispose();
            _opus?.Dispose();
        }

        public void StartMic()
        {
            if (_isRecording || Microphone.IsRecording(_device)) return;
            _isRecording = true;

            _micClip = Microphone.Start(_device, true, 1, _micSampleRate);

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
            if (!_isRecording) return;
            _isRecording = false;

            Microphone.End(_device);
            Debug.Log("Streaming stopped.");
        }

        public void Update()
        {
            if (!isLocalPlayer)
            {
                _vcIcon.sprite = _playbackActive ? _vcActiveIcon : _vcInactiveIcon;
            }

            if (!isLocalPlayer || !_isRecording || !Microphone.IsRecording(_device)) return;

            int micWritePos = Microphone.GetPosition(_device);

            if (SettingsManager.ActiveSettings.PushToTalk)
            {
                //Do nothing if PTT inactive
                if (!_pushToTalkAction.action.IsPressed()) return;

                //When starting a new PTT block, reset reading state
                if (_pushToTalkAction.action.WasPressedThisFrame())
                {
                    _micReadPos = micWritePos;
                    _accumulationBuffer.Clear();
                    _opus.ResetEncoderState();
                }
            }

            //Get available samples
            int numAvailableSamples = micWritePos >= _micReadPos
                ? micWritePos - _micReadPos
                : micWritePos + _micClip.samples - _micReadPos; //wraparound

            if (numAvailableSamples <= 0) return;

            //Copy new samples into resampled buffer
            float[] micSamples = ArrayPool<float>.Shared.Rent(numAvailableSamples);
            _micClip.GetData(micSamples.AsSpan(0, numAvailableSamples), _micReadPos);
            _micReadPos = (_micReadPos + numAvailableSamples) % _micClip.samples;

            if (_resampler != null)
            {
                float[] resampled = ArrayPool<float>.Shared.Rent(_resampler.GetResampledSize(numAvailableSamples));
                int newSampleCount = _resampler.Resample(micSamples, numAvailableSamples, resampled);
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
                if (SettingsManager.ActiveSettings.NoiseSuppression)
                {
                    // 0-10ms
                    _accumulationBuffer.ReadInto(_denoiseBuffer, RnNoiseProcessor.FrameSize);
                    var vad1 = _denoiser?.ProcessFrame(_denoiseBuffer, _denoiseBuffer);
                    Array.Copy(_denoiseBuffer, _opusFrameBuffer, RnNoiseProcessor.FrameSize);

                    // 10-20ms
                    _accumulationBuffer.ReadInto(_denoiseBuffer, RnNoiseProcessor.FrameSize);
                    var vad2 = _denoiser?.ProcessFrame(_denoiseBuffer, _denoiseBuffer);
                    Array.Copy(_denoiseBuffer, 0, _opusFrameBuffer, RnNoiseProcessor.FrameSize, RnNoiseProcessor.FrameSize);

                    if (vad1 < RnNoiseProcessor.VoiceThreshold && vad2 < RnNoiseProcessor.VoiceThreshold)
                    {
                        // Entire frame is noise, don't bother sending
                        continue;
                    }
                }
                else
                {
                    // Just passthrough accumulation buffer into opus frame buffer
                    _accumulationBuffer.ReadInto(_denoiseBuffer, OpusProcessor.FrameSize);
                    Array.Copy(_denoiseBuffer, _opusFrameBuffer, OpusProcessor.FrameSize);
                }

                int packetSize = _opus.Encode(_opusFrameBuffer, _opusPacketBuffer);

                var packetSegment = new ArraySegment<byte>(_opusPacketBuffer, 0, packetSize);
                CmdSendAudio(++_lastSentSequence, packetSegment);
            }
        }

        [Command(channel = Channels.Unreliable)]
        void CmdSendAudio(uint seq, ArraySegment<byte> opusPacket)
        {
            RpcReceiveAudio(seq, opusPacket);
        }

        [ClientRpc(channel = Channels.Unreliable, includeOwner = false)]
        void RpcReceiveAudio(uint seq, ArraySegment<byte> opusPacket)
        {
            // UDP out-of-order check
            if (seq < _lastReceivedSequence)
            {
                Debug.Log($"Received VoIP seq {seq}, but latest is {_lastReceivedSequence}");
                return;
            }

            _lastReceivedSequence = seq;
            _plcFramesGenerated = 0;

            _opus.Decode(opusPacket, _opusFrameBuffer);

            if (_resampler != null)
            {
                float[] resampled = ArrayPool<float>.Shared.Rent(_resampler.GetResampledSize(OpusProcessor.FrameSize));
                int newSampleCount = _resampler.Resample(_opusFrameBuffer, OpusProcessor.FrameSize, resampled);
                _receiveBuffer.Write(resampled, newSampleCount);
                ArrayPool<float>.Shared.Return(resampled);
            }
            else
            {
                _receiveBuffer.Write(_opusFrameBuffer, OpusProcessor.FrameSize);
            }
        }

        void OnAudioFilterRead(float[] data, int channels)
        {
            if (isLocalPlayer) return;

            //Clear entire buffer in case of gaps
            Array.Clear(data, 0, data.Length);

            int samplesRequested = data.Length / channels;

            if (!_playbackActive)
            {
                if (_receiveBuffer.Available < JitterBufferSamples)
                {
                    return;
                }

                _playbackActive = true;
            }

            //Generate PLC frames to fill in gaps if necessary
            while (_receiveBuffer.Available < samplesRequested && _plcFramesGenerated < MaxPlcFrames)
            {
                _opus.Decode(null, _opusFrameBuffer);
                _plcFramesGenerated++;

                // Debug.Log($"Generating PLC frame {_plcFramesGenerated}");

                // todo make reusable "resample or write" method
                if (_resampler != null)
                {
                    float[] resampled = ArrayPool<float>.Shared.Rent(_resampler.GetResampledSize(OpusProcessor.FrameSize));
                    int newSampleCount = _resampler.Resample(_opusFrameBuffer, OpusProcessor.FrameSize, resampled);
                    _receiveBuffer.Write(resampled, newSampleCount);
                    ArrayPool<float>.Shared.Return(resampled);
                }
                else
                {
                    _receiveBuffer.Write(_opusFrameBuffer, OpusProcessor.FrameSize);
                }
            }

            if (_receiveBuffer.Available == 0)
            {
                _playbackActive = false;
                _opus.ResetDecoderState();
                _plcFramesGenerated = 0;
                return;
            }

            // todo i think samplesNeeded is consistent per output device, could pre-allocate if/when device changes
            float[] samples = ArrayPool<float>.Shared.Rent(samplesRequested);
            int samplesRead = _receiveBuffer.ReadInto(samples, samplesRequested);

            //Copy samples into data, duplicating for each channel
            for (int s = 0; s < samplesRead; s++)
            {
                for (int c = 0; c < channels; c++)
                {
                    float vcVolLin = SettingsManager.ActiveSettings.VoiceChatVolume;
                    data[s * channels + c] = samples[s] * (vcVolLin * vcVolLin); //quadratic gain
                }
            }

            ArrayPool<float>.Shared.Return(samples);
        }
    }
}