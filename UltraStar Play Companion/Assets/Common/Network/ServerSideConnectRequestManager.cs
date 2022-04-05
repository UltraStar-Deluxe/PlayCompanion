/**
 * Dummy implementation for Companion App.
 * Companion App does not connect to other clients for receiving mic samples.
 */
public class ServerSideConnectRequestManager
{
    public static bool TryGetConnectedClientHandler(string connectedClientId, out IConnectedClientHandler connectedClientHandler)
    {
        connectedClientHandler = null;
        return false;
    }
}
