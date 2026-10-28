using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using Swarmblastrr.Core;

namespace Swarmblastrr.Controls
{
    /// <summary>
    /// Reads a horizontal finger / mouse drag every frame and exposes it as a world-space lateral
    /// delta. One full screen width of drag moves the crowd roughly <c>2 * LaneHalfWidth *
    /// Sensitivity</c> world units. Runs before gameplay scripts so <c>CrowdController.Update</c>
    /// sees this frame's value. Works with either the Input System package or the legacy manager.
    /// Only produces a delta while a run is in progress (Running, BossFight, RewardRamp) so drags on
    /// the shop or result UI never move the crowd.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class SwipeInput : MonoBehaviour
    {
        private const float DefaultLaneHalfWidth = 3f;

        /// <summary>Drag multiplier: one screen width of drag ≈ 2 * LaneHalfWidth * Sensitivity world units.</summary>
        public float Sensitivity = 1f;

        /// <summary>True while a pointer (touch or mouse button) is held down during a run.</summary>
        public bool IsPressed { get; private set; }

        /// <summary>World-space lateral delta accumulated this frame; 0 when not dragging or outside a run.</summary>
        public float DeltaX { get; private set; }

        /// <summary>Pointer x mapped to -1..1 across the screen, for debugging.</summary>
        public float NormalizedScreenX { get; private set; }

        private float _lastX;

        private void Update()
        {
            bool pressed;
            float pointerX;
            bool hasPointer = TryReadPointer(out pressed, out pointerX);

            float screenWidth = Mathf.Max(1f, Screen.width);

            if (hasPointer)
            {
                NormalizedScreenX = Mathf.Clamp(pointerX / screenWidth * 2f - 1f, -1f, 1f);
            }

            // Outside a run the pointer belongs to the UI: treat it as released so a drag across the
            // shop / result panel (or a press that started on PLAY) cannot offset the crowd.
            if (!hasPointer || !pressed || !IsInRun())
            {
                IsPressed = false;
                DeltaX = 0f;
                return;
            }

            if (!IsPressed)
            {
                IsPressed = true;
                _lastX = pointerX;
                DeltaX = 0f;
                return;
            }

            float laneHalfWidth = ResolveLaneHalfWidth();
            DeltaX = (pointerX - _lastX) / screenWidth * (2f * laneHalfWidth) * Sensitivity;
            _lastX = pointerX;
        }

        private void OnDisable()
        {
            IsPressed = false;
            DeltaX = 0f;
        }

        private static bool IsInRun()
        {
            GameManager game = GameManager.Instance;
            if (game == null)
            {
                return false;
            }

            GameState state = game.State;
            return state == GameState.Running || state == GameState.BossFight || state == GameState.RewardRamp;
        }

        private static float ResolveLaneHalfWidth()
        {
            GameManager game = GameManager.Instance;
            if (game != null && game.Config != null)
            {
                return game.Config.LaneHalfWidth;
            }

            return DefaultLaneHalfWidth;
        }

        /// <summary>
        /// Reads the primary pointer. Returns false when no pointer device exists this frame.
        /// </summary>
        private static bool TryReadPointer(out bool pressed, out float x)
        {
#if ENABLE_INPUT_SYSTEM
            Pointer pointer = Pointer.current;
            if (pointer == null)
            {
                pressed = false;
                x = 0f;
                return false;
            }

            pressed = pointer.press.isPressed;
            x = pointer.position.ReadValue().x;
            return true;
#else
            if (Input.touchCount > 0)
            {
                Touch touch = Input.GetTouch(0);
                pressed = touch.phase != TouchPhase.Ended && touch.phase != TouchPhase.Canceled;
                x = touch.position.x;
                return true;
            }

            pressed = Input.GetMouseButton(0);
            x = Input.mousePosition.x;
            return true;
#endif
        }
    }
}
