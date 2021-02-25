using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Firebase;
using Firebase.Database;
using Firebase.Unity.Editor;
using System.Threading.Tasks;
using UnityEngine.UI;
using System;
using UnityEngine.SceneManagement;
using Firebase.Extensions;

public abstract class FirebaseManager : MonoBehaviour
{
    [Header("Firebase Manager")]
    public GameObject splashTextObj;

    protected readonly string FIREBASE_URL = "https://vr-mod.firebaseio.com/";

    private Firebase.Auth.FirebaseAuth auth;
    private bool isReady = false;
    private Dictionary<string, LocationPayload> fetchedLocationsDict = new Dictionary<string, LocationPayload>();
    private Firebase.DependencyStatus dependencyStatus = Firebase.DependencyStatus.UnavailableOther;

    protected readonly string ANCHOR_ID_PLAYER_PREFS_KEY = "CURRENT_ANCHOR_ID";
    protected static readonly string LOGIN_EMAIL_PP_KEY = "LOGIN_EMAIL";
    protected static readonly string LOGIN_PASSWORD_PP_KEY = "LOGIN_PASSWORD";
    public static readonly string IS_CREATE_MODE_PP_KEY = "IS_CREATE_MODE";
    public static readonly string ANCHOR_ID_TO_DELETE_PP_KEY = "DELETING_ANCHORID";
    protected static readonly string NEW_LOCATIONID_PP_KEY = "NEW_LOCATION_ID";
    protected long NumMyLocations = 0;

    #region Public Properties
    public string UserId => auth.CurrentUser.UserId;
    public bool HasAnchorLocations => NumMyLocations > 0;
    public Dictionary<string, LocationPayload> FetchedLocationsDict => fetchedLocationsDict;
    public static ModelPayload TranslateModel { get; set; }
    public bool IsAuthenticated => auth != null && auth.CurrentUser != null;
    public bool IsReady => isReady;
    #endregion // Public Properties


    protected virtual void Start()
    {
        InitFirebase();
    }

    protected virtual void Update()
    {
    }

    protected void InitFirebase()
    {
        isReady = false;

        FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task =>
        {
            if (task.IsCanceled || task.IsFaulted)
            {
                Debug.LogError("Failed to check & fix dependencies:");
                Debug.LogError(task.Exception.GetBaseException());
                return;
            }

            dependencyStatus = task.Result;

            if (dependencyStatus == DependencyStatus.Available)
            {

                auth = Firebase.Auth.FirebaseAuth.DefaultInstance;
                // Firebase.Analytics.FirebaseAnalytics.SetAnalyticsCollectionEnabled(true);
                FirebaseApp.DefaultInstance.SetEditorDatabaseUrl(FIREBASE_URL);

                if (!IsAuthenticated)
                {
                    auth.SignInAnonymouslyAsync().ContinueWithOnMainThread(task2 =>
                    {
                        if (task2.IsCanceled || task2.IsFaulted)
                        {
                            Debug.LogError(task2.Exception);
                            SplashText(task2.Exception.GetBaseException().Message, Color.red);
                            return;
                        }

                        TriggerFirebaseInitialized();
                    });
                }
                else
                {
                    TriggerFirebaseInitialized();
                }
            }
            else
            {
                Debug.LogError(string.Format("Could not resolve all Firebase dependencies: {0}", dependencyStatus));
            }
        });
    }

    private void TriggerFirebaseInitialized()
    {
        OnFirebaseInitialized().ContinueWithOnMainThread(task =>
        {
            if (task.IsCanceled || task.IsFaulted)
            {
                Debug.LogError("Failed on initialized:");
                Debug.LogError(task.Exception.GetBaseException());
                return;
            }

            isReady = true;
        });
    }

    protected void AuthenticateAnonymously()
    {
        auth.SignInAnonymouslyAsync().ContinueWithOnMainThread(task =>
        {
            if (task.IsCanceled || task.IsFaulted)
            {
                Debug.LogError(task.Exception);
                SplashText(task.Exception.GetBaseException().Message, Color.red);
                return;
            }

            Debug.Log("Signed in anonymously");
        });
    }

    protected void SignOut()
    {
        Debug.Log("Signing out.");
        auth.SignOut();
        if (SceneManager.GetActiveScene().name != "Login UI") SceneManager.LoadScene("Login UI");
    }

    protected string GetCurrentUserEmail()
    {
        if (IsAuthenticated)
            return auth.CurrentUser.Email;
        return "";
    }

    protected void CreateEmailPassword(string email, string password)
    {
        auth.CreateUserWithEmailAndPasswordAsync(email, password).ContinueWith(task =>
        {
            if (task.IsCanceled || task.IsFaulted)
            {
                Debug.LogError(task.Exception);
                SplashText(task.Exception.GetBaseException().Message, Color.red);

                return;
            }

            OnUserCreateSuccessful(task.Result.UserId);
        });
    }

    protected void LoginEmailPassword(string email, string password)
    {
        auth.SignInWithEmailAndPasswordAsync(email, password)
        .ContinueWith(task =>
        {
            if (task.IsCanceled || task.IsFaulted)
            {
                Debug.LogError("Login encountered an error or was cancelled:\t" + task.Exception);
                SplashText(task.Exception.GetBaseException().Message, Color.red);
                return;
            }

            OnUserLoginSuccessful();
        });
    }

    protected void SendPasswordResetEmail(string email)
    {
        auth.SendPasswordResetEmailAsync(email).ContinueWith(task =>
        {
            if (task.IsFaulted || task.IsCanceled)
            {
                SplashText(task.Exception.GetBaseException().Message, Color.red);
                Debug.LogError(task.Exception);
                return;
            }

            SplashText("Password Reset Email Sent", Color.green);
        });
    }

    protected Task SendVerificationEmail()
    {
        if (!IsAuthenticated)
        {
            Debug.LogError("Not authenticated");
            return Task.CompletedTask;
        }

        return auth.CurrentUser.SendEmailVerificationAsync();
    }

    protected virtual void OnUserCreateSuccessful(string userId) { }

    protected virtual void OnUserLoginSuccessful() { }

    protected virtual Task OnFirebaseInitialized() { return Task.CompletedTask; }

    protected virtual async Task<Dictionary<string, LocationPayload>> FetchUserLocationAnchorIdsAsync(string userId, bool setLinkedModels = false)
    {
        try
        {
            if (string.IsNullOrEmpty(userId))
            {
                Debug.LogError("No user selected");
                return fetchedLocationsDict;
            }

            DataSnapshot snapshot = await FirebaseDatabase.DefaultInstance.GetReference("users" + "/" + userId + "/locations").GetValueAsync();
            fetchedLocationsDict.Clear();

            if (snapshot.Exists && snapshot.HasChildren)
            {
                if (userId == UserId)
                    NumMyLocations = snapshot.ChildrenCount;

                foreach (DataSnapshot location in snapshot.Children)
                {
                    string locationId = location.Key.ToString();

                    string locKey0 = "locationName";
                    string locKey1 = "locationDesc";
                    if (location.HasChild(locKey0) && location.HasChild(locKey1))
                    {
                        fetchedLocationsDict.Add(locationId, new LocationPayload(
                            location.Child(locKey0).Value.ToString(), location.Child(locKey1).Value.ToString()));
                    }
                    else if (location.HasChild(locKey0))
                    {
                        fetchedLocationsDict.Add(locationId, new LocationPayload(
                            location.Child(locKey0).Value.ToString(), ""));
                    }
                    else
                    {
                        fetchedLocationsDict.Add(locationId, new LocationPayload(locationId, ""));
                        Debug.LogWarning("No location info found... Using location ID instead");
                    }

                    var newLocation = fetchedLocationsDict[locationId];
                    newLocation.HasModel = location.HasChild("filePath");

                    if (setLinkedModels && newLocation.HasModel)
                    {
                        newLocation.ModelPayload = new ModelPayload(
                            location.Child("fileName").Value.ToString(),
                            location.Child("filePath").Value.ToString(),
                            location.Child("fileDesc").Value.ToString()
                            );
                    }
                }
            }
            else
            {
                Debug.Log("No Locations Found For User");

                if (userId == UserId)
                    NumMyLocations = 0;
            }
        }
        catch (Exception ex)
        {
            Debug.LogError(ex);
            fetchedLocationsDict.Clear();
            SplashText(ex.Message, Color.red);

            if (userId == UserId)
                NumMyLocations = 0;
        }

        return fetchedLocationsDict;
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

    protected Dictionary<string, object> GetFullPayload(LocationPayload lp, ModelPayload mp)
    {
        Dictionary<string, object> result = new Dictionary<string, object>();
        result["locationName"] = lp.name;
        result["locationDesc"] = lp.description;
        result["fileName"] = mp.fileName;
        result["filePath"] = mp.filePath;
        result["fileDesc"] = mp.fileDesc;

        if (mp.HasMyFileData())
        {
            var dict = mp.ToDictionary();
            result["myFilePath"] = dict["myFilePath"];
            result["myFileName"] = dict["myFileName"];
            result["myFileDesc"] = dict["myFileDesc"];
        }

        return result;
    }

    public class LocationPayload
    {
        public string name;
        public string description;
        public bool HasModel { get; set; }
        public ModelPayload ModelPayload { get; set; }

        public LocationPayload(string name, string description)
        {
            this.name = name;
            this.description = description;
        }

        public Dictionary<string, object> ToDictionary()
        {
            Dictionary<string, object> result = new Dictionary<string, object>();
            result["locationName"] = name;
            result["locationDesc"] = description;

            return result;
        }

        public override string ToString()
        {
            return name + (!string.IsNullOrEmpty(description) ? " - " + description : "");
        }
    }

    public class ModelPayload
    {
        public string fileName;
        public string filePath;
        public string fileDesc;

        private string myFilePath;
        private string myFileName;
        private string myFileDesc;

        public ModelPayload(string fileName, string filePath, string fileDesc)
        {
            this.fileName = fileName;
            this.filePath = filePath;
            this.fileDesc = fileDesc;
        }

        public void SetMyFileData(string myFileName, string myFilePath, string myFileDesc)
        {
            this.myFileName = myFileName;
            this.myFilePath = myFilePath;
            this.myFileDesc = myFileDesc;
        }

        public override string ToString()
        {
            return fileName.Split('.')[0] + (string.IsNullOrEmpty(fileDesc) ? "" : " - " + fileDesc);
        }

        public Dictionary<string, object> ToDictionary()
        {
            Dictionary<string, object> result = new Dictionary<string, object>();
            result["fileName"] = fileName;
            result["filePath"] = filePath;
            result["fileDesc"] = fileDesc;

            if (HasMyFileData())
            {
                result["myFilePath"] = myFilePath;
                result["myFileName"] = myFileName;
                result["myFileDesc"] = myFileDesc;
            }

            return result;
        }

        public bool HasMyFileData()
        {
            return !string.IsNullOrEmpty(myFilePath);
        }
    }
}
