using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using UniInject;
using UniRx;
using ProTrans;
using Button = UnityEngine.UIElements.Button;
using Toggle = UnityEngine.UIElements.Toggle;

// Disable warning about fields that are never assigned, their values are injected.
#pragma warning disable CS0649

public class MainSceneControl : MonoBehaviour, INeedInjection, UniInject.IBinder
{
    private const int ConnectRequestCountShowTroubleshootingHintThreshold = 3;
    
    [InjectedInInspector]
    public TextAsset versionPropertiesTextAsset;

    [InjectedInInspector]
    public UIDocument uiDoc;
    
    [InjectedInInspector]
    public AudioWaveFormVisualization audioWaveFormVisualizer;

    [InjectedInInspector]
    public SongListRequestor songListRequestor;
    
    [Inject]
    private ClientSideConnectRequestManager clientSideConnectRequestManager;

    [Inject]
    private ClientSideMicSampleRecorder clientSideMicSampleRecorder;

    [Inject]
    private Settings settings;
    
    [Inject(Key = "#semanticVersionText")]
    private Label semanticVersionText;

    [Inject(Key = "#buildTimeStampText")]
    private Label buildTimeStampText;

    [Inject(Key = "#commitHashText")]
    private Label commitHashText;
    
    [Inject(Key = "#fpsText")]
    private Label fpsText;
    
    [Inject(Key = "#toggleRecordingButton")]
    private Button toggleRecordingButton;
    
    [Inject(Key = "#recordingDeviceButtonContainer")]
    private VisualElement recordingDeviceButtonContainer;
    
    [Inject(Key = "#connectionStatusText")]
    private Label connectionStatusText;

    [Inject(Key = "#selectedRecordingDeviceText")]
    private Label selectedRecordingDeviceText;
    
    [Inject(Key = "#clientNameTextField")]
    private TextField clientNameTextField;
    
    [Inject(Key = "#visualizeAudioToggle")]
    private Toggle visualizeAudioToggle;
    
    [Inject(Key = "#audioWaveForm")]
    private VisualElement audioWaveForm;

    [Inject(Key = "#connectionThroubleshootingText")]
    private Label connectionThroubleshootingText;
    
    [Inject(Key = "#serverErrorResponseText")]
    private Label serverErrorResponseText;

    [Inject(Key = "#songListContainer")]
    private VisualElement songListContainer;
    
    [Inject(Key = "#songListView")]
    private ScrollView songListView;
    
    [Inject(Key = "#showSongListButton")]
    private Button showSongListButton;
    
    [Inject(Key = "#closeSongListButton")]
    private Button closeSongListButton;
    
    [Inject(Key = "#sceneTitle")]
    private Label sceneTitle;

    private float frameCountTime;
    private int frameCount;
    
    private void Start()
    {
        clientSideMicSampleRecorder.DeviceName
            .Subscribe(_ => UpdateSelectedRecordingDeviceText());
        clientSideMicSampleRecorder.SampleRate
            .Subscribe(_ => UpdateSelectedRecordingDeviceText());
        clientSideMicSampleRecorder.IsRecording
            .Subscribe(OnRecordingStateChanged);

        // All controls are hidden until a connection has been established.
        uiDoc.rootVisualElement.Query(null, "onlyVisibleWhenConnected").ForEach(it => it.Hide());
        connectionThroubleshootingText.Hide();
        serverErrorResponseText.Hide();
        
        toggleRecordingButton.RegisterCallbackButtonTriggered(ToggleRecording);

        clientNameTextField.value = settings.ClientName;
        clientNameTextField.RegisterCallback<NavigationSubmitEvent>(_ => OnClientNameTextFieldSubmit());
        clientNameTextField.RegisterCallback<BlurEvent>(_ => OnClientNameTextFieldSubmit());
        
        visualizeAudioToggle.value = settings.ShowAudioWaveForm;
        audioWaveForm.SetVisible(settings.ShowAudioWaveForm);
        visualizeAudioToggle.RegisterValueChangedCallback(changeEvent =>
        {
            audioWaveForm.SetVisible(changeEvent.newValue);
            settings.ShowAudioWaveForm = changeEvent.newValue;
        });
        
        clientSideConnectRequestManager.ConnectEventStream
            .Subscribe(UpdateConnectionStatus);

        songListRequestor.SongListEventStream.Subscribe(evt => HandleSongListEvent(evt));
        
        showSongListButton.RegisterCallbackButtonTriggered(() => ShowSongList());
        closeSongListButton.RegisterCallbackButtonTriggered(() => songListContainer.Hide());
        
        UpdateVersionInfoText();
    }

    public void UpdateTranslation()
    {
        if (!Application.isPlaying && connectionStatusText == null)
        {
            SceneInjectionManager.Instance.DoInjection();
        }
        connectionStatusText.text = TranslationManager.GetTranslation(R.Messages.connecting);
        sceneTitle.text = TranslationManager.GetTranslation(R.Messages.title);
        visualizeAudioToggle.label = TranslationManager.GetTranslation(R.Messages.visualizeMicInput);
        showSongListButton.text = TranslationManager.GetTranslation(R.Messages.songList_show);
        closeSongListButton.text = TranslationManager.GetTranslation(R.Messages.songList_hide);
    }
    
    private void HandleSongListEvent(SongListEvent evt)
    {
        songListView.Clear();
        if (!evt.ErrorMessage.IsNullOrEmpty())
        {
            AddSongListLabel(evt.ErrorMessage);
            return;
        }

        evt.LoadedSongsDto.SongList.Sort((a,b) => string.Compare(a.Artist, b.Artist, StringComparison.InvariantCulture));
        foreach (SongDto songDto in evt.LoadedSongsDto.SongList)
        {
            AddSongListLabel(songDto.Artist + " - " + songDto.Title);
        }

        if (!evt.LoadedSongsDto.IsSongScanFinished)
        {
            AddSongListLabel("...");
        }
    }

    private void ShowSongList()
    {
        songListContainer.Show();
        
        if (!songListRequestor.SuccessfullyLoadedAllSongs)
        {
            songListView.Clear();
            AddSongListLabel("Loading songs list...");
            songListRequestor.RequestSongList();
        }
    }

    private void Update()
    {
        if (audioWaveForm.style.display != DisplayStyle.None)
        {
            audioWaveFormVisualizer.DrawWaveFormMinAndMaxValues(clientSideMicSampleRecorder.MicSampleBuffer);
        }
        UpdateFps();
    }

    private void UpdateFps()
    {
        frameCountTime += Time.deltaTime;
        frameCount++;
        if (frameCountTime > 1)
        {
            int fps = (int)(frameCount / frameCountTime);
            fpsText.text = TranslationManager.GetTranslation(R.Messages.fps, "value", fps);
            frameCount = 0;
            frameCountTime = 0;
        }
    }

    private void OnClientNameTextFieldSubmit()
    {
        settings.ClientName = clientNameTextField.value;
        // Reconnect to let the main know about the new clientName.
        clientSideConnectRequestManager.CloseConnectionAndReconnect();
    }

    private void UpdateSelectedRecordingDeviceText()
    {
        selectedRecordingDeviceText.text = $"{clientSideMicSampleRecorder.DeviceName.Value}\n({clientSideMicSampleRecorder.SampleRate.Value} Hz)";
    }

    private void OnRecordingStateChanged(bool isRecording)
    {
        if (isRecording)
        {
            toggleRecordingButton.text = TranslationManager.GetTranslation(R.Messages.stopRecording);
            toggleRecordingButton.AddToClassList("stopRecordingButton");
        }
        else
        {
            toggleRecordingButton.text = TranslationManager.GetTranslation(R.Messages.startRecording);
            toggleRecordingButton.RemoveFromClassList("stopRecordingButton");
        }
    }

    private void UpdateConnectionStatus(ConnectEvent connectEvent)
    {
        if (connectEvent.IsSuccess)
        {
            connectionStatusText.text = TranslationManager.GetTranslation(R.Messages.connectedTo, "remote" , connectEvent.ServerIpEndPoint.Address);
            uiDoc.rootVisualElement.Query(null, "onlyVisibleWhenConnected").ForEach(it => it.Show());
            audioWaveForm.SetVisible(settings.ShowAudioWaveForm);
            connectionThroubleshootingText.Hide();
            serverErrorResponseText.Hide();
            toggleRecordingButton.Focus();
            UpdateRecordingDeviceButtons();
        }
        else
        {
            connectionStatusText.text = connectEvent.ConnectRequestCount > 0
                ? TranslationManager.GetTranslation(R.Messages.connectingWithFailedAttempts, "count", connectEvent.ConnectRequestCount)
                : TranslationManager.GetTranslation(R.Messages.connecting);
            
            uiDoc.rootVisualElement.Query(null, "onlyVisibleWhenConnected").ForEach(it => it.Hide());
            if (connectEvent.ConnectRequestCount > ConnectRequestCountShowTroubleshootingHintThreshold)
            {
                connectionThroubleshootingText.Show();
                connectionThroubleshootingText.text = TranslationManager.GetTranslation(R.Messages.troubleShootingHints);
            }

            if (!connectEvent.ErrorMessage.IsNullOrEmpty())
            {
                serverErrorResponseText.Show();
                serverErrorResponseText.text = connectEvent.ErrorMessage;
            }
        }
    }

    private void UpdateRecordingDeviceButtons()
    {
        recordingDeviceButtonContainer.Clear();
        if (Microphone.devices.Length <= 1)
        {
            // No real choice
            return;
        }
        
        Microphone.devices.ForEach(device =>
        {
            Button deviceButton = new Button();
            deviceButton.RegisterCallbackButtonTriggered(
                () => clientSideMicSampleRecorder.SetRecordingDevice(device));
            deviceButton.text = $"{device}";
            recordingDeviceButtonContainer.Add(deviceButton);
        });
    }

    private void ToggleRecording()
    {
        if (clientSideMicSampleRecorder.IsRecording.Value)
        {
            clientSideMicSampleRecorder.StopRecording();
        }
        else
        {
            clientSideMicSampleRecorder.StartRecording();
        }
    }

    private void UpdateVersionInfoText()
    {
        Dictionary<string, string> versionProperties = PropertiesFileParser.ParseText(versionPropertiesTextAsset.text);

        // Show the release number (e.g. release date, or some version number)
        versionProperties.TryGetValue("release", out string release);
        semanticVersionText.text = TranslationManager.GetTranslation(R.Messages.version, "value", release);

        // Show the commit hash of the build
        versionProperties.TryGetValue("commit_hash", out string commitHash);
        commitHashText.text = TranslationManager.GetTranslation(R.Messages.commit, "value", commitHash);
        
        // Show the build timestamp only for development builds
        if (Debug.isDebugBuild)
        {
            versionProperties.TryGetValue("build_timestamp", out string buildTimeStamp);
            buildTimeStampText.text = TranslationManager.GetTranslation(R.Messages.buildTimeStamp, "value", buildTimeStamp);
        }
        else
        {
            buildTimeStampText.text = "";
        }
    }

    public List<UniInject.IBinding> GetBindings()
    {
        BindingBuilder bb = new BindingBuilder();
        bb.BindExistingInstance(uiDoc);
        return bb.GetBindings();
    }

    private void AddSongListLabel(string text)
    {
        Label label = new Label(text);
        label.AddToClassList("songListElement");
        label.style.whiteSpace = WhiteSpace.Normal;
        songListView.Add(label);
    }
}
