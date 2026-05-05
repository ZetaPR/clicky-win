namespace Clicky.Core;

/// <summary>Orchestrates the capture-and-analyze companion loop.</summary>
public interface ICompanionOrchestrator : IDisposable
{
    void Start();
}
