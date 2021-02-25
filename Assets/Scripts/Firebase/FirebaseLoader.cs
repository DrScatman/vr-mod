using Firebase.Database;
using Firebase.Storage;
using System.Collections.Generic;
using UnityEngine;
using System;
using UnityGLTF;
using Firebase.Extensions;
using Photon.Pun;

public class FirebaseLoader : FirebaseManager
{
    [Header("Firebase Loader")]
    public Transform networkObjTransfrom;
    public GameObject Error3dPrefab;
    public GameObject TempLoadingPrefab;
    public List<GameObject> loadingGFX = new List<GameObject>();

    private readonly Dictionary<string, List<GameObject>> spawnedObjects = new Dictionary<string, List<GameObject>>();
    private static readonly System.Random rnd = new System.Random();

    private Dictionary<string, List<GameObject>> tempObjects = new Dictionary<string, List<GameObject>>();


    #region TEST
    // private bool hasLoaded;
    // protected override void Update()
    // {
    //     base.Update();

    //     if (IsReady && !hasLoaded)
    //     {
    //         Debug.Log("Preloading");
    //         hasLoaded = true;

    //         FetchLocationIdsAsync().ContinueWith(task =>
    //         {
    //             if (task.IsFaulted || task.IsCanceled)
    //             {
    //                 Debug.LogError(task.Exception);
    //                 return;
    //             }

    //             UnityDispatcher.InvokeOnAppThread(() => PreLoadAllModelsForUser());
    //         });
    //     }
    // }
    #endregion

    public void LoadOrDuplicateModel(string filePath, Vector3 position, Quaternion rotation, Vector3 scale, bool setActive = true)
    {
        if (spawnedObjects.ContainsKey(filePath) && spawnedObjects[filePath] != null && spawnedObjects[filePath].Count > 0)
        {
            GameObject dup = Instantiate(spawnedObjects[filePath][0], position, rotation);
            dup.transform.localScale = scale;
            dup.gameObject.SetActive(setActive);

            AddToSpawnedObjects(filePath, dup);
            DestroyTempLoadingObj(filePath);

            dup.transform.SetParent(networkObjTransfrom, true);
            PreviousActions.AddPreviousAction(PreviousActions.ActionType.Spawn, dup);
        }
        else if (!spawnedObjects.ContainsKey(filePath))
        {
            LoadModelFromFirebase(filePath, position, rotation, scale, setActive);
        }
        else
        {
            Debug.LogWarning("Attempted to move model while loading");
        }
    }

    private void LoadModelFromFirebase(string filePath, Vector3 position, Quaternion rotation, Vector3 scale, bool setActive = true)
    {
        try
        {
            spawnedObjects.Add(filePath, null);

            UnityDispatcher.InvokeOnAppThread(() =>
            {
                if (tempObjects.ContainsKey(filePath))
                    tempObjects[filePath].Add(Instantiate(TempLoadingPrefab, position, rotation));
                else
                    tempObjects[filePath] = new List<GameObject>() { Instantiate(TempLoadingPrefab, position, rotation) };

                if (numModelsLoading < 2 && loadingGFX != null && loadingGFX.Count > 0)
                {
                    int r = rnd.Next(loadingGFX.Count);
                    Instantiate(loadingGFX[r], position, rotation);
                }
            });

            StartCoroutine(DeferLoad(filePath, position, rotation, scale, setActive));
        }
        catch (Exception e)
        {
            Debug.LogError(e);
            spawnedObjects.Remove(filePath);
            SpawnErrorText(position);
        }
    }

    public string GetSpawnedObjKey(GameObject obj)
    {
        foreach (var entry in spawnedObjects)
        {
            if (entry.Value != null && entry.Value.Count > 0)
            {
                if (entry.Value[0] == obj)
                    return entry.Key;
            }
        }

        return null;
    }

    private void AddToSpawnedObjects(string filePath, GameObject obj)
    {
        if (spawnedObjects.ContainsKey(filePath) && spawnedObjects[filePath] != null)
        {
            spawnedObjects[filePath].Add(obj);
        }
        else
        {
            spawnedObjects[filePath] = new List<GameObject>() { obj };
        }
    }

    private void DestroyTempLoadingObj(string filePath)
    {
        if (tempObjects.ContainsKey(filePath) && tempObjects[filePath] != null && tempObjects[filePath].Count > 0)
        {
            GameObject temp = tempObjects[filePath][0];
            tempObjects[filePath].Remove(temp);
            Destroy(temp);
        }
    }

    private System.Collections.IEnumerator DeferLoad(string filePath, Vector3 position, Quaternion rotation, Vector3 scale, bool setActive)
    {
        yield return new WaitUntil(() => numModelsLoading < MAX_LOADING_NUM);
        numModelsLoading++;

        FirebaseStorage.DefaultInstance.GetReference(filePath).GetDownloadUrlAsync()
        .ContinueWithOnMainThread(task =>
        {
            if (task.IsCanceled || task.IsFaulted)
            {
                Debug.LogError(task.Exception);
                SpawnErrorText(position);
                numModelsLoading--;
            }

            if (this != null)
                LoadUnityGLTF(filePath, position, rotation, scale, task.Result.AbsoluteUri, setActive);
        });

        yield return null;
    }

    private int numModelsLoading;
    private static readonly int MAX_LOADING_NUM = 2;

    private void LoadUnityGLTF(string filePath, Vector3 position, Quaternion rotation, Vector3 scale, string uri, bool setActive, int retries = 6)
    {
        var gltf = new GameObject().AddComponent<GLTFComponent>();
        gltf.loadOnStart = false;
        gltf.Multithreaded = true;
        gltf.PlayAnimationOnLoad = true;
        gltf.AppendStreamingAssets = true;
        //gltf.MaximumLod = 600;
        gltf.Collider = GLTFSceneImporter.ColliderType.Box;
        gltf.GLTFUri = uri;

        //int maxLoad = Math.Max(numModelsLoading, GetNumModelsToLoad());
        gltf.gameObject.AddComponent<AsyncCoroutineHelper>().BudgetPerFrameInSeconds = ((((retries > 0 ? retries : 0.1f) / 6) * 0.03f) / numModelsLoading);

        gltf.gameObject.name = filePath;
        gltf.transform.localScale = Vector3.zero;

        gltf.Load().ContinueWith((task) =>
        {
            if (task.IsFaulted || task.IsCanceled)
            {
                Debug.LogError(task.Exception);

                if (retries > 0 && (task.Exception.GetBaseException() is OutOfMemoryException || task.Exception.GetBaseException() is System.Net.Http.HttpRequestException))
                {
                    UnityDispatcher.InvokeOnAppThread(() =>
                    {
                        Destroy(gltf.gameObject);
                        LoadUnityGLTF(filePath, position, rotation, scale, uri, setActive, retries - 1);
                    });
                    return;
                }

                spawnedObjects.Remove(filePath);
                numModelsLoading--;
                SpawnErrorText(position);
                return;
            }

            UnityDispatcher.InvokeOnAppThread(() =>
            {
                GameObject obj = gltf.gameObject;
                AddToSpawnedObjects(filePath, obj);

                obj.transform.position = position;
                obj.transform.rotation = rotation;

                DestroyTempLoadingObj(filePath);

                obj.SetActive(setActive);
                obj.LeanScale(scale, 4f);

                obj.transform.SetParent(networkObjTransfrom, true);
                AddComponents(obj);

                PreviousActions.AddPreviousAction(PreviousActions.ActionType.Spawn, obj);
                numModelsLoading--;
            });
        });
    }

    private List<string> locationsToLoad = new List<string>();

    private int GetNumModelsToLoad()
    {
        foreach (var loc in FetchedLocationsDict)
        {
            if (loc.Value != null && loc.Value.HasModel && !locationsToLoad.Contains(loc.Key)
                && (!spawnedObjects.ContainsKey(loc.Key) || spawnedObjects[loc.Key] == null))
                locationsToLoad.Add(loc.Key);
        }

        return locationsToLoad.Count;
    }

    private void AddComponents(GameObject obj)
    {
        obj.layer = LayerMask.NameToLayer("Grab");
        XROffsetGrabInteractable interactable = obj.AddComponent<XROffsetGrabInteractable>();
        interactable.movementType = UnityEngine.XR.Interaction.Toolkit.XRGrabInteractable.MovementType.Kinematic;

        PhotonRigidbodyView networkRbView = obj.AddComponent<PhotonRigidbodyView>();
        networkRbView.m_TeleportEnabled = true;
        networkRbView.m_SynchronizeVelocity = true;
    }

    public void CleanupAllObjects()
    {
        List<string> loadingKeys = new List<string>();

        foreach (var entry in spawnedObjects)
        {
            if (entry.Value != null)
            {
                foreach (GameObject obj in entry.Value)
                {
                    Destroy(obj);
                }
            }
        }

        spawnedObjects.Clear();

        foreach (var entry in tempObjects)
        {
            if (entry.Value != null)
            {
                foreach (GameObject obj in entry.Value)
                {
                    Destroy(obj);
                }
            }
        }

        tempObjects.Clear();
    }

    public bool IsSpawnedObjectSelected()
    {
        try
        {
            if (spawnedObjects != null && spawnedObjects.Values != null && spawnedObjects.Values.Count > 0)
            {
                foreach (List<GameObject> objs in spawnedObjects.Values)
                {
                    foreach (GameObject o in objs)
                    {
                        XROffsetGrabInteractable interactable = o.GetComponent<XROffsetGrabInteractable>();

                        if (interactable != null && interactable.isSelected)
                            return true;
                    }
                }
            }
        }
        catch (Exception) { return false; }
        return false;
    }

    public void RemoveLoadingModels()
    {
        List<string> loadingKeys = new List<string>();

        foreach (var entry in spawnedObjects)
        {
            if (entry.Value == null && entry.Key != null)
                loadingKeys.Add(entry.Key);
        }

        loadingKeys.ForEach(k => spawnedObjects.Remove(k));
    }

    private GameObject SpawnErrorText(Vector3 position)
    {
        const string errorKey = "ERROR";

        if (spawnedObjects.ContainsKey(errorKey)
            && spawnedObjects[errorKey] != null && spawnedObjects[errorKey].Count > 0)
        {
            foreach (GameObject obj in spawnedObjects[errorKey])
            {
                Destroy(obj);
            }

            spawnedObjects[errorKey].Clear();
        }

        GameObject errorObj = Instantiate(Error3dPrefab, position, Quaternion.identity);
        spawnedObjects[errorKey] = new List<GameObject>() { errorObj };
        return errorObj;
    }
}
