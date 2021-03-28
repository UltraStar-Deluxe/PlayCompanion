using System.Net;

public class ConnectResponseDto : JsonSerializable
{
    public string clientName;
    public int microphonePort;
    public string errorMessage;
    
    // This field is set by the client when the response is received.
    public IPEndPoint serverIpEndPoint;
}
