using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UIElements;

namespace SpotlightAR.UI
{
    /// <summary>
    /// Downloads song thumbnails once and reuses them. Textures are kept non-readable
    /// (GPU only) to halve their memory, and the cache is capped so long browsing
    /// sessions do not keep growing memory on the phone.
    /// </summary>
    public static class CoverImages
    {
        const int MaxCached = 80;

        static readonly Dictionary<string, Texture2D> Cache = new Dictionary<string, Texture2D>();
        static readonly LinkedList<string> Order = new LinkedList<string>();

        /// <summary>Sets the element's background to the image at url once it has loaded.</summary>
        public static async void Apply(VisualElement target, string url)
        {
            if (target == null || string.IsNullOrEmpty(url)) return;
            target.userData = url;

            Texture2D texture = await Get(url);
            if (texture != null && Equals(target.userData, url))
            {
                target.style.backgroundImage = new StyleBackground(texture);
                target.AddToClassList("cover--loaded");
            }
        }

        static async Awaitable<Texture2D> Get(string url)
        {
            if (Cache.TryGetValue(url, out Texture2D cached)) return cached;

            using UnityWebRequest request = UnityWebRequestTexture.GetTexture(url, true);
            request.timeout = 15;
            await request.SendWebRequest();
            if (request.result != UnityWebRequest.Result.Success) return null;
            if (Cache.TryGetValue(url, out cached)) return cached; // another request finished first

            Texture2D texture = DownloadHandlerTexture.GetContent(request);
            texture.wrapMode = TextureWrapMode.Clamp;
            Cache[url] = texture;
            Order.AddLast(url);

            if (Order.Count > MaxCached)
            {
                string oldest = Order.First.Value;
                Order.RemoveFirst();
                if (Cache.Remove(oldest, out Texture2D evicted)) Object.Destroy(evicted);
            }
            return texture;
        }
    }
}
