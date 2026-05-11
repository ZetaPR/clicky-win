namespace Clicky.Core;

/// <summary>A single turn in the conversation history sent to the worker on verify calls.</summary>
public sealed record LlmMessage(string Role, string Content);
