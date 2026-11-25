using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Ninjazon.Game
{
    public enum PlayerAction { None, Jump, LaneUp, LaneDown, Dash, Pause }

    /// <summary>
    /// Turns raw touches/mouse/keyboard into game actions:
    ///   tap = jump, swipe up/down = change lane, swipe right = dash.
    /// Swipes fire the moment the finger has travelled far enough (not on
    /// release) so they feel instant. A tap fires on release.
    /// </summary>
    public sealed class GameInput
    {
        private readonly List<RaycastResult> uiHits = new();
        private readonly Queue<PlayerAction> queue = new();
        private Vector2 start;
        private bool tracking;
        private bool swipeFired;
        private float pressTime;

        public bool Enabled { get; set; }

        public void Poll()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.escapeKey.wasPressedThisFrame || keyboard.pKey.wasPressedThisFrame) queue.Enqueue(PlayerAction.Pause);
                if (Enabled)
                {
                    if (keyboard.spaceKey.wasPressedThisFrame)
                        queue.Enqueue(PlayerAction.Jump);
                    if (keyboard.upArrowKey.wasPressedThisFrame || keyboard.wKey.wasPressedThisFrame)
                        queue.Enqueue(PlayerAction.LaneUp);
                    if (keyboard.downArrowKey.wasPressedThisFrame || keyboard.sKey.wasPressedThisFrame)
                        queue.Enqueue(PlayerAction.LaneDown);
                    if (keyboard.rightArrowKey.wasPressedThisFrame || keyboard.dKey.wasPressedThisFrame ||
                        keyboard.leftShiftKey.wasPressedThisFrame || keyboard.rightShiftKey.wasPressedThisFrame)
                        queue.Enqueue(PlayerAction.Dash);
                }
            }

            var pointer = Pointer.current;
            if (pointer == null) return;
            if (!Enabled)
            {
                tracking = false;
                return;
            }

            var position = pointer.position.ReadValue();
            if (pointer.press.wasPressedThisFrame)
            {
                tracking = !OverButton(position);
                swipeFired = false;
                start = position;
                pressTime = Time.unscaledTime;
            }

            if (!tracking) return;

            // Threshold scales with screen size so it feels the same on every phone.
            var threshold = Mathf.Max(24f, Screen.height * 0.05f);
            var delta = position - start;
            if (!swipeFired && delta.magnitude > threshold)
            {
                swipeFired = true;
                if (Mathf.Abs(delta.x) > Mathf.Abs(delta.y))
                    queue.Enqueue(delta.x > 0 ? PlayerAction.Dash : PlayerAction.None);
                else
                    queue.Enqueue(delta.y > 0 ? PlayerAction.LaneUp : PlayerAction.LaneDown);
            }

            if (pointer.press.wasReleasedThisFrame)
            {
                if (!swipeFired && Time.unscaledTime - pressTime < 0.6f) queue.Enqueue(PlayerAction.Jump);
                tracking = false;
            }
        }

        public bool TryNext(out PlayerAction action)
        {
            while (queue.Count > 0)
            {
                action = queue.Dequeue();
                if (action != PlayerAction.None) return true;
            }
            action = PlayerAction.None;
            return false;
        }

        public void Clear()
        {
            queue.Clear();
            tracking = false;
        }

        private bool OverButton(Vector2 screenPosition)
        {
            var es = EventSystem.current;
            if (es == null) return false;
            uiHits.Clear();
            es.RaycastAll(new PointerEventData(es) { position = screenPosition }, uiHits);
            foreach (var hit in uiHits)
                if (hit.gameObject.GetComponentInParent<Selectable>() != null) return true;
            return false;
        }
    }
}
