namespace Clicky.Core;

/// <summary>
/// Sends a fresh screenshot plus conversation history to the worker and returns
/// a verification result telling the orchestrator whether to advance, correct, or complete.
/// </summary>
public interface IStepVerifier
{
    Task<VerifyResult> VerifyAsync(
        byte[] screenshot,
        int screenshotWidth,
        int screenshotHeight,
        int stepNumber,
        string stepText,
        IReadOnlyList<LlmMessage> history,
        CancellationToken cancellationToken = default);
}
