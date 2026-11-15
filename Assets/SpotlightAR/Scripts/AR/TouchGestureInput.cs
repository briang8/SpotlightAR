using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;

namespace SpotlightAR.AR
{
    /// <summary>
    /// Turns raw touches into the three gestures the stage needs: tap, pinch and twist.
    /// In the Editor the mouse stands in (click = tap, scroll = pinch, Q/E = twist).
    /// Touches that start on the UI are ignored so buttons never place the stage.
    /// </summary>
    public sealed class TouchGestureInput : MonoBehaviour
    {
        [Tooltip("A touch longer than this (seconds) is not a tap.")]
        [SerializeField] float tapMaxDuration = 0.35f;
        [Tooltip("A touch that moves further than this (fraction of screen height) is not a tap.")]
        [SerializeField] float tapMaxTravel = 0.03f;
        [SerializeField] float editorTwistSpeed = 90f;

        /// <summary>Screen position of a tap.</summary>
        public event Action<Vector2> Tapped;

        /// <summary>Scale ratio since last frame (1 = no change).</summary>
        public event Action<float> Pinched;

        /// <summary>Rotation since last frame in degrees, clockwise positive.</summary>
        public event Action<float> Twisted;

        /// <summary>Set by the UI so gestures that start on a button are ignored.</summary>
        public Func<Vector2, bool> IsOverUI { get; set; }

        Vector2 pressPosition;
        float pressTime;
        bool tapCandidate;

        void OnEnable() => EnhancedTouchSupport.Enable();

        void OnDisable()
        {
            EnhancedTouchSupport.Disable();
            tapCandidate = false;
        }

        void Update()
        {
            if (Touchscreen.current != null) ReadTouches();
            else ReadMouseAndKeyboard();
        }

        void ReadTouches()
        {
            var touches = Touch.activeTouches;
            if (touches.Count == 1) TrackTap(touches[0]);
            else if (touches.Count >= 2)
            {
                tapCandidate = false; // a second finger cancels the tap
                ReadTwoFingerGesture(touches[0], touches[1]);
            }
        }

        void TrackTap(Touch touch)
        {
            switch (touch.phase)
            {
                case UnityEngine.InputSystem.TouchPhase.Began:
                    BeginPress(touch.screenPosition);
                    break;
                case UnityEngine.InputSystem.TouchPhase.Ended:
                    EndPress(touch.screenPosition);
                    break;
                case UnityEngine.InputSystem.TouchPhase.Canceled:
                    tapCandidate = false;
                    break;
            }
        }

        void ReadTwoFingerGesture(Touch a, Touch b)
        {
            if (IsOverUI != null && (IsOverUI(a.startScreenPosition) || IsOverUI(b.startScreenPosition))) return;

            Vector2 current = b.screenPosition - a.screenPosition;
            Vector2 previous = (b.screenPosition - b.delta) - (a.screenPosition - a.delta);
            if (previous.sqrMagnitude < 1f || current.sqrMagnitude < 1f) return;

            float ratio = current.magnitude / previous.magnitude;
            if (!Mathf.Approximately(ratio, 1f)) Pinched?.Invoke(ratio);

            float angle = Vector2.SignedAngle(previous, current);
            if (Mathf.Abs(angle) > 0.01f) Twisted?.Invoke(-angle);
        }

        void ReadMouseAndKeyboard()
        {
            Mouse mouse = Mouse.current;
            if (mouse != null)
            {
                Vector2 position = mouse.position.ReadValue();
                if (mouse.leftButton.wasPressedThisFrame) BeginPress(position);
                if (mouse.leftButton.wasReleasedThisFrame) EndPress(position);

                float scroll = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(scroll) > 0.01f) Pinched?.Invoke(1f + Mathf.Clamp(scroll, -120f, 120f) * 0.001f);
            }

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;
            float twist = (keyboard.eKey.isPressed ? 1f : 0f) - (keyboard.qKey.isPressed ? 1f : 0f);
            if (twist != 0f) Twisted?.Invoke(twist * editorTwistSpeed * Time.deltaTime);
        }

        void BeginPress(Vector2 position)
        {
            tapCandidate = IsOverUI == null || !IsOverUI(position);
            pressPosition = position;
            pressTime = Time.unscaledTime;
        }

        void EndPress(Vector2 position)
        {
            if (!tapCandidate) return;
            tapCandidate = false;

            bool quick = Time.unscaledTime - pressTime <= tapMaxDuration;
            bool still = (position - pressPosition).magnitude <= tapMaxTravel * Screen.height;
            if (quick && still) Tapped?.Invoke(position);
        }
    }
}
