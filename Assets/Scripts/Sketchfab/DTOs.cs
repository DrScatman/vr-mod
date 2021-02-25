namespace DTOs
{
    [System.Serializable]
    public class ModelResponse
    {
        public string next;
        public string previous;
        public ModelList[] results;
        public Cursors cursors;

        public override string ToString()
        {
            return UnityEngine.JsonUtility.ToJson(this, true);
        }
    }

    [System.Serializable]
    public class Cursors
    {
        string previous;
        string next;

        public override string ToString()
        {
            return UnityEngine.JsonUtility.ToJson(this, true);
        }
    }

    [System.Serializable]
    public class ModelList
    {
        public string uid;
        public string price;
        public TagRelated[] tags;
        public string viewerUrl;
        public bool isProtected;
        public CategoriesRelated[] categories;
        public string publishedAt;
        public int likeCount;
        public int commentCount;
        public int viewCount;
        public int vertexCount;
        public UserRelated user;
        public bool isDownloadable;
        public string description;
        public int animationCount;
        public string name;
        public int soundCount;
        public bool isAgeRestricted;
        public string uri;
        public int faceCount;
        public string staffpickedAt;
        public string createdAt;
        public ThumbnailsRelated thumbnails;
        public LicenseRelated license;
        public string embedUrl;

        public override string ToString()
        {
            return UnityEngine.JsonUtility.ToJson(this, true);
        }
    }
    [System.Serializable]
    public class TagRelated
    {
        public string slug;
        public string uri;
        public string name;

        public override string ToString()
        {
            return UnityEngine.JsonUtility.ToJson(this, true);
        }
    }

    [System.Serializable]
    public class CategoriesRelated
    {
        public string uri;
        public string uid;
        public string name;
        public string slug;

        public override string ToString()
        {
            return UnityEngine.JsonUtility.ToJson(this, true);
        }
    }

    [System.Serializable]
    public class UserRelated
    {
        public string username;
        public string profileUrl;
        public string account;
        public string displayName;
        public string uid;
        public string uri;
        public AvatarRelated avatar;

        public override string ToString()
        {
            return UnityEngine.JsonUtility.ToJson(this, true);
        }
    }

    [System.Serializable]
    public class ThumbnailsRelated
    {
        public inline_model_2[] images;

        public override string ToString()
        {
            return UnityEngine.JsonUtility.ToJson(this, true);
        }
    }

    [System.Serializable]
    public class LicenseRelated
    {
        public string uid;
        public string label;

        public override string ToString()
        {
            return UnityEngine.JsonUtility.ToJson(this, true);
        }
    }

    [System.Serializable]
    public class AvatarRelated
    {
        public inline_model[] images;
        // public string uid;
        public string uri;

        public override string ToString()
        {
            return UnityEngine.JsonUtility.ToJson(this, true);
        }
    }
    [System.Serializable]
    public class inline_model_2
    {
        public string url;
        public int width;
        public int size;
        public string uid;
        public int height;

        public override string ToString()
        {
            return UnityEngine.JsonUtility.ToJson(this, true);
        }
    }
    [System.Serializable]
    public class inline_model
    {
        public string url;
        public int width;
        public int height;
        public int size;

        public override string ToString()
        {
            return UnityEngine.JsonUtility.ToJson(this, true);
        }
    }

    [System.Serializable]
    public class ModelDownload
    {
        public inline_model_1 gltf;

        public override string ToString()
        {
            return UnityEngine.JsonUtility.ToJson(this, true);
        }
    }

    [System.Serializable]
    public class inline_model_1
    {
        // temporary URL where the archive can be downloaded
        public string url;
        // when the temporary URL will expire(in seconds)
        public int expires;

        public override string ToString()
        {
            return UnityEngine.JsonUtility.ToJson(this, true);
        }
    }

    [System.Serializable]
    public class CategoriesResponse
    {
        public CategoriesRelated[] results;

        public override string ToString()
        {
            return UnityEngine.JsonUtility.ToJson(this, true);
        }
    }

    [System.Serializable]
    public class ModelSearchResponse
    {
        public ModelSearchList[] results;
        public string next;
        public string previous;
        public Cursors cursors;
    }
    [System.Serializable]
    public class ModelSearchList
    {
        public string uid;
        public int animationCount;
        public string viewerUrl;
        public string publishedAt;
        public int likeCount;
        public int commentCount;
        public UserRelated user;
        public bool isDownloadable;
        public string name;
        public int viewCount;
        public ThumbnailsRelated thumbnails;
        public bool isPublished;
        public string staffpickedAt;
        public inline_model_0 archives;
        public int downloadCount;
        public string embedUrl;

        public override string ToString()
        {
            return UnityEngine.JsonUtility.ToJson(this, true);
        }
    }
    [System.Serializable]
    public class inline_model_0
    {
        public Inline_Model_1 gltf;
    }
    [System.Serializable]
    public class Inline_Model_1
    {
        public int size;
    }
}
