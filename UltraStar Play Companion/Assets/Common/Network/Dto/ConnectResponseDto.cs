﻿using System.Net;

public class ConnectResponseDto : JsonSerializable
{
    public string ClientName { get; set; }
    public int MicrophonePort { get; set; }
    public string ErrorMessage { get; set; }
    
    // This field is set by the client when the response is received.
    public IPEndPoint ServerIpEndPoint { get; set; }
}