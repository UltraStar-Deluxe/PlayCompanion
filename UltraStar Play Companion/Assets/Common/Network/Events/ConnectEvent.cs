using System.Net;

public class ConnectEvent
{
    public bool IsSuccess { get; set; }
    public int ConnectRequestCount { get; set; }
    public int MicrophonePort { get; set; }
    public IPEndPoint ServerIpEndPoint { get; set; }
}
