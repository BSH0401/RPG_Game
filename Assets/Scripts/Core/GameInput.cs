using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace MoonlightPost
{
    /// <summary>
    /// 입력을 한 곳에서 읽는다. 프로젝트 설정이 새 Input System이든 기존 Input Manager든 동작한다.
    /// 키 배치를 바꾸고 싶으면 이 파일만 고치면 된다.
    /// </summary>
    public static class GameInput
    {
#if ENABLE_INPUT_SYSTEM
        static Keyboard Kb => Keyboard.current;
        static bool Held(Key key) => Kb != null && Kb[key].isPressed;
        static bool Down(Key key) => Kb != null && Kb[key].wasPressedThisFrame;
        static bool MouseLeftDown => Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
        static bool MouseRightHeld => Mouse.current != null && Mouse.current.rightButton.isPressed;

        public static Vector2 Move
        {
            get
            {
                float x = (Held(Key.D) || Held(Key.RightArrow) ? 1f : 0f) - (Held(Key.A) || Held(Key.LeftArrow) ? 1f : 0f);
                float y = (Held(Key.W) || Held(Key.UpArrow) ? 1f : 0f) - (Held(Key.S) || Held(Key.DownArrow) ? 1f : 0f);
                return Vector2.ClampMagnitude(new Vector2(x, y), 1f);
            }
        }

        public static bool AttackPressed => MouseLeftDown || Down(Key.J);
        public static bool DodgePressed => Down(Key.Space) || Down(Key.LeftShift);
        public static bool InteractPressed => Down(Key.E);
        public static bool ToolPressed => Down(Key.Q);
        public static bool MapTogglePressed => Down(Key.Tab) || Down(Key.M);
        public static bool AdvancePressed => Down(Key.E) || Down(Key.Space) || Down(Key.Enter) || MouseLeftDown;
        public static bool HelpPressed => Down(Key.F1);
        public static bool ResetPressed => Down(Key.F12);
        public static bool SkipPressed => Down(Key.F11);
        public static bool InventoryPressed => Down(Key.I);
        public static bool UseItemPressed => Down(Key.R);
        /// <summary>우편가방 막기: 마우스 오른쪽 또는 K 를 누르고 있는 동안.</summary>
        public static bool GuardHeld => MouseRightHeld || Held(Key.K);
        public static bool SwitchToolPressed => Down(Key.C);
        public static bool PausePressed => Down(Key.Escape);
        public static bool MenuUp => Down(Key.W) || Down(Key.UpArrow);
        public static bool MenuDown => Down(Key.S) || Down(Key.DownArrow);
        public static bool MenuLeft => Down(Key.A) || Down(Key.LeftArrow);
        public static bool MenuRight => Down(Key.D) || Down(Key.RightArrow);
        public static bool MenuConfirm => Down(Key.Enter) || Down(Key.Space) || Down(Key.E);

        public static bool ChoicePressed(int index)
        {
            switch (index)
            {
                case 0: return Down(Key.Digit1) || Down(Key.Numpad1);
                case 1: return Down(Key.Digit2) || Down(Key.Numpad2);
                case 2: return Down(Key.Digit3) || Down(Key.Numpad3);
                default: return false;
            }
        }
#else
        public static Vector2 Move
        {
            get
            {
                float x = (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow) ? 1f : 0f)
                        - (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow) ? 1f : 0f);
                float y = (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow) ? 1f : 0f)
                        - (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow) ? 1f : 0f);
                return Vector2.ClampMagnitude(new Vector2(x, y), 1f);
            }
        }

        public static bool AttackPressed => Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.J);
        public static bool DodgePressed => Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.LeftShift);
        public static bool InteractPressed => Input.GetKeyDown(KeyCode.E);
        public static bool ToolPressed => Input.GetKeyDown(KeyCode.Q);
        public static bool MapTogglePressed => Input.GetKeyDown(KeyCode.Tab) || Input.GetKeyDown(KeyCode.M);
        public static bool AdvancePressed => Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Space)
                                          || Input.GetKeyDown(KeyCode.Return) || Input.GetMouseButtonDown(0);
        public static bool HelpPressed => Input.GetKeyDown(KeyCode.F1);
        public static bool ResetPressed => Input.GetKeyDown(KeyCode.F12);
        public static bool SkipPressed => Input.GetKeyDown(KeyCode.F11);
        public static bool InventoryPressed => Input.GetKeyDown(KeyCode.I);
        public static bool UseItemPressed => Input.GetKeyDown(KeyCode.R);
        /// <summary>우편가방 막기: 마우스 오른쪽 또는 K 를 누르고 있는 동안.</summary>
        public static bool GuardHeld => Input.GetMouseButton(1) || Input.GetKey(KeyCode.K);
        public static bool SwitchToolPressed => Input.GetKeyDown(KeyCode.C);
        public static bool PausePressed => Input.GetKeyDown(KeyCode.Escape);
        public static bool MenuUp => Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow);
        public static bool MenuDown => Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow);
        public static bool MenuLeft => Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow);
        public static bool MenuRight => Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow);
        public static bool MenuConfirm => Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.E);

        public static bool ChoicePressed(int index)
        {
            switch (index)
            {
                case 0: return Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1);
                case 1: return Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2);
                case 2: return Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3);
                default: return false;
            }
        }
#endif
    }
}
