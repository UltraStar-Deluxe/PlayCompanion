﻿using System.Net;

public class ConnectEvent
{
    public bool IsSuccess { get; set; }
    public int ConnectRequestCount { get; set; }
    public int MicrophonePort { get; set; }
    public int HttpServerPort { get; set; }
    public string errorMessage { get; set; }
    public IPEndPoint ServerIpEndPoint { get; set; }
}
