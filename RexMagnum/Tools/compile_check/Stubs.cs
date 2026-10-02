// Compile-check stubs. Every member below was checked against the real package source
// (InputSystem 1.11.2, URP/Core 17 6000.0/staging, uGUI). Bodies are empty.
#pragma warning disable
using System.Collections.Generic;
using UnityEngine;
namespace UnityEngine.InputSystem.Controls {
  public class InputControl<T> where T : struct { public T ReadValue() => default; }
  public class AxisControl : InputControl<float> {}
  public class ButtonControl : AxisControl { public bool isPressed, wasPressedThisFrame, wasReleasedThisFrame; }
  public class KeyControl : ButtonControl { public UnityEngine.InputSystem.Key keyCode; public string displayName; }
  public class AnyKeyControl : ButtonControl {}
  public class Vector2Control : InputControl<Vector2> {}
  public class StickControl : Vector2Control {}
  public class DpadControl : Vector2Control { public ButtonControl up, down, left, right; }
  public class DeltaControl : Vector2Control {}
  public class IntegerControl : InputControl<int> {}
  public class TouchPhaseControl : InputControl<UnityEngine.InputSystem.TouchPhase> {}
  public class TouchPressControl : ButtonControl {}
  public class TouchControl { public TouchPressControl press; public Vector2Control position; public Vector2Control delta;
    public TouchPhaseControl phase; public IntegerControl touchId; }
}
namespace UnityEngine.InputSystem {
  using UnityEngine.InputSystem.Controls;
  public enum TouchPhase { None, Began, Moved, Ended, Canceled, Stationary }
  public enum Key { None, Space, Enter, Tab, Backquote, Quote, Semicolon, Comma, Period, Slash, Backslash, LeftBracket, RightBracket, Minus, Equals,
    A, B, C, D, E, F, G, H, I, J, K, L, M, N, O, P, Q, R, S, T, U, V, W, X, Y, Z,
    Digit1, Digit2, Digit3, Digit4, Digit5, Digit6, Digit7, Digit8, Digit9, Digit0, LeftShift, RightShift, LeftAlt, RightAlt, AltGr = RightAlt,
    LeftCtrl, RightCtrl, LeftMeta, RightMeta, LeftWindows = LeftMeta, RightWindows = RightMeta, LeftApple = LeftMeta, RightApple = RightMeta,
    LeftCommand = LeftMeta, RightCommand = RightMeta, ContextMenu, Escape, LeftArrow, RightArrow, UpArrow, DownArrow, Backspace, PageDown, PageUp,
    Home, End, Insert, Delete, CapsLock, NumLock, PrintScreen, ScrollLock, Pause, NumpadEnter, NumpadDivide, NumpadMultiply, NumpadPlus,
    NumpadMinus, NumpadPeriod, NumpadEquals, Numpad0, Numpad1, Numpad2, Numpad3, Numpad4, Numpad5, Numpad6, Numpad7, Numpad8, Numpad9,
    F1, F2, F3, F4, F5, F6, F7, F8, F9, F10, F11, F12, OEM1, OEM2, OEM3, OEM4, OEM5, IMESelected }
  public class InputDevice { public string displayName, name; public double lastUpdateTime; }
  public class Keyboard : InputDevice { public static Keyboard current; public KeyControl this[Key key] => null; public AnyKeyControl anyKey; public UnityEngine.InputSystem.Utilities.ReadOnlyArray<KeyControl> allKeys; }
  public class Pointer : InputDevice { public Vector2Control position; public DeltaControl delta; }
  public class Mouse : Pointer { public static new Mouse current; public ButtonControl leftButton, rightButton, middleButton; public DeltaControl scroll; }
  public class Gamepad : InputDevice { public static Gamepad current; public ButtonControl this[UnityEngine.InputSystem.LowLevel.GamepadButton button] => null; public StickControl leftStick, rightStick; public DpadControl dpad;
    public ButtonControl leftShoulder, rightShoulder, leftTrigger, rightTrigger, buttonSouth, buttonNorth, buttonEast, buttonWest,
      startButton, selectButton, leftStickButton, rightStickButton; public void SetMotorSpeeds(float low, float high) {} }
  public class Touchscreen : Pointer { public static new Touchscreen current; public TouchControl primaryTouch;
    public UnityEngine.InputSystem.Utilities.ReadOnlyArray<TouchControl> touches; }
}
namespace UnityEngine.InputSystem.LowLevel {
  public enum GamepadButton { DpadUp = 0, DpadDown = 1, DpadLeft = 2, DpadRight = 3, North = 4, East = 5, South = 6, West = 7,
    LeftStick = 8, RightStick = 9, LeftShoulder = 10, RightShoulder = 11, Start = 12, Select = 13, LeftTrigger = 32, RightTrigger = 33,
    X = West, Y = North, A = South, B = East }
}
namespace UnityEngine.InputSystem.Utilities {
  public struct ReadOnlyArray<T> : IEnumerable<T> { public int Count => 0; public T this[int i] => default;
    public IEnumerator<T> GetEnumerator() { yield break; } System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator(); }
}
namespace UnityEngine.UI {
  public abstract class UIBehaviour : MonoBehaviour {}
  public class Graphic : UIBehaviour { public Color color; public bool raycastTarget; public Material material; public RectTransform rectTransform => null; }
  public class MaskableGraphic : Graphic {}
  public class Text : MaskableGraphic { public string text; public Font font; public int fontSize; public TextAnchor alignment; public FontStyle fontStyle;
    public HorizontalWrapMode horizontalOverflow; public VerticalWrapMode verticalOverflow; public bool supportRichText; public float lineSpacing; }
  public class Image : MaskableGraphic { public Sprite sprite; public bool preserveAspect; public enum Type { Simple, Sliced, Tiled, Filled } public Type type; public float fillAmount; }
  public class RawImage : MaskableGraphic { public Texture texture; }
  public class BaseMeshEffect : UIBehaviour {}
  public class Shadow : BaseMeshEffect { public Color effectColor; public Vector2 effectDistance; public bool useGraphicAlpha; }
  public class Outline : Shadow {}
  public class CanvasScaler : UIBehaviour { public enum ScaleMode { ConstantPixelSize, ScaleWithScreenSize, ConstantPhysicalSize }
    public enum ScreenMatchMode { MatchWidthOrHeight, Expand, Shrink } public ScaleMode uiScaleMode; public Vector2 referenceResolution;
    public ScreenMatchMode screenMatchMode; public float matchWidthOrHeight; public float scaleFactor; }
  public class GraphicRaycaster : MonoBehaviour {}
}
namespace UnityEngine.Rendering {
  public abstract class VolumeComponent : ScriptableObject { public bool active = true; }
  public abstract class VolumeParameter {}
  public class VolumeParameter<T> : VolumeParameter { public T value; public bool overrideState; public void Override(T x) {} }
  public class FloatParameter : VolumeParameter<float> {}
  public class MinFloatParameter : FloatParameter {}
  public class ClampedFloatParameter : FloatParameter {}
  public class VolumeProfile : ScriptableObject { public List<VolumeComponent> components = new List<VolumeComponent>();
    public T Add<T>(bool overrides = false) where T : VolumeComponent => null; public bool TryGet<T>(out T component) where T : VolumeComponent { component = null; return false; } }
  public class Volume : MonoBehaviour { public bool isGlobal; public float priority, weight; public VolumeProfile sharedProfile, profile; }
}
namespace UnityEngine.Rendering.Universal {
  using UnityEngine.Rendering;
  public enum TonemappingMode { None, Neutral, ACES }
  public class TonemappingModeParameter : VolumeParameter<TonemappingMode> {}
  public class Tonemapping : VolumeComponent { public TonemappingModeParameter mode; }
  public class Bloom : VolumeComponent { public MinFloatParameter threshold, intensity; public ClampedFloatParameter scatter; }
  public class ColorAdjustments : VolumeComponent { public FloatParameter postExposure; public ClampedFloatParameter contrast, saturation; }
  public class WhiteBalance : VolumeComponent { public ClampedFloatParameter temperature, tint; }
  public class Vignette : VolumeComponent { public ClampedFloatParameter intensity, smoothness; }
  public abstract class ScriptableRendererFeature : ScriptableObject {}
  public abstract class ScriptableRendererData : ScriptableObject { public List<ScriptableRendererFeature> rendererFeatures => null; }
  public class UniversalRendererData : ScriptableRendererData {}
  public class UniversalRenderPipelineAsset : RenderPipelineAsset {
    public override RenderPipeline CreatePipeline() => null;
    public static UniversalRenderPipelineAsset Create(ScriptableRendererData rendererData = null) => null;
    public bool supportsCameraDepthTexture, supportsCameraOpaqueTexture; public int msaaSampleCount; public float renderScale, shadowDistance; }
  public enum AntialiasingMode { None, FastApproximateAntialiasing, SubpixelMorphologicalAntiAliasing, TemporalAntiAliasing }
  public class UniversalAdditionalCameraData : MonoBehaviour { public bool renderPostProcessing; public AntialiasingMode antialiasing; }
  public sealed class UniversalRenderPipeline { public class SingleCameraRequest { public RenderTexture destination; public int mipLevel; public CubemapFace face; public int slice; } }
}
public static class RPX { public static bool SupportsRenderRequest<T>(UnityEngine.Camera c, T d) => true; public static void SubmitRenderRequest<T>(UnityEngine.Camera c, T d) {} }
