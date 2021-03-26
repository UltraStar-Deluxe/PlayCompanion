using System;

[Serializable]
public class Settings
{
    public string ClientName { get; set; } = "MyCompanionApp";
    public string RecordingDeviceName { get; set; }
    public int TargetFps { get; set; } = 30;
    public bool ShowFps { get; set; }
}
