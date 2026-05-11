namespace Clicky.Core;

public enum VerifyOutcome { Advance, Correct, Complete }

public sealed record VerifyResult(
    VerifyOutcome Outcome,
    string SpokenText,
    int? NextX,
    int? NextY,
    string? NextLabel);
