using System;
using System.Collections.Generic;
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

        //Number of samples stored
        public const int ReceiveBufferSamples = OpusProcessor.FrameSize * 10;

        //Number of available samples before we start playing, gives some leeway for latency changes
        public const int JitterBufferSamples = OpusProcessor.FrameSize * 3;

        //Max number of samples behind head before we skip ahead to mitigate latency
        public const int BacklogSkipTriggerSamples = OpusProcessor.FrameSize * 5;

        //Max number of PLC frames to be generated after reaching end of receive buffer
        public const int MaxPlcFrames = 3;

        private readonly OpusProcessor _opus = new();
        [CanBeNull] private SpeexResampler _resampler;
        [CanBeNull] private RnNoiseProcessor _denoiser;

        [SerializeField] [ReadOnly] private string _device;
        [SerializeField] private AudioSource _source;

        [SerializeField] private InputActionReference _pushToTalkAction;

        [SerializeField] private Image _vcIcon;
        [SerializeField] private Sprite _vcActiveIcon;
        [SerializeField] private Sprite _vcInactiveIcon;

        [SerializeField] private PlayerController _player;

        //Mic clip
        private AudioClip _micClip;
        private int _micReadPos;
        private int _micSampleRate;

        private bool _isRecording;
        private volatile bool _playbackActive;

        //Number of PLC frames generated since last received audio
        private uint _plcFramesGenerated;

        //Latest sequence number received/sent for dropping out-of-order unreliable packets
        private uint _lastReceivedSequence;
        private uint _lastSentSequence;

        //Stores PCM frames sent to/received from Opus. Used on both sides
        private readonly float[] _opusFrameBuffer = new float[OpusProcessor.FrameSize];

        //Stores samples read from mic
        private float[] _micSamplesBuffer;

        //Stores resampled mic samples if necessary
        private float[] _sendResampleBuffer;

        //Accumulates samples ready for encoding, post-resampling if necessary
        private RingBuffer<float> _sendAccumulationBuffer;

        //Stores denoiser input/output samples
        private float[] _denoiseBuffer;

        //Stores Opus-encoded packet data to be sent
        private byte[] _opusPacketBuffer;

        //Stores incoming decoded audio samples
        private RingBuffer<float> _receivedSamplesBuffer;

        //Stores resampled received samples if necessary
        private float[] _receiveResampleBuffer;

        //Stores resampled PLC samples if necessary
        private float[] _plcResampleBuffer;

        //Stores samples ready to be played back
        private float[] _playbackSamplesBuffer;

        //Lock for encoding/decoding in main/audio threads
        private readonly object _opusLock = new();

        protected override void OnValidate()
        {
            if (!_player) _player = GetComponent<PlayerController>();
        }

        public void Start()
        {
            if (isLocalPlayer)
            {
                _denoiser = new RnNoiseProcessor();

                _sendAccumulationBuffer = new RingBuffer<float>(SampleRate);
                _denoiseBuffer = new float[RnNoiseProcessor.FrameSize];
                _opusPacketBuffer = new byte[OpusProcessor.MaxPacketSize];

                if (SettingsManager.ActiveSettings.InputDevice != null)
                {
                    SetMic(SettingsManager.ActiveSettings.InputDevice);
                    StartMic();
                }
            }
            else
            {
                _receivedSamplesBuffer = new RingBuffer<float>(ReceiveBufferSamples);

                AudioSettings.GetDSPBufferSize(out var dspBufferSize, out _);
                _playbackSamplesBuffer = new float[dspBufferSize];

                int outputRate = AudioSettings.outputSampleRate;
                if (outputRate != SampleRate)
                {
                    Debug.Log($"Received audio will be resampled from {SampleRate / 1000f}kHz to {outputRate / 1000f}kHz.");
                    _resampler = new SpeexResampler(SampleRate, (uint)outputRate);

                    var resampledSize = _resampler.GetResampledSize(OpusProcessor.FrameSize);
                    _receiveResampleBuffer = new float[resampledSize];
                    _plcResampleBuffer = new float[resampledSize];
                }

                //To initiate OnAudioFilterRead()
                AudioClip clip = AudioClip.Create("VoIP Playback", outputRate, 1, outputRate, false);

                _source.clip = clip;
                _source.loop = true;
                _source.Play();

                if (!_player.CutscenePlayer)
                {
                    _vcIcon.sprite = _vcInactiveIcon;
                }

                SettingsManager.ActiveSettings.PlayerVoiceVolumePercents.TryAdd(_player.PlayerUID, 100);
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

            _resampler?.Dispose();
            _resampler = null;

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

            _micSamplesBuffer = new float[_micClip.samples];

            if (_resampler != null)
            {
                var resampledSize = _resampler.GetResampledSize(_micClip.samples);
                _sendResampleBuffer = new float[resampledSize];
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

            if (_micClip)
            {
                Destroy(_micClip);
                _micClip = null;
            }

            _micReadPos = 0;
            _sendAccumulationBuffer?.Clear();
            _opus.ResetEncoderState();

            Debug.Log("Streaming stopped.");
        }

        public void Update()
        {
            if (!isLocalPlayer && !_player.CutscenePlayer)
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
                    _sendAccumulationBuffer.Clear();
                    _opus.ResetEncoderState();
                }
            }

            //Get available samples
            int numAvailableSamples = micWritePos >= _micReadPos
                ? micWritePos - _micReadPos
                : micWritePos + _micClip.samples - _micReadPos; //wraparound

            if (numAvailableSamples <= 0) return;

            //Copy new samples into resampled buffer
            _micClip.GetData(_micSamplesBuffer.AsSpan(0, numAvailableSamples), _micReadPos);
            _micReadPos = (_micReadPos + numAvailableSamples) % _micClip.samples;

            if (_resampler != null)
            {
                int newSampleCount = _resampler.Resample(_micSamplesBuffer, numAvailableSamples, _sendResampleBuffer);
                _sendAccumulationBuffer.Write(_sendResampleBuffer, newSampleCount);
            }
            else
            {
                _sendAccumulationBuffer.Write(_micSamplesBuffer, numAvailableSamples);
            }

            while (_sendAccumulationBuffer.Available >= OpusProcessor.FrameSize)
            {
                if (SettingsManager.ActiveSettings.NoiseSuppression)
                {
                    // 0-10ms
                    _sendAccumulationBuffer.ReadInto(_denoiseBuffer, RnNoiseProcessor.FrameSize);
                    var vad1 = _denoiser?.ProcessFrame(_denoiseBuffer, _denoiseBuffer);
                    Array.Copy(_denoiseBuffer, _opusFrameBuffer, RnNoiseProcessor.FrameSize);

                    // 10-20ms
                    _sendAccumulationBuffer.ReadInto(_denoiseBuffer, RnNoiseProcessor.FrameSize);
                    var vad2 = _denoiser?.ProcessFrame(_denoiseBuffer, _denoiseBuffer);
                    Array.Copy(_denoiseBuffer, 0, _opusFrameBuffer, RnNoiseProcessor.FrameSize, RnNoiseProcessor.FrameSize);

                    if (vad1 < RnNoiseProcessor.VoiceThreshold && vad2 < RnNoiseProcessor.VoiceThreshold)
                    {
                        // Entire frame is noise, don't bother sending
                        _lastSentSequence++;
                        continue;
                    }
                }
                else
                {
                    // Just passthrough accumulation buffer into opus frame buffer
                    _sendAccumulationBuffer.ReadInto(_opusFrameBuffer, OpusProcessor.FrameSize);
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
            if (seq <= _lastReceivedSequence)
            {
                // Debug.Log($"Received VoIP seq {seq}, but latest is already {_lastReceivedSequence}");
                return;
            }

            _lastReceivedSequence = seq;

            if (_receivedSamplesBuffer == null)
            {
                // we're either not meant to be a receiver or we're not done setting up, so drop
                return;
            }

            lock (_opusLock)
            {
                _opus.Decode(opusPacket, _opusFrameBuffer);
                _plcFramesGenerated = 0;

                if (_resampler != null)
                {
                    int newSampleCount = _resampler.Resample(_opusFrameBuffer, OpusProcessor.FrameSize, _receiveResampleBuffer);
                    _receivedSamplesBuffer.Write(_receiveResampleBuffer, newSampleCount);
                }
                else
                {
                    _receivedSamplesBuffer.Write(_opusFrameBuffer, OpusProcessor.FrameSize);
                }
            }
        }

        void OnAudioFilterRead(float[] data, int channels)
        {
            if (isLocalPlayer) return;

            //Clear entire buffer in case of gaps
            Array.Clear(data, 0, data.Length);

            //Skip excess if we're running behind
            if (_receivedSamplesBuffer.Available > BacklogSkipTriggerSamples)
            {
                // Debug.Log($"Receive buffer at {_receiveBuffer.Available / (float)OpusProcessor.FrameSize} frames, skipping ahead");

                // we explicitly skip to the edge of the jitter buffer instead of the backlog max to avoid constant tiny skips
                // the trigger is just an indicator of when the situation gets dire, not how many samples we'd *like* to have
                var excess = _receivedSamplesBuffer.Available - JitterBufferSamples;
                _receivedSamplesBuffer.Skip(excess);
            }

            int samplesRequested = data.Length / channels;

            //Start playback once we have a full jitter buffer
            if (!_playbackActive)
            {
                if (_receivedSamplesBuffer.Available < JitterBufferSamples)
                {
                    return;
                }

                _playbackActive = true;
            }

            lock (_opusLock)
            {
                //Generate PLC frames to fill in gaps if necessary
                while (_receivedSamplesBuffer.Available < samplesRequested && _plcFramesGenerated < MaxPlcFrames)
                {
                    lock (_opusLock) _opus.Decode(null, _opusFrameBuffer);
                    _plcFramesGenerated++;

                    // Debug.Log($"Generating PLC frame {_plcFramesGenerated}");

                    if (_resampler != null)
                    {
                        int newSampleCount = _resampler.Resample(_opusFrameBuffer, OpusProcessor.FrameSize, _plcResampleBuffer);
                        _receivedSamplesBuffer.Write(_plcResampleBuffer, newSampleCount);
                    }
                    else
                    {
                        _receivedSamplesBuffer.Write(_opusFrameBuffer, OpusProcessor.FrameSize);
                    }
                }

                if (_receivedSamplesBuffer.Available == 0)
                {
                    _playbackActive = false;
                    _opus.ResetDecoderState();
                    _plcFramesGenerated = 0;
                    return;
                }
            }

            int samplesRead = _receivedSamplesBuffer.ReadInto(_playbackSamplesBuffer, samplesRequested);

            float voiceVolLinear = SettingsManager.ActiveSettings.PlayerVoiceVolumePercents.GetValueOrDefault(_player.PlayerUID, 100f) / 100f;
            float voiceVolQuadratic = voiceVolLinear * voiceVolLinear;

            //Copy samples into data, duplicating for each channel
            for (int s = 0; s < samplesRead; s++)
            {
                float multipliedSample = _playbackSamplesBuffer[s] * voiceVolQuadratic; // apply quadratic gain
                for (int c = 0; c < channels; c++)
                {
                    data[s * channels + c] = multipliedSample;
                }
            }
        }
    }
}