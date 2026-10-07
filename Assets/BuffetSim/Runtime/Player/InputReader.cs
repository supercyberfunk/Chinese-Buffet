using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace BuffetSim.Player
{
    /// <summary>
    /// Thin wrapper so the player scripts don't care which input backend the project has active.
    /// Uses the Input System package when it is enabled, otherwise the legacy Input Manager.
    /// </summary>
    public static class InputReader
    {
        public static Vector2 Move()
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard kb = Keyboard.current;
            if (kb == null) return Vector2.zero;
            float x = (kb.dKey.isPressed ? 1f : 0f) - (kb.aKey.isPressed ? 1f : 0f);
            float y = (kb.wKey.isPressed ? 1f : 0f) - (kb.sKey.isPressed ? 1f : 0f);
            return Vector2.ClampMagnitude(new Vector2(x, y), 1f);
#else
            return Vector2.ClampMagnitude(new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")), 1f);
#endif
        }

        public static Vector2 Look()
        {
#if ENABLE_INPUT_SYSTEM
            Mouse mouse = Mouse.current;
            if (mouse == null) return Vector2.zero;
            // Input System deltas are in pixels; scale to roughly match the legacy "Mouse X/Y" axes.
            return mouse.delta.ReadValue() * 0.1f;
#else
            return new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y"));
#endif
        }

        public static bool SprintHeld()
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard kb = Keyboard.current;
            return kb != null && kb.leftShiftKey.isPressed;
#else
            return Input.GetKey(KeyCode.LeftShift);
#endif
        }

        public static bool JumpPressed()
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard kb = Keyboard.current;
            return kb != null && kb.spaceKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.Space);
#endif
        }

        public static bool InteractPressed()
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard kb = Keyboard.current;
            return kb != null && kb.eKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.E);
#endif
        }

        public static bool InteractHeld()
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard kb = Keyboard.current;
            return kb != null && kb.eKey.isPressed;
#else
            return Input.GetKey(KeyCode.E);
#endif
        }

        public static bool DropPressed()
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard kb = Keyboard.current;
            return kb != null && kb.qKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.Q);
#endif
        }

        public static bool EscapePressed()
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard kb = Keyboard.current;
            return kb != null && kb.escapeKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.Escape);
#endif
        }

        public static bool ClickPressed()
        {
#if ENABLE_INPUT_SYSTEM
            Mouse mouse = Mouse.current;
            return mouse != null && mouse.leftButton.wasPressedThisFrame;
#else
            return Input.GetMouseButtonDown(0);
#endif
        }

        /// <summary>Left click: throw or use whatever is selected in the apron pocket.</summary>
        public static bool ThrowPressed() => ClickPressed();

        /// <summary>R: crack a fortune cookie.</summary>
        public static bool CrackPressed()
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard kb = Keyboard.current;
            return kb != null && kb.rKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.R);
#endif
        }

        /// <summary>Tab: next thing in the apron pocket.</summary>
        public static bool CyclePocketPressed()
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard kb = Keyboard.current;
            return kb != null && kb.tabKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.Tab);
#endif
        }

        /// <summary>F1..F12 went down this frame (the debug keys).</summary>
        public static bool FunctionKeyPressed(int number)
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard kb = Keyboard.current;
            if (kb == null) return false;
            UnityEngine.InputSystem.Controls.KeyControl key;
            switch (number)
            {
                case 1: key = kb.f1Key; break;
                case 2: key = kb.f2Key; break;
                case 3: key = kb.f3Key; break;
                case 4: key = kb.f4Key; break;
                case 5: key = kb.f5Key; break;
                case 6: key = kb.f6Key; break;
                case 7: key = kb.f7Key; break;
                case 8: key = kb.f8Key; break;
                case 9: key = kb.f9Key; break;
                case 10: key = kb.f10Key; break;
                case 11: key = kb.f11Key; break;
                case 12: key = kb.f12Key; break;
                default: return false;
            }
            return key != null && key.wasPressedThisFrame;
#else
            if (number < 1 || number > 12) return false;
            return Input.GetKeyDown(KeyCode.F1 + (number - 1));
#endif
        }
    }
}
