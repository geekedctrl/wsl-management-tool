namespace WSLManager.Desktop.Core.Backend;

public sealed class OperationState
{
    public bool IsBusy { get; private set; }
    public string Status { get; private set; } = "Ready";

    public bool TryBegin(string status)
    {
        if (IsBusy) return false;
        IsBusy = true;
        Status = status;
        return true;
    }

    public void End(string status)
    {
        IsBusy = false;
        Status = status;
    }
}
