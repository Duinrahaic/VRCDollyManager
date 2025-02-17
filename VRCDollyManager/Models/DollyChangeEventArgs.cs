namespace VRCDollyManager.Models;

public class DollyChangedEventArgs : EventArgs
{
    public string FileName { get; }
    public DollyChangeType ChangeType { get; }

    public DollyChangedEventArgs(string fileName, DollyChangeType changeType)
    {
        FileName = fileName;
        ChangeType = changeType;
    }
}