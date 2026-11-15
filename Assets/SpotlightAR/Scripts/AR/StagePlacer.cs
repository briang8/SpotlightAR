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

        static readonly List<ARRaycastHit> Hits = new List<ARRaycastHit>();

        ARAnchor anchor;
        ARPlane placedOnPlane;
        float targetScale;
        bool editing;

        /// <summary>Raised when a surface becomes available or is lost.</summary>
        public event Action<SurfaceKind> SurfaceChanged;

        /// <summary>Raised whenever the stage is placed or moved.</summary>
        public event Action StagePlaced;

        public Stage Placed { get; private set; }
        public Stage StagePrefab => stagePrefab;
        public SurfaceKind Surface { get; private set; }
        public StageSize Size { get; private set; } = StageSize.Tabletop;
        public bool IsEditing => editing;

        void Awake()
        {
            if (arCamera == null) arCamera = Camera.main;
            targetScale = ScaleFor(Size);
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
            Placed.transform.localScale = Vector3.one * targetScale;
            SetPlaneDetection(false);
            CreateAnchor();
            return true;
        }

        /// <summary>Place (or move) the stage at a surface pose, facing the camera.</summary>
        public void PlaceStage(Pose pose)
        {
            if (Placed == null)
            {
                Placed = Instantiate(stagePrefab);
                Placed.transform.localScale = Vector3.one * targetScale;
            }

            Vector3 toCamera = arCamera.transform.position - pose.position;
            toCamera.y = 0f;
            Quaternion facing = toCamera.sqrMagnitude > 1e-4f ? Quaternion.LookRotation(toCamera.normalized, Vector3.up) : pose.rotation;
            Placed.transform.SetPositionAndRotation(pose.position, facing);
            reticle.Hide();
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
        public void Resize(float scale) => targetScale = Mathf.Clamp(scale, scaleLimits.x, scaleLimits.y);

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

            if (editing) UpdateSurfacePreview();
        }

        void UpdateSurfacePreview()
        {
            SurfaceKind kind = SurfaceKind.None;
            if (Raycast(ScreenCentre, out ARRaycastHit hit))
            {
                kind = arCamera.transform.position.y - hit.pose.position.y < tableHeightThreshold ? SurfaceKind.Table : SurfaceKind.Floor;
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
