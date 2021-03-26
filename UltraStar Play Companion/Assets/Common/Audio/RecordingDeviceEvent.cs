public class RecordingDeviceEvent
{
    public string DeviceName { get; private set; }

    public RecordingDeviceEvent(string deviceName)
    {
        this.DeviceName = deviceName;
    }
}
