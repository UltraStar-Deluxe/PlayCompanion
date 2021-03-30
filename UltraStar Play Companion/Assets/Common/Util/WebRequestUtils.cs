using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

public static class WebRequestUtils
{
    public static IEnumerator LoadTextFromUriCoroutine(string uri, Action<string> onSuccess, Action<UnityWebRequest> onFailure = null)
    {
        using (UnityWebRequest webRequest = UnityWebRequest.Get(uri))
        {
            webRequest.SendWebRequest();

            while (!webRequest.isDone)
            {
                yield return null;
            }

            if (webRequest.isNetworkError || webRequest.isHttpError)
            {
                Debug.LogError("Error loading text from: " + uri);
                Debug.LogError(webRequest.error);
                if (onFailure != null)
                {
                    onFailure(webRequest);
                }
                yield break;
            }

            onSuccess(webRequest.downloadHandler.text);
        }
    }
}
