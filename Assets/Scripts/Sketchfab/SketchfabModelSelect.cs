using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using DTOs;
using System.Text.RegularExpressions;

public class SketchfabModelSelect : SketchfabLoader
{
    [Header("Sketchfab ModelSelect")]
    public Transform scrollViewContent;
    public InputField searchInputField;
    public Dropdown categoryDropdown;
    public Dropdown sortDropdown;
    public Toggle staffpickedToggle;
    public Toggle animatedToogle;

    public string SelectedModelUid = null;

    private readonly int ITEMS_PER_ROW = 6;
    private ModelList[] models;
    private CategoriesRelated[] categories;
    private string queryString = "?downloadable=true&staffpicked=true&animated=false&sort_by=-viewCount";

    protected override void Start()
    {
        base.Start();
        FetchModelListAsync();
        FetchCategoryListAsync();

        sortDropdown.SetValueWithoutNotify(2);
        sortDropdown.RefreshShownValue();
    }

    private void FetchModelListAsync()
    {
        FetchModelsAsync(queryString).Then(res =>
        {
            models = res.results;

            for (int i = 0; i < scrollViewContent.childCount; i++)
            {
                for (int j = i * ITEMS_PER_ROW; j < i * ITEMS_PER_ROW + ITEMS_PER_ROW && j < models.Length; j++)
                {
                    Transform item = scrollViewContent.GetChild(i).GetChild(j % ITEMS_PER_ROW);

                    StartCoroutine(SetImageDownload(item.GetComponentInChildren<RawImage>(), models[j].thumbnails.images[2].url));
                    item.GetChild(1).GetComponent<Text>().text = models[j].name;
                    item.GetChild(2).GetComponent<Text>().text = "by " + models[j].user.username;
                    item.GetChild(3).GetComponent<Text>().text = models[j].license.label;
                }
            }
        }).Catch(err =>
        {
            Debug.LogError(queryString + " : " + err);
            SplashText(err.GetBaseException().Message, Color.red);
        });
    }

    public void OnModelSelect(int selectionIndex)
    {
        SelectedModelUid = models[selectionIndex].uid;
        SplashText("Model Selected!", Color.cyan);
        //gameObject.SetActive(false);
    }

    public void OnSearchButtonPress()
    {
        if (string.IsNullOrEmpty(searchInputField.text)) return;
        string searchQueryString = $"?type=models&downloadable=true&q={searchInputField.text}";

        SearchModelsAsync(searchQueryString).Then(res =>
        {
            ModelSearchList[] models = res.results;

            for (int i = 0; i < scrollViewContent.childCount; i++)
            {
                for (int j = i * ITEMS_PER_ROW; j < i * ITEMS_PER_ROW + ITEMS_PER_ROW; j++)
                {
                    Transform item = scrollViewContent.GetChild(i).GetChild(j % ITEMS_PER_ROW);

                    StartCoroutine(SetImageDownload(item.GetComponentInChildren<RawImage>(), models[j].thumbnails.images[3].url));
                    item.GetChild(1).GetComponent<Text>().text = models[j].name;
                    item.GetChild(2).GetComponent<Text>().text = "by " + models[j].user.username;
                }
            }
        }).Catch(err =>
        {
            Debug.LogError(queryString + " : " + err);
            SplashText(err.GetBaseException().Message, Color.red);
        });
    }

    public void OnStaffPickedToggle()
    {
        if (staffpickedToggle.isOn)
        {
            if (queryString.Contains("staffpicked="))
                queryString = queryString.Replace("staffpicked=false", "staffpicked=true");
            else
                queryString += "&staffpicked=true";
        }
        else
        {
            if (queryString.Contains("staffpicked="))
                queryString = queryString.Replace("staffpicked=true", "staffpicked=false");
            else
                queryString += "&staffpicked=false";
        }

        FetchModelListAsync();
    }

    public void OnAnimatedToggle()
    {
        if (animatedToogle.isOn)
        {
            if (queryString.Contains("animated="))
                queryString = queryString.Replace("animated=false", "animated=true");
            else
                queryString += "&animated=true";
        }
        else
        {
            if (queryString.Contains("animated="))
                queryString = queryString.Replace("animated=true", "animated=false");
            else
                queryString += "&animated=false";
        }

        FetchModelListAsync();
    }

    public void OnSortDropdownChange()
    {
        string selection = sortDropdown.options[sortDropdown.value].text;

        Match match = Regex.Match(queryString, @"&sort_by=[-|a-zA-Z]+", RegexOptions.IgnoreCase);

        switch (selection)
        {
            case "Relevance":
                queryString = match.Success ? queryString.Replace(match.Value, "")
                                            : queryString;
                break;
            case "Likes":
                queryString = match.Success ? queryString.Replace(match.Value, "&sort_by=-likedAt")
                                            : queryString + "&sort_by=-likedAt";
                break;
            case "Views":
                queryString = match.Success ? queryString.Replace(match.Value, "&sort_by=-viewCount")
                                            : queryString + "&sort_by=-viewCount";
                break;
            case "Recent":
                queryString = match.Success ? queryString.Replace(match.Value, "&sort_by=-createdAt")
                                            : queryString + "&sort_by=-createdAt";
                break;
        }

        FetchModelListAsync();
    }

    private void FetchCategoryListAsync()
    {
        FetchCategoriesAsync().Then(res =>
        {
            categories = res.results;
            categoryDropdown.options.Clear();
            categoryDropdown.options.Add(new Dropdown.OptionData("All"));

            foreach (CategoriesRelated c in categories)
            {
                categoryDropdown.options.Add(new Dropdown.OptionData(c.name));
            }

            categoryDropdown.RefreshShownValue();
            categoryDropdown.Hide();
        }).Catch(err =>
        {
            Debug.LogError(queryString + " : " + err);
            SplashText(err.GetBaseException().Message, Color.red);
        });
    }

    public void OnCategoryDropdownChange()
    {
        Match match = Regex.Match(queryString, @"&categories=[a-zA-Z|0-9]+", RegexOptions.IgnoreCase);

        if (categoryDropdown.value <= 0)
        {
            if (match.Success)
                queryString.Replace(match.Value, "");
        }
        else
        {
            string slug = categories[categoryDropdown.value - 1].slug;

            if (match.Success)
                queryString = queryString.Replace(match.Value, "&categories=" + slug);
            else
                queryString += "&categories=" + slug;
        }

        FetchModelListAsync();
    }

    IEnumerator SetImageDownload(RawImage rawImage, string url)
    {
        UnityWebRequest request = UnityWebRequestTexture.GetTexture(url);
        yield return request.SendWebRequest();

        if (request.isNetworkError || request.isHttpError)
        {
            Debug.LogError(request.error);
            yield return null;
        }

        rawImage.color = Color.white;
        rawImage.texture = ((DownloadHandlerTexture)request.downloadHandler).texture;
    }

    public void OpenURL(string url)
    {
        Application.OpenURL(url);
    }
}
