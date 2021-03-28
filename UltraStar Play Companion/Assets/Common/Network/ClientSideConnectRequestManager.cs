using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;
using UniInject;
using UniRx;

// Disable warning about fields that are never assigned, their values are injected.
#pragma warning disable CS0649

public class ClientSideConnectRequestManager : MonoBehaviour, INeedInjection
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void InitOnLoad()
    {
        instance = null;
    }

    private static ClientSideConnectRequestManager instance;
    public static ClientSideConnectRequestManager Instance
    {
        get
        {
            if (instance == null)
            {
                ClientSideConnectRequestManager instanceInScene = FindObjectOfType<ClientSideConnectRequestManager>();
                instanceInScene.InitSingleInstance();
            }
            return instance;
        }
    }
    
    /**
     * This version number must to be increased when introducing breaking changes.
     */
    public static readonly int protocolVersion = 1;

    [Inject]
    private Settings settings;

    [Inject]
    private ClientSideMicSampleRecorder clientSideMicSampleRecorder;

    private const float ConnectRequestPauseInSeconds = 1f;
    private float nextConnectRequestTime;

    private readonly Subject<ConnectEvent> connectEventStream = new Subject<ConnectEvent>();
    public IObservable<ConnectEvent> ConnectEventStream => connectEventStream;
    
    private UdpClient clientUdpClient;
    private const int ConnectPortOnServer = 34567;
    private const int ConnectPortOnClient = 34568;

    private bool isListeningForConnectResponse;

    private bool hasBeenDestroyed;

    private bool IsConnected => serverMicrophonePort > 0;
    private int serverMicrophonePort;

    private int connectRequestCount;

    private ConcurrentQueue<ConnectResponseDto> serverResponseQueue = new ConcurrentQueue<ConnectResponseDto>();

    private bool isApplicationPaused;

    private Thread acceptMessageFromServerThread;
    
    private void Start()
    {
        InitSingleInstance();
        if (!Application.isPlaying || instance != this)
        {
            return;
        }

        GameObjectUtils.SetTopLevelGameObjectAndDontDestroyOnLoad(gameObject);
        
        clientUdpClient = new UdpClient(ConnectPortOnClient);
        acceptMessageFromServerThread = new Thread(poolHandle =>
        {
            while (!hasBeenDestroyed)
            {
                ClientAcceptMessageFromServer();
            }
        });
        acceptMessageFromServerThread.Start();
    }

    private void Update()
    {
        while (serverResponseQueue.TryDequeue(out ConnectResponseDto connectResponseDto))
        {
            if (connectResponseDto.microphonePort > 0)
            {
                serverMicrophonePort = connectResponseDto.microphonePort;
                connectEventStream.OnNext(new ConnectEvent
                {
                    IsSuccess = true,
                    ConnectRequestCount = connectRequestCount,
                    MicrophonePort = serverMicrophonePort,
                    ServerIpEndPoint = connectResponseDto.serverIpEndPoint,
                });
                connectRequestCount = 0;            
            }
        }
        
        if (!IsConnected
            && Time.time > nextConnectRequestTime
            && clientSideMicSampleRecorder.SampleRateHz.Value > 0
            && !isApplicationPaused)
        {
            nextConnectRequestTime = Time.time + ConnectRequestPauseInSeconds;
            ClientSendConnectRequest();
        }
    }

    private void InitSingleInstance()
    {
        if (instance != null
            && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        isApplicationPaused = pauseStatus;
        if (pauseStatus)
        {
            // Application is paused now (e.g. the app was moved to the background on Android)
            CloseConnectionAndReconnect();
        }

        Debug.Log("OnApplicationPause with pauseStatus: " + pauseStatus);
    }

    private void ClientAcceptMessageFromServer()
    {
        try
        {
            Debug.Log("Client listening for connect response on port " + clientUdpClient.GetPort());
            IPEndPoint serverIpEndPoint = new IPEndPoint(IPAddress.Any, 0);
            // Receive is a blocking call
            byte[] receivedBytes = clientUdpClient.Receive(ref serverIpEndPoint);
            string message = Encoding.UTF8.GetString(receivedBytes);
            HandleServerMessage(serverIpEndPoint, message);
        }
        catch (Exception e)
        {
            Debug.LogException(e);
        }
    }

    private void HandleServerMessage(IPEndPoint serverIpEndPoint, string message)
    {
        Debug.Log($"Received message from server {serverIpEndPoint} ({serverIpEndPoint.Address}): '{message}'");
        ConnectResponseDto connectResponseDto = JsonConverter.FromJson<ConnectResponseDto>(message);
        if (connectResponseDto.clientName.IsNullOrEmpty())
        {
            throw new ConnectRequestException("Malformed ConnectResponse: missing clientId.");
        }
        if (connectResponseDto.microphonePort <= 0)
        {
            throw new ConnectRequestException("Malformed ConnectResponse: invalid microphonePort.");
        }

        connectResponseDto.serverIpEndPoint = serverIpEndPoint;
        serverResponseQueue.Enqueue(connectResponseDto);
    }
    
    private void ClientSendConnectRequest()
    {   
        if (connectRequestCount > 0)
        {
            // Last attempt failed
            connectEventStream.OnNext(new ConnectEvent
            {
                IsSuccess = false,
                ConnectRequestCount = connectRequestCount,
            });
        }
        
        connectRequestCount++;
        try
        {
            ConnectRequestDto connectRequestDto = new ConnectRequestDto
            {
                protocolVersion = protocolVersion,
                clientName = settings.ClientName,
                microphoneSampleRate = clientSideMicSampleRecorder.SampleRateHz.Value,
            };
            byte[] requestBytes = Encoding.UTF8.GetBytes(connectRequestDto.ToJson());
            // UDP Broadcast (255.255.255.255)
            clientUdpClient.Send(requestBytes, requestBytes.Length, "255.255.255.255", ConnectPortOnServer);
            Debug.Log($"Client has sent ConnectRequest as broadcast. Request: {connectRequestDto.ToJson()}");
        }
        catch (Exception e)
        {
            Debug.LogException(e);
        }
    }

    private void OnDestroy()
    {
        hasBeenDestroyed = true;
        clientUdpClient?.Close();
    }

    public void CloseConnectionAndReconnect()
    {
        serverMicrophonePort = 0;
        connectEventStream.OnNext(new ConnectEvent
        {
            IsSuccess = false,
        });
    }
}
