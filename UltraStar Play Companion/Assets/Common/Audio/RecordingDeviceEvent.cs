public class RecordingDeviceEvent
{
    public string DeviceName { get; private set; }
    public int minSampleRateHz { get; private set; }
    public int maxSampleRateHz { get; private set; }
    
    public RecordingDeviceEvent(string deviceName, int minSampleRateHz, int maxSampleRateHz)
    {
        this.DeviceName = deviceName;
        this.minSampleRateHz = minSampleRateHz;
        this.maxSampleRateHz = maxSampleRateHz;
    }
}
