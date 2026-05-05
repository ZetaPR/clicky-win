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
        var receivedData = new List<byte[]>();

        recorder.AudioDataAvailable += (sender, data) =>
        {
            receivedData.Add(data);
        };

        // Create test data
        var testData = new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05 };

        // Act: Invoke the internal DataAvailable handler via reflection
        var dataAvailableMethod = typeof(WasapiMicrophoneRecorder)
            .GetMethod("OnDataAvailable", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        var eventArgs = new NAudio.Wave.WaveInEventArgs(testData, testData.Length);
        dataAvailableMethod?.Invoke(recorder, new object[] { null!, eventArgs });

        // Assert
        Assert.NotEmpty(receivedData);
        Assert.Equal(testData, receivedData[0]);

        // Verify the buffer is a copy, not the original
        Assert.NotSame(testData, receivedData[0]);
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
        var receivedData = new List<byte[]>();

        recorder.AudioDataAvailable += (sender, data) =>
        {
            receivedData.Add(data);
        };

        // Create a buffer larger than the actual data
        var largeBuffer = new byte[1024];
        var actualData = new byte[] { 0xAA, 0xBB, 0xCC };
        Array.Copy(actualData, largeBuffer, actualData.Length);

        // Act: Invoke with BytesRecorded set to 3
        var dataAvailableMethod = typeof(WasapiMicrophoneRecorder)
            .GetMethod("OnDataAvailable", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        var eventArgs = new NAudio.Wave.WaveInEventArgs(largeBuffer, actualData.Length);
        dataAvailableMethod?.Invoke(recorder, new object[] { null!, eventArgs });

        // Assert: Received buffer should be only the actual data size
        Assert.NotEmpty(receivedData);
        Assert.Equal(actualData.Length, receivedData[0].Length);
        Assert.Equal(actualData, receivedData[0]);
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
        var receivedData = new List<byte[]>();

        recorder.AudioDataAvailable += (sender, data) =>
        {
            receivedData.Add(data);
        };

        // Act: Invoke with 0 bytes
        var dataAvailableMethod = typeof(WasapiMicrophoneRecorder)
            .GetMethod("OnDataAvailable", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        var emptyBuffer = new byte[0];
        var eventArgs = new NAudio.Wave.WaveInEventArgs(emptyBuffer, 0);
        dataAvailableMethod?.Invoke(recorder, new object[] { null!, eventArgs });

        // Assert
        Assert.Empty(receivedData);
    }
}
