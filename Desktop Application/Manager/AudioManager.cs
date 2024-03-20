using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.IO;

namespace BaldursGateInworld.Manager
{
    internal class AudioManager
    {
        private static readonly AudioManager instance = new AudioManager();
        private WaveInEvent waveSource;
        private WaveOutEvent _player;
        private bool isRecording;
        private WaveChannel32 activeChannel;
        private Action<byte[]> audioCallback;
        private List<RawSourceWaveStream> _streamQueue = new List<RawSourceWaveStream>();
        private AudioManager() { }

        public static AudioManager Instance
        {
            get
            {
                return instance;
            }
        }

        public void StartRecording(Action<byte[]> callback)
        {
            if (!isRecording)
            {
                audioCallback = callback;
                waveSource = new WaveInEvent
                {
                    WaveFormat = new WaveFormat(16000, 1)
                };
                waveSource.DataAvailable += WaveSource_DataAvailable;
                waveSource.StartRecording();
                isRecording = true;
            }
        }

        public void StopRecording()
        {
            if (waveSource != null)
            {
                waveSource.StopRecording();
                waveSource.Dispose();
                waveSource = null;
            }
            isRecording = false;
        }

        public void TickSoundManager()
        {
            if (_streamQueue != null && _streamQueue.Count > 0)
            {
                var first = _streamQueue[0];

                if (_player == null)
                {
                    _player = new WaveOutEvent();
                    _player.Volume = 1f;
                }

                if (_player.PlaybackState == PlaybackState.Stopped)
                {
                    _streamQueue.RemoveAt(0);
                    activeChannel = new WaveChannel32(first);
                    activeChannel.PadWithZeroes = false;
                    activeChannel.Volume = lastVolume;
                    _player.Init(activeChannel);
                    _player.Play();
                }
            }
        }

        private float lastVolume = 1f;

        public void SetVolume(float volume)
        {
            if (activeChannel != null)
            {
                lastVolume = volume;
                activeChannel.Volume = volume;
            }
        }

        public bool IsTalking()
        {
            if (_player == null) return false;
            if (_player.PlaybackState == PlaybackState.Playing) return true;
            return false;
        }

        public void PushChunk(string chunk)
        {
            if (string.IsNullOrEmpty(chunk)) return;
            if (_streamQueue == null)
            {
                _streamQueue = new List<RawSourceWaveStream>();
            }

            var sampleRate = 22050;
            byte[] decodedBytes = Convert.FromBase64String(chunk);
            var ms = new MemoryStream(decodedBytes);
            var rs = new RawSourceWaveStream(ms, new NAudio.Wave.WaveFormat(sampleRate, 16, 1));
            _streamQueue.Add(rs);
        }

        public void ClearQueue()
        {
            _streamQueue.Clear();
        }

        public void StopEverythingAbruptly()
        {
            if (_player != null)
                _player.Stop();
            if (_streamQueue != null)
                _streamQueue.Clear();
        }

        private void WaveSource_DataAvailable(object sender, WaveInEventArgs e)
        {
            if (audioCallback != null)
            {
                audioCallback.Invoke(e.Buffer);
            }
        }
    }
}
