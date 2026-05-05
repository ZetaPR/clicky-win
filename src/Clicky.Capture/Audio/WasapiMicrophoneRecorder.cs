using Clicky.Core;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace Clicky.Capture.Audio;

/// <summary>
/// Captures audio from the system microphone using Windows WASAPI.
/// Configured for 16kHz, 16-bit mono PCM.
/// </summary>
public sealed class WasapiMicrophoneRecorder : IMicrophoneRecorder
{
    private readonly WasapiCapture _capture;
    private bool _isRecording;
    private bool _disposed;

    public event EventHandler<byte[]>? AudioDataAvailable;

    public WasapiMicrophoneRecorder()
    {
        _capture = new WasapiCapture();
        _capture.WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(16000, 1);
        _capture.DataAvailable += OnDataAvailable;
    }

    public void Start()
    {
        if (!_isRecording)
        {
            _capture.StartRecording();
            _isRecording = true;
        }
    }

    public void Stop()
    {
        if (_isRecording)
        {
            _capture.StopRecording();
            _isRecording = false;
        }
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        if (e.BytesRecorded > 0)
        {
            byte[] buffer = new byte[e.BytesRecorded];
            Array.Copy(e.Buffer, 0, buffer, 0, e.BytesRecorded);
            AudioDataAvailable?.Invoke(this, buffer);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        Stop();
        _capture.DataAvailable -= OnDataAvailable;
        _capture.Dispose();
        _disposed = true;
    }
}
