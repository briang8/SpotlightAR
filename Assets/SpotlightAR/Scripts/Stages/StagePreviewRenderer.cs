using UnityEngine;

namespace SpotlightAR.Stages
{
    /// <summary>
    /// Takes a picture of a stage for the Stages tab: the real prefab is built far below the
    /// AR scene, lit up for a moment, photographed by a temporary camera into a texture and
    /// removed again. So the previews always match the stages exactly, with no screenshots
    /// to keep up to date.
    /// </summary>
    public static class StagePreviewRenderer
    {
        static readonly Vector3 StudioPosition = new Vector3(0f, -500f, 0f);
        const int WarmUpFrames = 8;

        public static async Awaitable<RenderTexture> Render(Stage prefab, int width, int height, Color background, string signText = null)
        {
            if (prefab == null) return null;

            var studio = new GameObject("Stage Preview");
            studio.transform.position = StudioPosition;
            Stage stage = Object.Instantiate(prefab, studio.transform);
            stage.transform.localPosition = Vector3.zero;
            stage.transform.localRotation = Quaternion.identity;
            stage.transform.localScale = Vector3.one;
            stage.SetStageName(signText);

            var texture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4, name = prefab.name + " Preview" };
            var cameraObject = new GameObject("Preview Camera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = background;
            camera.fieldOfView = 34f;
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 12f;
            camera.targetTexture = texture;
            // From the crowd, a little to the side and above, like a photo from the audience.
            camera.transform.position = StudioPosition + new Vector3(0.75f, 0.95f, 2.7f);
            camera.transform.LookAt(StudioPosition + new Vector3(0f, 0.3f, 0f));

            try
            {
                // Let the stage build its crowd and settle, with the show lit as if someone is singing.
                for (int i = 0; i < WarmUpFrames; i++)
                {
                    stage.ReactTo(0.65f);
                    await Awaitable.NextFrameAsync();
                }
            }
            finally
            {
                camera.targetTexture = null;
                Object.Destroy(cameraObject);
                Object.Destroy(studio);
            }
            return texture;
        }
    }
}
