namespace ReqMint.Core.Workspaces;

public sealed class WorkspaceFormatException : Exception
{
    public WorkspaceFormatException(string message)
        : base(message)
    {
    }

    public WorkspaceFormatException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

