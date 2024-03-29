using System;
using System.Collections;
using System.Collections.Generic;
using ProTrans;
using UniInject;
using UnityEngine;
using UniRx;

// Disable warning about fields that are never assigned, their values are injected.
#pragma warning disable CS0649

public class SongListRequestor : AbstractHttpRequestor
{
    private Subject<SongListEvent> songListEventStream = new Subject<SongListEvent>();
    public IObservable<SongListEvent> SongListEventStream => songListEventStream;
    
    public bool SuccessfullyLoadedAllSongs { get; private set; }

    public LoadedSongsDto LoadedSongsDto { get; private set; }

    public void RequestSongList()
    {
        if (serverIPEndPoint == null
            || httpServerPort == 0)
        {
            FireErrorMessageEvent(TranslationManager.GetTranslation(R.Messages.songList_error_notConnected));
            return;
        }
        
        string uri = $"http://{serverIPEndPoint.Address}:{httpServerPort}/api/rest/songs";
        Debug.Log("GET song list from URI: " + uri);

        StartCoroutine(WebRequestUtils.LoadTextFromUriCoroutine(uri,
            HandleSongListResponse,
            _ => FireErrorMessageEvent(TranslationManager.GetTranslation(R.Messages.songList_error_general))));
    }

    private void HandleSongListResponse(string downloadHandlerText)
    {
        try
        {
            LoadedSongsDto = JsonConverter.FromJson<LoadedSongsDto>(downloadHandlerText);
            if (!LoadedSongsDto.IsSongScanFinished
                && LoadedSongsDto.SongCount == 0)
            {
                SuccessfullyLoadedAllSongs = false;
                FireErrorMessageEvent(TranslationManager.GetTranslation(R.Messages.songList_error_noSongsFound));
                return;
            }

            if (LoadedSongsDto.IsSongScanFinished)
            {
                SuccessfullyLoadedAllSongs = true;
            }
            
            songListEventStream.OnNext(new SongListEvent
            {
                LoadedSongsDto = LoadedSongsDto,
            });
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            SuccessfullyLoadedAllSongs = false;
            FireErrorMessageEvent(TranslationManager.GetTranslation(R.Messages.songList_error_general));
        }
    }
    
    private void FireErrorMessageEvent(string errorMessage)
    {
        songListEventStream.OnNext(new SongListEvent
        {
            ErrorMessage = errorMessage,
        });
    }
}
