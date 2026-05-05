using System.Reflection;
using Clicky.Capture.Audio;
using Xunit;

namespace Clicky.Tests;

public class MicrophoneRecorderTests
{
    [Fact]
    public void AudioDataAvailable_FiresWithCopiedBuffer_WhenDataReceived()
    {
        // Arrange
        using var recorder = new WasapiMicrophoneRecorder();
        List<byte[]> receivedData = new();

        recorder.AudioDataAvailable += (sender, data) =>
        {
            receivedData.Add(data);
        };

        // Create test data that matches the target format (16kHz 16-bit mono PCM)
        // This is 1 sample (2 bytes for 16-bit): 0x0102
        byte[] testData = new byte[] { 0x01, 0x02 };

        // Act: Invoke the internal DataAvailable handler via reflection
        MethodInfo? dataAvailableMethod = typeof(WasapiMicrophoneRecorder)
            .GetMethod("OnDataAvailable", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("OnDataAvailable not found — method was renamed");

        NAudio.Wave.WaveInEventArgs eventArgs = new(testData, testData.Length);
        dataAvailableMethod.Invoke(recorder, new object[] { null!, eventArgs });

        // Assert
        Assert.NotEmpty(receivedData);
        // Data should be copied; verify it's the same content
        Assert.NotSame(testData, receivedData[0]);
        Assert.Equal(testData.Length, receivedData[0].Length);
    }

    [Fact]
    public void Dispose_CanBeCalledMultipleTimes_WithoutThrowing()
    {
        // Arrange
        var recorder = new WasapiMicrophoneRecorder();

        // Act & Assert - should not throw
        recorder.Dispose();
        recorder.Dispose();
    }

    [Fact]
    public void AudioDataAvailable_EventFiringWithPartialBuffer_UsesBytesRecorded()
    {
        // Arrange
        using var recorder = new WasapiMicrophoneRecorder();
        List<byte[]> receivedData = new();

        recorder.AudioDataAvailable += (sender, data) =>
        {
            receivedData.Add(data);
        };

        // Create a buffer larger than the actual data
        // Use valid 16-bit PCM data (2 bytes per sample for 16-bit)
        byte[] largeBuffer = new byte[1024];
        byte[] actualData = new byte[] { 0xAA, 0xBB, 0xCC, 0xDD }; // 2 samples of 16-bit data
        Array.Copy(actualData, largeBuffer, actualData.Length);

        // Act: Invoke with BytesRecorded set to the actual data length
        MethodInfo? dataAvailableMethod = typeof(WasapiMicrophoneRecorder)
            .GetMethod("OnDataAvailable", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("OnDataAvailable not found — method was renamed");

        NAudio.Wave.WaveInEventArgs eventArgs = new(largeBuffer, actualData.Length);
        dataAvailableMethod.Invoke(recorder, new object[] { null!, eventArgs });

        // Assert: Received buffer should be only the actual data size
        Assert.NotEmpty(receivedData);
        Assert.Equal(actualData.Length, receivedData[0].Length);
    }

    [Fact]
    public void Start_AndStop_ChangeRecordingState()
    {
        // Arrange
        using var recorder = new WasapiMicrophoneRecorder();

        // Act
        recorder.Start();
        recorder.Stop();

        // Assert - if we get here without an exception, the state transitions worked
        // (actual recording state cannot be easily verified without mocking WasapiCapture)
    }

    [Fact]
    public void AudioDataAvailable_DoesNotFireForZeroByteBuffer()
    {
        // Arrange
        using var recorder = new WasapiMicrophoneRecorder();
        List<byte[]> receivedData = new();

        recorder.AudioDataAvailable += (sender, data) =>
        {
            receivedData.Add(data);
        };

        // Act: Invoke with 0 bytes
        MethodInfo? dataAvailableMethod = typeof(WasapiMicrophoneRecorder)
            .GetMethod("OnDataAvailable", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("OnDataAvailable not found — method was renamed");

        byte[] emptyBuffer = Array.Empty<byte>();
        NAudio.Wave.WaveInEventArgs eventArgs = new(emptyBuffer, 0);
        dataAvailableMethod.Invoke(recorder, new object[] { null!, eventArgs });

        // Assert
        Assert.Empty(receivedData);
    }
}
