using System.Collections.Generic;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Firebase.Database;
using System.Threading.Tasks;
using System;

public class ModelSelector : FirebaseLoader
{
    [Header("Model Selector")]
    public Dropdown modelDropdown;
    public Button urlButton;
    public Button saveButton;
    public InputField urlTextInput;
    public Sprite usernameSprite;
    public Sprite otherSprite;

    public ModelPayload SelectedModel;

    // Key - userID of model owner
    private Dictionary<string, List<ModelPayload>> modelDict = new Dictionary<string, List<ModelPayload>>();
    private List<string> resUsernames = new List<string>();



    // Start is called before the first frame update
    protected override void Start()
    {
        SetMainButtonAction(true);
        modelDropdown.options.Insert(0, new Dropdown.OptionData("Loading..."));

        base.Start();
    }

    protected override async Task OnFirebaseInitialized()
    {
        try
        {
            modelDict = await GetAllModelInfoAsync();
            resUsernames.Clear();

            foreach (KeyValuePair<string, List<ModelPayload>> entry in modelDict)
            {
                List<ModelPayload> models = entry.Value;
                for (int i = 0; i < models.Count; i++)
                {
                    bool isUser = entry.Key == UserId;

                    // Username header if start of new list
                    if (i == 0)
                    {
                        DataSnapshot snapshot = await FirebaseDatabase.DefaultInstance.GetReference(entry.Key + "/username").GetValueAsync();
                        string username = snapshot.Value.ToString();
                        resUsernames.Add(username);
                        modelDropdown.options.Insert(isUser ? 1 : modelDropdown.options.Count, new Dropdown.OptionData(username, usernameSprite));
                    }

                    ModelPayload m = models[i];
                    modelDropdown.options.Insert(isUser ? 2 : modelDropdown.options.Count, new Dropdown.OptionData(m.ToString(), otherSprite));
                }
            }

            modelDropdown.options[0].text = "";
            modelDropdown.Hide();
            modelDropdown.RefreshShownValue();
        }
        catch (Exception ex)
        {
            Debug.LogError(ex);
            SplashText(ex.Message, Color.red);
        }
    }

    public void OnCloseButtonPressed()
    {
        modelDropdown.transform.root.gameObject.SetActive(false);
    }

    public void OnSaveButtonPressed()
    {
        ModelPayload selectedModel = null;
        string modelOption = modelDropdown.options[modelDropdown.value].text;

        foreach (List<ModelPayload> userModels in modelDict.Values)
        {
            foreach (ModelPayload m in userModels)
            {
                if (modelOption == m.ToString())
                {
                    selectedModel = m;
                    break;
                }
            }

            if (selectedModel != null)
                break;
        }


        if (selectedModel == null)
        {
            SplashText("Select a 3D Model", Color.red);
        }

        SelectedModel = selectedModel;
        SetMainButtonAction(false);
    }

    public void OnURLButtonPressed(string url)
    {
        urlButton.gameObject.SetActive(false);
        urlTextInput.gameObject.SetActive(true);
        urlTextInput.text = url;

        StopCoroutine("OpenURLCoroutine");
        StartCoroutine(OpenURLCoroutine(url));
    }

    private IEnumerator OpenURLCoroutine(string url)
    {
        SetMainButtonAction(false);
        SplashText("For The Best Experience - Open On PC/MAC", Color.white);
        yield return new WaitForSeconds(2.5f);
        Application.OpenURL(url);
        yield return null;
    }

    private async Task<Dictionary<string, List<ModelPayload>>> GetAllModelInfoAsync()
    {
        string myId = base.UserId;
        Dictionary<string, List<ModelPayload>> dict = new Dictionary<string, List<ModelPayload>>();
        DataSnapshot snapshot = await FirebaseDatabase.DefaultInstance.RootReference.GetValueAsync();

        foreach (DataSnapshot user in snapshot.Children)
        {
            if (user.HasChild("files/models"))
            {
                foreach (DataSnapshot model in user.Child("files/models").Children)
                {
                    if (model.HasChild("filePath"))
                    {
                        List<string> paths = new List<string>() { "filePath" };
                        if (model.HasChild("myFilePath"))
                            paths.Add("myFilePath");

                        foreach (string fp in paths)
                        {
                            string filePath = model.Child(fp).Value.ToString();
                            string[] pp = filePath.Split('/');

                            if (pp.Length > 1 &&
                                (pp[0] == myId || pp[1] == "public"))
                            {
                                string userKey = pp[0];
                                string fileName = model.Child(fp == "myFilePath" ? "myFileName" : "fileName").Value.ToString();
                                string fileDesc = model.Child(fp == "myFilePath" ? "myFileDesc" : "fileDesc").Value.ToString();
                                ModelPayload modelPayload = new ModelPayload(fileName, filePath, fileDesc);

                                if (dict.ContainsKey(userKey))
                                {
                                    if (dict[userKey].Exists(m => m.filePath == filePath))
                                        continue;

                                    dict[userKey].Add(modelPayload);
                                }
                                else
                                {
                                    dict.Add(userKey, new List<ModelPayload>() { modelPayload });
                                }
                            }
                        }
                    }
                }
            }
        }
        return dict;
    }

    public void OnModelDropdownValueChanged()
    {
        int index = modelDropdown.value;
        var options = modelDropdown.options;

        if (resUsernames.Contains(options[index].text)
            && (index + 1) < options.Count)
        {
            modelDropdown.value = index + 1;
            modelDropdown.RefreshShownValue();
        }

        SetMainButtonAction(true);
    }

    private Color initialButtonColor = Color.clear;

    private void SetMainButtonAction(bool isSelect)
    {
        UnityDispatcher.InvokeOnAppThread(() =>
        {
            saveButton.onClick.RemoveAllListeners();
            Text buttonText = saveButton.GetComponentInChildren<Text>();
            Lean.Gui.LeanBox buttonImage = saveButton.GetComponent<Lean.Gui.LeanBox>();
            if (initialButtonColor == Color.clear)
                initialButtonColor = buttonImage.color;

            if (isSelect)
            {
                saveButton.onClick.AddListener(OnSaveButtonPressed);
                buttonImage.color = initialButtonColor;
                buttonText.text = "SELECT";
            }
            else
            {
                saveButton.onClick.AddListener(OnCloseButtonPressed);
                buttonImage.color = new Color32(0, 120, 254, 200);
                buttonText.text = "DONE";
            }
        });
    }
}
