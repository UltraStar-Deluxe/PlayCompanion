using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using UniInject;
using UnityEngine;
using UniRx;

// Disable warning about fields that are never assigned, their values are injected.
#pragma warning disable CS0649

public class ClientSideMicDataSender : MonoBehaviour, INeedInjection
{
    public static ClientSideMicDataSender Instance
    {
        get
        {
            return GameObjectUtils.FindComponentWithTag<ClientSideMicDataSender>("ClientSideMicrophoneDataSender");
        }
    }
    
    [Inject]
    private ClientSideConnectRequestManager clientSideConnectRequestManager;

    [Inject]
    private ClientSideMicSampleRecorder clientSideMicSampleRecorder;

    // Max size of a single UDP datagram is roughly 65500 (depending on IP version, network limits, etc.)
    // See https://stackoverflow.com/questions/1098897/what-is-the-largest-safe-udp-packet-size-on-the-internet
    private const int MaxUdpDatagramLength = 65500;
    
    private UdpClient clientMicDataSender;
    public IPEndPoint serverMicDataReceiverEndPoint;
    
    private void Start()
    {
        clientMicDataSender = new UdpClient();
        clientSideConnectRequestManager.ConnectEventStream.Subscribe(UpdateConnectionStatus);
        clientSideMicSampleRecorder.RecordingEventStream.Subscribe(HandleNewMicSamples);
    }

    private void HandleNewMicSamples(RecordingEvent recordingEvent)
    {
        if (serverMicDataReceiverEndPoint != null)
        {
            SendMicData(recordingEvent);
        }
    }

    private void SendMicData(RecordingEvent recordingEvent)
    {
        // Copy from float array to byte array. Note that in a float there are sizeof(float) bytes.
        byte[] newByteData = new byte[recordingEvent.NewSampleCount * sizeof(float)];
        Buffer.BlockCopy(
            recordingEvent.MicSamples, recordingEvent.NewSamplesStartIndex * sizeof(float),
            newByteData, 0,
            recordingEvent.NewSampleCount * sizeof(float));

        try
        {
            int sendBytesLength = newByteData.Length < MaxUdpDatagramLength
                ? newByteData.Length
                : MaxUdpDatagramLength;
            DateTime now = DateTime.Now;
            Debug.Log($"Send datagram: {sendBytesLength} bytes at {now}:{now.Millisecond}");
            clientMicDataSender.Send(newByteData, sendBytesLength, serverMicDataReceiverEndPoint);
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            Debug.LogError(
                $"Failed sending mic data: {newByteData.Length} bytes ({recordingEvent.NewSampleCount} samples)");
        }
    }

    private void UpdateConnectionStatus(ConnectEvent connectEvent)
    {
        if (connectEvent.IsSuccess
            && connectEvent.MicrophonePort > 0
            && connectEvent.ServerIpEndPoint != null)
        {
            serverMicDataReceiverEndPoint = new IPEndPoint(connectEvent.ServerIpEndPoint.Address, connectEvent.MicrophonePort);
        }
        else
        {
            serverMicDataReceiverEndPoint = null;
            clientSideMicSampleRecorder.StopRecording();
        }
    }
}
