using System;

// Minimal shapes of the YouTube Data API v3 responses, for JsonUtility.
// Only the fields the app reads are declared.
namespace SpotlightAR.Songs
{
    [Serializable]
    sealed class PlaylistItemsResponse
    {
        public string nextPageToken;
        public PlaylistItem[] items;
    }

    [Serializable]
    sealed class PlaylistItem
    {
        public PlaylistSnippet snippet;
    }

    [Serializable]
    sealed class PlaylistSnippet
    {
        public string title;
        public string videoOwnerChannelTitle;
        public ResourceId resourceId;
    }

    [Serializable]
    sealed class ResourceId
    {
        public string videoId;
    }

    [Serializable]
    sealed class VideosResponse
    {
        public VideoItem[] items;
    }

    [Serializable]
    sealed class VideoItem
    {
        public string id;
        public VideoContentDetails contentDetails;
        public VideoStatus status;
    }

    [Serializable]
    sealed class VideoContentDetails
    {
        public string duration; // ISO 8601, e.g. PT3M42S
    }

    [Serializable]
    sealed class VideoStatus
    {
        public bool embeddable;
        public string privacyStatus;
    }

    [Serializable]
    sealed class ApiErrorResponse
    {
        public ApiError error;
    }

    [Serializable]
    sealed class ApiError
    {
        public int code;
        public string message;
    }
}
