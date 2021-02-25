using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Net;
using System.Threading;
using System.Text.RegularExpressions;
using Proyecto26;
using DTOs;
using RSG;
using UnityEngine.UI;

public class SketchfabManager : MonoBehaviour
{
    [Header("Sketchfab Manager")]
    public GameObject splashTextObj;

    protected static readonly string BASE_URL = "https://api.sketchfab.com/v3";
    protected string AccessToken => accessToken;

    private static readonly string CLIENT_ID = "XEIxNFzSDj23Lal5s1k9cbpOhkWZ6J7g9Y0KMrFk";
    // private static readonly string CLIENT_SECRET = "CLk5IEkuWrc03Di4fAp2rtFvAWFq3jFSEU0DwwCRoa8S2YNtgGxAQhm0hdrwh8bobz616su6SEdW3ywNwMCAsxM4V1PWPgvFd0olaE9Stls1QCY6B1HALbpQR3oK2TOS";
    private static readonly string LOGIN_URL = "https://sketchfab.com/oauth2/authorize/?state=123456789&response_type=token&client_id=" + CLIENT_ID;
    private static readonly string REDIRECT_URI = "http://localhost";
    private Thread serverThread;
    private HttpListener listener;
    private readonly string ACCESS_TOKEN_PP_KEY = "token";
    private string accessToken;

    protected virtual void Start()
    {
        accessToken = PlayerPrefs.GetString(ACCESS_TOKEN_PP_KEY, "");
    }

    protected virtual void Update() { }

    public IPromise<ModelResponse> FetchModelsAsync(string queryString = "?downloadable=true&sort_by=viewCount")
    {
        return RestClient.Get<ModelResponse>($"{BASE_URL}/models{queryString ?? ""}");
    }

    public IPromise<ModelSearchResponse> SearchModelsAsync(string queryString = "?type=models&downloadable=true")
    {
        return RestClient.Get<ModelSearchResponse>($"{BASE_URL}/search{queryString ?? ""}");
    }

    public IPromise<CategoriesResponse> FetchCategoriesAsync()
    {
        return RestClient.Get<CategoriesResponse>(BASE_URL + "/categories");
    }

    public void AuthorizeApp()
    {
        serverThread = new Thread(() => StartServer(new string[] { REDIRECT_URI + "/" }));
        serverThread.Start();

        Application.OpenURL(LOGIN_URL);
    }

    public void StartServer(string[] prefixes)
    {
        if (!HttpListener.IsSupported)
        {
            Debug.LogError("Windows XP SP2 or Server 2003 is required to use the HttpListener class.");
            return;
        }

        listener = new HttpListener();

        foreach (string s in prefixes)
        {
            listener.Prefixes.Add(s);
        }

        listener.Start();
        Debug.Log("Server started...");

        while (true)
        {
            try
            {
                HttpListenerContext context = listener.GetContext();
                ProcessRequest(context);
            }
            catch (System.Exception e)
            {
                if (e is System.Threading.ThreadAbortException) return;
                Debug.LogError(e);
            }
        }
    }

    private void ProcessRequest(HttpListenerContext context)
    {
        HttpListenerRequest request = context.Request;
        Match m = Regex.Match(request.Url.AbsolutePath, @"access_token=[a-zA-Z|0-9]+", RegexOptions.IgnoreCase);

        if (m.Success)
        {
            // Access token found in url
            accessToken = m.Value.Split('=')[1];
            UnityDispatcher.InvokeOnAppThread(() => PlayerPrefs.SetString(ACCESS_TOKEN_PP_KEY, accessToken));

            HttpListenerResponse response = context.Response;
            string responseString = "<HTML><BODY><h2>VRMod Authorized!</h2></BODY></HTML>";
            byte[] buffer = System.Text.Encoding.UTF8.GetBytes(responseString);
            response.ContentLength64 = buffer.Length;

            System.IO.Stream output = response.OutputStream;
            output.Write(buffer, 0, buffer.Length);
            output.Close();

            UnityDispatcher.InvokeOnAppThread(() => StopServer());
        }
        else
        {
            // Construct a script response to send the access token via URL
            HttpListenerResponse response = context.Response;
            string responseString =
                "<HTML><BODY><script>window.open('http://localhost/' + window.location.hash.replace('#', ''), '_self');</script></BODY></HTML>";
            byte[] buffer = System.Text.Encoding.UTF8.GetBytes(responseString);
            response.ContentLength64 = buffer.Length;

            System.IO.Stream output = response.OutputStream;
            output.Write(buffer, 0, buffer.Length);
            output.Close();
        }
    }

    public void SplashText(string text, Color color)
    {
        if (splashTextObj != null)
        {
            UnityDispatcher.InvokeOnAppThread(() =>
            {
                StopCoroutine("SplashTextCoroutine");
                Text splashText = splashTextObj.GetComponentInChildren<Text>();
                Lean.Gui.LeanBox splashImage = splashTextObj.GetComponentInChildren<Lean.Gui.LeanBox>();
                StartCoroutine(SplashTextCoroutine(splashText, splashImage, text, color));
            });
        }
    }

    private IEnumerator SplashTextCoroutine(Text splashText, Lean.Gui.LeanBox splashImage, string text, Color color)
    {
        splashText.text = text;
        splashText.color = color;
        splashText.CrossFadeAlpha(1f, 0f, false);
        splashImage.CrossFadeAlpha(0.66667f, 0f, false);

        splashTextObj.gameObject.SetActive(true);
        splashText.CrossFadeAlpha(0f, 16f, false);
        splashImage.CrossFadeAlpha(0f, 16f, false);

        yield return new WaitWhile(() => splashText.color.a > 0f || splashImage.color.a > 0f);

        splashTextObj.gameObject.SetActive(false);
        yield return null;
    }

    private void StopServer()
    {
        if (serverThread != null)
            serverThread.Abort();
        if (listener != null)
            listener.Stop();
    }

    protected virtual void OnDestroy()
    {
        StopServer();
    }
}
