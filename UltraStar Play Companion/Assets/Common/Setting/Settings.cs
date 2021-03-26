using System;

[Serializable]
public class Settings
{
    public RecordingSettings RecordingSettings { get; set; }
    public GraphicSettings GraphicSettings { get; set; } = new GraphicSettings();
    public DeveloperSettings DeveloperSettings { get; set; } = new DeveloperSettings();
}
