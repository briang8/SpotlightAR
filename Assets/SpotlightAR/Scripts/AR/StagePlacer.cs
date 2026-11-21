using System;
using System.Collections.Generic;
using SpotlightAR.Stages;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace SpotlightAR.AR
{
    /// <summary>
    /// Finds floors and tables with AR plane detection, previews the spot with a reticle,
    /// and lets the player tap to place, pinch to resize and twist to turn the stage.
    /// It keeps the stage within the floor that was actually found (with a "Fit" shortcut),
    /// drops it in with a small bounce, and turns on depth occlusion where the phone supports
    /// it, so real things in front of the stage hide it.
    /// Once confirmed, the stage is pinned with an ARAnchor and plane detection is
    /// switched off to save battery and CPU.
    /// </summary>
    public sealed class StagePlacer : MonoBehaviour
    {
        [Header("AR")]
        [SerializeField] ARRaycastManager raycaster;
        [SerializeField] ARPlaneManager planeManager;
        [SerializeField] ARAnchorManager anchorManager;
        [SerializeField] Camera arCamera;

        [Header("Placement")]
        [SerializeField] TouchGestureInput gestures;
        [Tooltip("Place the stage so the player stands behind it like the performer, facing the crowd. Off = the stage front faces the player.")]
        [SerializeField] bool performerView = true;
        [SerializeField] PlacementReticle reticle;
        [SerializeField] Stage stagePrefab;

        [Header("Sizes (scale of a 1 m stage)")]
        [SerializeField] float tabletopScale = 0.35f;
        [SerializeField] float roomScale = 1.2f;
        [SerializeField] float lifeSizeScale = 3f;
        [SerializeField] Vector2 scaleLimits = new Vector2(0.15f, 4f);
        [SerializeField] float scaleSmoothing = 12f;
        [Tooltip("If the camera is less than this high above the surface (m), call it a table.")]
        [SerializeField] float tableHeightThreshold = 1f;

        [Header("Realism")]
        [Tooltip("Floor area a stage needs at scale 1, crowd included (metres).")]
        [SerializeField] float stageFootprint = 1.7f;
        [Tooltip("Hide the stage behind real objects using ARCore depth, on phones that support it.")]
        [SerializeField] bool depthOcclusion = true;
        [SerializeField] float dropHeight = 0.25f;
        [SerializeField] float dropSeconds = 0.55f;

        static readonly List<ARRaycastHit> Hits = new List<ARRaycastHit>();

        ARAnchor anchor;
        ARPlane placedOnPlane;
        float targetScale;
        bool editing;
        ARPlane surfacePlane;
        Vector3 restPosition;
        float dropTime = -1f;

        /// <summary>Raised when a surface becomes available or is lost.</summary>
        public event Action<SurfaceKind> SurfaceChanged;

        /// <summary>Raised whenever the stage is placed or moved.</summary>
        public event Action StagePlaced;

        /// <summary>Raised when the stage starts or stops fitting on the surface that was found.</summary>
        public event Action<bool> FitChanged;

        /// <summary>False when the stage is bigger than the floor or table found so far.</summary>
        public bool Fits { get; private set; } = true;

        public Stage Placed { get; private set; }
        public Stage StagePrefab => stagePrefab;
        public SurfaceKind Surface { get; private set; }
        public StageSize Size { get; private set; } = StageSize.Tabletop;
        public bool IsEditing => editing;

        void Awake()
        {
            if (arCamera == null) arCamera = Camera.main;
            targetScale = ScaleFor(Size);
            if (depthOcclusion) EnableOcclusion();
        }

        void OnEnable()
        {
            gestures.Tapped += OnTapped;
            gestures.Pinched += OnPinched;
            gestures.Twisted += OnTwisted;
        }

        void OnDisable()
        {
            gestures.Tapped -= OnTapped;
            gestures.Pinched -= OnPinched;
            gestures.Twisted -= OnTwisted;
        }

        /// <summary>Swap the stage theme. Replaces the placed stage, keeping its pose.</summary>
        public void SetStagePrefab(Stage prefab)
        {
            if (prefab == null || prefab == stagePrefab) return;
            stagePrefab = prefab;
            if (Placed == null) return;

            Transform old = Placed.transform;
            Stage replacement = Instantiate(stagePrefab, old.position, old.rotation, old.parent);
            replacement.transform.localScale = old.localScale;
            Destroy(Placed.gameObject);
            Placed = replacement;
        }

        /// <summary>Enter placement mode: show planes and the reticle, accept gestures.</summary>
        public void BeginPlacement()
        {
            editing = true;
            gestures.enabled = true;
            ReleaseAnchor();
            SetPlaneDetection(true);
        }

        /// <summary>Lock the stage in place. Places it at the reticle if it is not placed yet.</summary>
        public bool ConfirmPlacement()
        {
            if (Placed == null && !TryPlaceAt(ScreenCentre)) return false;

            editing = false;
            gestures.enabled = false;
            reticle.Hide();
            FinishDrop();
            Placed.transform.localScale = Vector3.one * targetScale;
            SetPlaneDetection(false);
            CreateAnchor();
            return true;
        }

        /// <summary>Place (or move) the stage at a surface pose, facing the camera.</summary>
        public void PlaceStage(Pose pose)
        {
            bool arriving = Placed == null;
            if (arriving)
            {
                Placed = Instantiate(stagePrefab);
                // Starts a little small and grows into place with the scale smoothing below.
                Placed.transform.localScale = Vector3.one * (targetScale * 0.7f);
            }

            Vector3 toCamera = arCamera.transform.position - pose.position;
            toCamera.y = 0f;
            // The crowd stands at the stage front. In performer view the front points away from
            // the player, so they look over the stage at the crowd's faces.
            Vector3 front = performerView ? -toCamera : toCamera;
            Quaternion facing = front.sqrMagnitude > 1e-4f ? Quaternion.LookRotation(front.normalized, Vector3.up) : pose.rotation;
            Placed.transform.SetPositionAndRotation(pose.position, facing);
            reticle.Hide();
            restPosition = pose.position;
            dropTime = arriving ? 0f : -1f;
            if (arriving) Placed.PlayEntrance();
            UpdateFit();
            StagePlaced?.Invoke();
        }

        /// <summary>Remove the stage completely so the player can start placing again.</summary>
        public void Clear()
        {
            ReleaseAnchor();
            if (Placed != null) Destroy(Placed.gameObject);
            Placed = null;
            placedOnPlane = null;
        }

        /// <summary>Resize to an exact scale (clamped to the allowed range).</summary>
        public void Resize(float scale)
        {
            targetScale = Mathf.Clamp(scale, scaleLimits.x, scaleLimits.y);
            UpdateFit();
        }

        /// <summary>Size the stage to the floor or table found so far (a little smaller, to leave a margin).</summary>
        public void FitToSurface()
        {
            ARPlane plane = placedOnPlane != null ? placedOnPlane : surfacePlane;
            if (plane == null) return;
            Resize(Mathf.Min(plane.size.x, plane.size.y) / stageFootprint * 0.9f);
        }

        public void SetSize(StageSize size)
        {
            Size = size;
            Resize(ScaleFor(size));
        }

        void Update()
        {
            if (Placed != null)
            {
                float current = Placed.transform.localScale.x;
                float next = Mathf.Lerp(current, targetScale, 1f - Mathf.Exp(-scaleSmoothing * Time.deltaTime));
                Placed.transform.localScale = Vector3.one * next;
            }

            UpdateDrop();
            if (editing) UpdateSurfacePreview();
        }

        void UpdateSurfacePreview()
        {
            SurfaceKind kind = SurfaceKind.None;
            if (Raycast(ScreenCentre, out ARRaycastHit hit))
            {
                kind = arCamera.transform.position.y - hit.pose.position.y < tableHeightThreshold ? SurfaceKind.Table : SurfaceKind.Floor;
                surfacePlane = hit.trackable as ARPlane;
                if (Placed == null) reticle.Show(hit.pose, targetScale);
            }
            else if (Placed == null)
            {
                reticle.Hide();
            }

            if (kind != Surface)
            {
                Surface = kind;
                SurfaceChanged?.Invoke(kind);
            }
            UpdateFit();
        }

        void OnTapped(Vector2 screenPosition)
        {
            if (editing) TryPlaceAt(screenPosition);
        }

        void OnPinched(float ratio)
        {
            if (editing && Placed != null) Resize(targetScale * ratio);
        }

        void OnTwisted(float degrees)
        {
            if (editing && Placed != null) Placed.transform.Rotate(0f, degrees, 0f, Space.World);
        }

        bool TryPlaceAt(Vector2 screenPosition)
        {
            if (!Raycast(screenPosition, out ARRaycastHit hit)) return false;
            placedOnPlane = hit.trackable as ARPlane;
            PlaceStage(hit.pose);
            return true;
        }

        bool Raycast(Vector2 screenPosition, out ARRaycastHit hit)
        {
            hit = default;
            if (!raycaster.Raycast(screenPosition, Hits, TrackableType.PlaneWithinPolygon)) return false;
            hit = Hits[0]; // hits are sorted by distance
            return true;
        }

        /// <summary>The stage drops onto the surface and settles, instead of popping into existence.</summary>
        void UpdateDrop()
        {
            if (dropTime < 0f || Placed == null) return;
            dropTime += Time.deltaTime / dropSeconds;
            if (dropTime >= 1f)
            {
                FinishDrop();
                return;
            }
            float fall = Mathf.Pow(1f - dropTime, 3f);
            Placed.transform.position = restPosition + Vector3.up * (dropHeight * targetScale * fall);
        }

        void FinishDrop()
        {
            if (dropTime < 0f || Placed == null) return;
            dropTime = -1f;
            Placed.transform.position = restPosition;
        }

        /// <summary>
        /// Compares the stage's footprint with the plane under it. Planes grow as the phone sees
        /// more of the floor, so there's some tolerance, and no plane yet means no warning.
        /// </summary>
        void UpdateFit()
        {
            ARPlane plane = placedOnPlane != null ? placedOnPlane : surfacePlane;
            bool fits = plane == null || targetScale * stageFootprint <= Mathf.Min(plane.size.x, plane.size.y) * 1.25f;
            reticle.SetTooBig(!fits);
            if (fits == Fits) return;
            Fits = fits;
            FitChanged?.Invoke(fits);
        }

        /// <summary>
        /// Turns on ARCore depth occlusion: real objects closer than the stage cover it. On phones
        /// without depth support the request is simply ignored.
        /// </summary>
        void EnableOcclusion()
        {
            if (arCamera == null) return;
            var occlusion = arCamera.GetComponent<AROcclusionManager>();
            if (occlusion == null) occlusion = arCamera.gameObject.AddComponent<AROcclusionManager>();
            occlusion.requestedEnvironmentDepthMode = EnvironmentDepthMode.Fastest;
            occlusion.requestedOcclusionPreferenceMode = OcclusionPreferenceMode.PreferEnvironmentOcclusion;
            occlusion.environmentDepthTemporalSmoothingRequested = true;
        }

        void CreateAnchor()
        {
            if (anchorManager == null || placedOnPlane == null) return;

            Transform stage = Placed.transform;
            anchor = anchorManager.AttachAnchor(placedOnPlane, new Pose(stage.position, stage.rotation));
            if (anchor != null) stage.SetParent(anchor.transform, true);
        }

        void ReleaseAnchor()
        {
            if (anchor == null) return;
            if (Placed != null) Placed.transform.SetParent(null, true);
            Destroy(anchor.gameObject);
            anchor = null;
        }

        void SetPlaneDetection(bool active)
        {
            planeManager.enabled = active;
            foreach (ARPlane plane in planeManager.trackables) plane.gameObject.SetActive(active);
        }

        float ScaleFor(StageSize size) => size switch
        {
            StageSize.Room => roomScale,
            StageSize.LifeSize => lifeSizeScale,
            _ => tabletopScale
        };

        static Vector2 ScreenCentre => new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
    }
}
