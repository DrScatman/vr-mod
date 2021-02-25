using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Proyecto26;
using UnityGLTF;
using Photon.Pun;
using Ionic.Zip;
using System.IO;
using UnityEngine.Networking;
using DTOs;
using UnityEngine.XR.Interaction.Toolkit;

public class SketchfabLoader : SketchfabManager
{
    [Header("Sketchfab Loader")]
    public GameObject Error3dPrefab;
    public GameObject TempLoadingPrefab;

    private readonly Dictionary<string, List<GameObject>> spawnedObjects = new Dictionary<string, List<GameObject>>();
    private Dictionary<string, List<GameObject>> tempObjects = new Dictionary<string, List<GameObject>>();

    private static readonly string AUTH_APPROVAL_PARAM = "approval_prompt=auto";
    private int numModelsLoading;
    private static readonly int MAX_LOADING_NUM = 2;
    private string _downloadDirectory = "";
    private string _unzipDirectory = "";
    private List<string> filesToDelete = new List<string>();

    protected override void Start()
    {
        base.Start();
        _unzipDirectory = Application.temporaryCachePath + "/downloads";
        _unzipDirectory = Application.temporaryCachePath + "/unzip";
        StartCoroutine(FileSystemCleanupCoroutine());
    }

    public void LoadOrDuplicateModel(string uid, Vector3 position, Quaternion rotation, Vector3 scale)
    {
        if (spawnedObjects.ContainsKey(uid) && spawnedObjects[uid] != null && spawnedObjects[uid].Count > 0)
        {
            GameObject dup = Instantiate(spawnedObjects[uid][0], position, rotation);
            dup.transform.localScale = scale;

            AddToSpawnedObjects(uid, dup);
            DestroyTempLoadingObj(uid);
            dup.SetActive(true);

            PreviousActions.AddPreviousAction(PreviousActions.ActionType.Spawn, dup);
        }
        else if (!spawnedObjects.ContainsKey(uid))
        {
            StartModelImport(uid, position, rotation, scale);
        }
        else
        {
            Debug.Log("Attempted to move model while loading");
        }
    }

    private void StartModelImport(string uid, Vector3 position, Quaternion rotation, Vector3 scale)
    {
        if (string.IsNullOrEmpty(AccessToken))
        {
            AuthorizeApp();
            return;
        }

        spawnedObjects.Add(uid, null);

        if (tempObjects.ContainsKey(uid))
            tempObjects[uid].Add(Instantiate(TempLoadingPrefab, position, rotation));
        else
            tempObjects[uid] = new List<GameObject>() { Instantiate(TempLoadingPrefab, position, rotation) };

        RestClient.DefaultRequestHeaders["Authorization"] = $"Bearer {AccessToken}";
        RestClient.Get<ModelDownload>($"{BASE_URL}/models/{uid}/download?{AUTH_APPROVAL_PARAM}").Then(res =>
        {
            StartCoroutine(DownloadArchiveDefered(uid, res.gltf.url, position, rotation, scale));

        }).Catch(err =>
        {
            Debug.LogError(err);
            Debug.Log($"uid - {uid}");
            spawnedObjects.Remove(uid);
        });
    }

    private IEnumerator DownloadArchiveDefered(string uid, string url, Vector3 position, Quaternion rotation, Vector3 scale)
    {
        yield return new WaitUntil(() => numModelsLoading < MAX_LOADING_NUM);
        numModelsLoading++;

        UnityWebRequest www = UnityWebRequest.Get(url);
        {
            yield return www.SendWebRequest();
            if (www.isNetworkError || www.isHttpError)
            {
                Debug.LogError(www.error);
                Debug.Log($"uid - {uid} | url - {url}");
            }
            else
            {
                string savePath = string.Format("{0}/{1}{2}", _downloadDirectory, uid, url.Contains(".zip") ? ".zip" : ".rar");
                File.WriteAllBytes(savePath, www.downloadHandler.data);

                LoadUnityGLTF(uid, UnzipArchive(uid, savePath), position, rotation, scale);
            }
        }
    }

    private void LoadUnityGLTF(string uid, string uri, Vector3 position, Quaternion rotation, Vector3 scale, int retries = 8)
    {
        var gltf = new GameObject().AddComponent<GLTFComponent>();
        gltf.loadOnStart = false;
        gltf.Multithreaded = true;
        gltf.PlayAnimationOnLoad = true;
        gltf.AppendStreamingAssets = true;
        gltf.MaximumLod = 600;
        gltf.Collider = GLTFSceneImporter.ColliderType.Box;
        gltf.UseStream = true;
        gltf.GLTFUri = uri;

        gltf.gameObject.AddComponent<AsyncCoroutineHelper>().BudgetPerFrameInSeconds = (retries > 0 ? retries : 0.1f) / 6 * 0.03f / numModelsLoading;

        gltf.gameObject.name = uid;
        gltf.transform.localScale = Vector3.zero;
        gltf.gameObject.layer = LayerMask.NameToLayer("Grab");

        gltf.Load().ContinueWith((task) =>
        {
            if (task.IsFaulted || task.IsCanceled)
            {
                Debug.LogError(task.Exception);

                if (retries > 0 && (task.Exception.GetBaseException() is System.OutOfMemoryException || task.Exception.GetBaseException() is System.Net.Http.HttpRequestException))
                {
                    UnityDispatcher.InvokeOnAppThread(() =>
                    {
                        Destroy(gltf.gameObject);
                        LoadUnityGLTF(uid, uri, position, rotation, scale, retries - 1);
                    });
                    return;
                }

                spawnedObjects.Remove(uid);
                numModelsLoading--;
                SpawnErrorText(position);
                if (File.Exists(uri))
                    filesToDelete.Add(uri);
                return;
            }

            UnityDispatcher.InvokeOnAppThread(() =>
            {
                GameObject obj = gltf.gameObject;
                AddToSpawnedObjects(uid, obj);
                obj.transform.localScale = Vector3.zero;

                obj.transform.position = position;
                obj.transform.rotation = rotation;

                DestroyTempLoadingObj(uid);

                obj.SetActive(false);
                ScaleHelper(obj, new Vector3(15, 15, 15));
                obj.SetActive(true);
                AddComponents(obj);

                PreviousActions.AddPreviousAction(PreviousActions.ActionType.Spawn, obj);

                if (File.Exists(uri))
                    filesToDelete.Add(uri);

                numModelsLoading--;
            });
        });
    }

    private void ScaleHelper(GameObject obj, Vector3 maxScale)
    {
        var renderers = obj.GetComponentsInChildren<Renderer>();
        obj.transform.localScale = Vector3.one;

        var bounds = renderers[0].bounds;
        foreach (var r in renderers) bounds.Encapsulate(r.bounds);

        Vector3 szB = bounds.size;

        if (szB.sqrMagnitude > maxScale.sqrMagnitude)
        {
            var xFraction = maxScale.x / szB.x;
            var yFraction = maxScale.y / szB.y;
            var zFraction = maxScale.z / szB.z;

            var minFraction = Mathf.Min(xFraction, yFraction, zFraction);

            Vector3 newScale = new Vector3(minFraction, minFraction, minFraction);
            obj.transform.localScale = Vector3.zero;
            obj.LeanScale(newScale, 1.5f);
        }
        else
        {
            obj.transform.localScale = Vector3.zero;
            obj.LeanScale(Vector3.one, 1.5f);
        }
    }

    private void AddComponents(GameObject obj)
    {
        obj.layer = LayerMask.NameToLayer("Grab");
        obj.AddComponent<XRCustomGrabInteractable>().movementType = XRGrabInteractable.MovementType.Kinematic;
        obj.GetComponent<Rigidbody>().isKinematic = false;

        obj.AddComponent<PhotonView>().OwnershipTransfer = OwnershipOption.Takeover;
        PhotonRigidbodyView networkRbView = obj.AddComponent<PhotonRigidbodyView>();
        networkRbView.m_TeleportEnabled = true;
        networkRbView.m_SynchronizeVelocity = true;
        networkRbView.m_SynchronizeAngularVelocity = true;
    }

    private void AddToSpawnedObjects(string uid, GameObject obj)
    {
        if (spawnedObjects.ContainsKey(uid) && spawnedObjects[uid] != null)
        {
            spawnedObjects[uid].Add(obj);
        }
        else
        {
            spawnedObjects[uid] = new List<GameObject>() { obj };
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

    private void DestroyTempLoadingObj(string uid)
    {
        if (tempObjects.ContainsKey(uid) && tempObjects[uid] != null && tempObjects[uid].Count > 0)
        {
            GameObject temp = tempObjects[uid][0];
            tempObjects[uid].Remove(temp);
            Destroy(temp);
        }
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

    private string UnzipArchive(string uid, string filepath)
    {
        if (!Directory.Exists(_unzipDirectory))
            Directory.CreateDirectory(_unzipDirectory);
        else
            DeleteOtherDirectories(_unzipDirectory, uid);

        string extractDir = _unzipDirectory + "/" + uid;
        Directory.CreateDirectory(extractDir);

        ZipFile zipFile = ZipFile.Read(filepath);

        foreach (ZipEntry e in zipFile)
        {
            e.Extract(extractDir, ExtractExistingFileAction.OverwriteSilently);
        }

        return FindGltfFile(extractDir);
    }

    private void DeleteOtherDirectories(string parentDirectory, string uid)
    {
        DirectoryInfo info = new DirectoryInfo(parentDirectory);

        foreach (DirectoryInfo d in info.GetDirectories())
        {
            if (!d.FullName.Contains(uid))
            {
                filesToDelete.Add(d.FullName);
            }
        }
    }

    private string FindGltfFile(string directory)
    {
        string gltfFile = "";
        DirectoryInfo info = new DirectoryInfo(directory);

        foreach (FileInfo fileInfo in info.GetFiles())
        {
            if (IsSupportedFile(fileInfo.FullName))
            {
                gltfFile = fileInfo.FullName;
            }
        }

        return gltfFile;
    }

    private bool IsSupportedFile(string filepath)
    {
        string ext = Path.GetExtension(filepath);
        return ext == ".gltf" || ext == ".glb";
    }

    private IEnumerator FileSystemCleanupCoroutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(10f);

            if (filesToDelete.Count > 0)
            {
                if (File.Exists(filesToDelete[0]))
                {
                    try
                    {
                        File.Delete(filesToDelete[0]);
                        filesToDelete.Remove(filesToDelete[0]);
                    }
                    catch (System.Exception) { }
                }
                else if (Directory.Exists(filesToDelete[0]))
                {
                    try
                    {
                        Directory.Delete(filesToDelete[0]);
                        filesToDelete.Remove(filesToDelete[0]);
                    }
                    catch (System.Exception) { }
                }
                else
                {
                    filesToDelete.Remove(filesToDelete[0]);
                }
            }

            yield return null;
        }
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();

        try
        {
            if (Directory.Exists(_downloadDirectory))
                Directory.Delete(_downloadDirectory, true);
            if (Directory.Exists(_unzipDirectory))
                Directory.Delete(_unzipDirectory, true);
        }
        catch (System.Exception) { }
    }
}
