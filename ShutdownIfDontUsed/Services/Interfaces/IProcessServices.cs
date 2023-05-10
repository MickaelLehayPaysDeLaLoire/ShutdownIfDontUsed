using System.Diagnostics;

namespace ShutdownIfDontUsed.Services.Interfaces;

/// <summary>
/// Process Services interface.
/// </summary>
public interface IProcessServices
{
    #region Methods

    bool StartProcessAsCurrentUser(
        string processCommandLine,
        string? processWorkingDirectory = null,
        Process? userProcess = null);

    #endregion
}