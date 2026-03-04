/*
 * Zoomify - Optifine-style zoom mod for Raft
 * Copyright (C) 2026 Flaze
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Affero General Public License as published
 * by the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 * GNU Affero General Public License for more details.
 *
 * You should have received a copy of the GNU Affero General Public License
 * along with this program. If not, see <https://www.gnu.org/licenses/>.
 */

using RaftModLoader;
using UnityEngine;
using UnityEngine.UI;
using HMLLibrary;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;

public class Zoomify : Mod
{
    // Singleton instance
    public static Zoomify Instance { get; private set; }

    // Harmony instance for patching
    private Harmony harmony;

    // -------------------------------------------------------------------------
    // ExtraSettingsAPI integration
    // -------------------------------------------------------------------------
    static bool ExtraSettingsAPI_Loaded = false;

    // Keybind identifier returned by ExtraSettingsAPI (used with MyInput)
    private string zoomKeybindName = null;

    // -------------------------------------------------------------------------
    // Settings (populated from ExtraSettingsAPI or from defaults)
    // -------------------------------------------------------------------------
    private float defaultZoomFactor = 5f;    // zoom factor when entering zoom (slider)
    private float scrollStep        = 1f;    // base step per scroll tick       (slider)
    private bool  enableSmoothing   = true;  // lerp on/off                    (checkbox)
    private float smoothingSpeed    = 10f;   // lerp speed                     (slider)

    // -------------------------------------------------------------------------
    // Runtime state
    // -------------------------------------------------------------------------
    private float originalFOV        = 70f;  // always tracks the game's real FOV setting
    // Zoom is stored as a multiplicative factor (e.g. 4.67 = 4.67x zoom).
    // Effective zoom FOV = originalFOV / currentZoomFactor.
    // This makes zoom feel identical regardless of what the player's game FOV is set to.
    private float currentZoomFactor  = 5f;   // 1 = no zoom, >1 = zoomed in (default 5x)
    private float currentAppliedFOV  = 70f;  // our own lerp accumulator (not read from cam)
    internal bool isZooming          = false;
    private bool  wasZoomingLastFrame = false;

    // -------------------------------------------------------------------------
    // Zoom indicator UI
    // -------------------------------------------------------------------------
    private Canvas    zoomCanvas      = null;
    private GameObject zoomLabelObject = null;
    private Text      zoomLabelText   = null;

    // -------------------------------------------------------------------------
    // Lifecycle
    // -------------------------------------------------------------------------
    public void Start()
    {
        Instance = this;
        harmony  = new Harmony("com.flaze.zoomify");

        // Camera.onPreRender fires after ALL Update/LateUpdate calls, right before
        // the camera draws. Nothing in Raft can overwrite our FOV after this point.
        Camera.onPreRender += OnCameraPreRender;

        // Patch the exact methods we care about directly by type rather than
        // scanning IL at runtime, which is unreliable in Unity/Mono.
        PatchTargetedMethods();

        CreateZoomIndicatorUI();

        Debug.Log("[Zoomify]: Mod loaded.");
    }

    public void OnModUnload()
    {
        Camera.onPreRender -= OnCameraPreRender;
        harmony?.UnpatchAll(harmony.Id);
        RestoreOriginalFOV();
        if (zoomCanvas != null)
            Destroy(zoomCanvas.gameObject);
        Debug.Log("[Zoomify]: Mod unloaded.");
    }

    // -------------------------------------------------------------------------
    // Update – input reading and scroll logic (no FOV write here)
    // -------------------------------------------------------------------------
    public void Update()
    {
        Camera cam = Camera.main;
        if (cam == null)
            return;

        // Determine zoom key state
        if (ExtraSettingsAPI_Loaded && zoomKeybindName != null)
            isZooming = MyInput.GetButton(zoomKeybindName);
        else
            isZooming = Input.GetKey(KeyCode.C); // fallback when API not installed

        // Read scroll wheel for zoom adjustment.
        // Our own Update is in the Zoomify assembly (not Assembly-CSharp),
        // so the scroll-blocking transpiler never touches this call – we always
        // get the real axis value here.
        if (isZooming)
        {
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (scroll != 0f)
            {
                // Base step comes from the scrollStep setting.
                // Multiply at higher zoom levels for comfortable coarse adjustment:
                //   1x – 10x  → scrollStep
                //  10x – 20x  → scrollStep * 2
                //  20x+       → scrollStep * 5
                float stepSize = currentZoomFactor < 10f ? scrollStep
                               : currentZoomFactor < 20f ? scrollStep * 2f
                               : scrollStep * 5f;
                stepSize = Mathf.Max(0.1f, stepSize); // guard against zero/NaN

                int direction = scroll > 0f ? 1 : -1;

                // Snap to nearest step boundary, then move one step.
                float snapped = Mathf.Round(currentZoomFactor / stepSize) * stepSize;
                currentZoomFactor = snapped + direction * stepSize;
                currentZoomFactor = Mathf.Clamp(currentZoomFactor, 1f, 50f);
            }
        }

        // Reset to base zoom factor when key is released
        if (wasZoomingLastFrame && !isZooming)
            currentZoomFactor = TargetZoomFactor();

        wasZoomingLastFrame = isZooming;

        // Update zoom indicator
        UpdateZoomIndicator();
    }

    // -------------------------------------------------------------------------
    // Camera.onPreRender – FOV is written HERE, guaranteed last-in-frame
    // -------------------------------------------------------------------------
    private void OnCameraPreRender(Camera cam)
    {
        if (cam != Camera.main)
            return;

        if (isZooming)
        {
            // Compute effective FOV from the relative factor.
            // Clamped so we never reach or exceed originalFOV (which would disable zoom)
            // and never go below 1 degree.
            float effectiveFOV = Mathf.Clamp(originalFOV / currentZoomFactor, 1f, originalFOV - 1f);

            // Apply zoom FOV.
            if (enableSmoothing)
                currentAppliedFOV = Mathf.Lerp(currentAppliedFOV, effectiveFOV, Time.deltaTime * smoothingSpeed);
            else
                currentAppliedFOV = effectiveFOV;

            cam.fieldOfView = currentAppliedFOV;
        }
        else
        {
            // Read the FOV Raft set this frame (from Settings) – never override it,
            // so the player can change FOV freely in real time.
            float gameFOV = cam.fieldOfView;
            originalFOV   = gameFOV;

            if (enableSmoothing && Mathf.Abs(currentAppliedFOV - gameFOV) > 0.05f)
            {
                // Smoothly return from zoom without snapping
                currentAppliedFOV = Mathf.Lerp(currentAppliedFOV, gameFOV, Time.deltaTime * smoothingSpeed);
                cam.fieldOfView   = currentAppliedFOV;
            }
            else
            {
                // Not zooming and no lerp needed – let Raft's value stand unmodified
                currentAppliedFOV = gameFOV;
            }
        }
    }

    // -------------------------------------------------------------------------
    // Zoom indicator UI
    // -------------------------------------------------------------------------
    private void CreateZoomIndicatorUI()
    {
        try
        {
            GameObject canvasObject = new GameObject("ZoomifyIndicatorCanvas");
            zoomCanvas = canvasObject.AddComponent<Canvas>();
            zoomCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            zoomCanvas.sortingOrder = 1;

            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            canvasObject.AddComponent<GraphicRaycaster>();
            DontDestroyOnLoad(canvasObject);

            zoomLabelObject = new GameObject("ZoomifyIndicatorText");
            zoomLabelObject.transform.SetParent(zoomCanvas.transform, false);

            zoomLabelText = zoomLabelObject.AddComponent<Text>();

            // Find font: first check existing Text components in the scene (same pattern as ComboCounter).
            // Raft's own UI Text components already have the game font attached, so this is the
            // most reliable way to get a non-Arial font without a direct asset reference.
            Font gameFont = null;
            try
            {
                Text[] allTexts = Resources.FindObjectsOfTypeAll<Text>();
                foreach (Text t in allTexts)
                {
                    if (t.font != null && t.font.name != "Arial")
                    {
                        gameFont = t.font;
                        break;
                    }
                }

                if (gameFont == null)
                {
                    Font[] allFonts = Resources.FindObjectsOfTypeAll<Font>();
                    foreach (Font f in allFonts)
                    {
                        string n = f.name.ToLower();
                        if (n.Contains("chinese") || n.Contains("rock") ||
                            n.Contains("raft")    || n.Contains("game"))
                        {
                            gameFont = f;
                            break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[Zoomify]: Could not search for game font: " + ex.Message);
            }

            zoomLabelText.font      = gameFont ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
            zoomLabelText.fontSize  = 30;
            zoomLabelText.alignment = TextAnchor.MiddleCenter;
            zoomLabelText.color     = new Color(0.95f, 0.89f, 0.77f, 1f); // #F2E2C5, same as ComboCounter
            zoomLabelText.text      = "";

            Outline outline = zoomLabelObject.AddComponent<Outline>();
            outline.effectColor    = new Color(0f, 0f, 0f, 0.8f);
            outline.effectDistance = new Vector2(2f, -2f);

            // Center-anchor, offset upward from screen center – puts it above the hotbar
            // at the same visual layer used by ComboCounter (-80 is center-minus-80).
            // We mirror that setup but shift to sit just above the hotbar (~-380 from center
            // in 1080p = 160px from the bottom edge).
            RectTransform rect = zoomLabelObject.GetComponent<RectTransform>();
            rect.anchorMin        = new Vector2(0.5f, 0.5f);
            rect.anchorMax        = new Vector2(0.5f, 0.5f);
            rect.pivot            = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0f, -425f); // 5% closer to hotbar than -380
            rect.sizeDelta        = new Vector2(300f, 50f);

            zoomLabelObject.SetActive(false);

            Debug.Log("[Zoomify]: Zoom indicator UI created, font: " +
                      (gameFont != null ? gameFont.name : "Arial (fallback)"));
        }
        catch (Exception ex)
        {
            Debug.LogError("[Zoomify]: ERROR creating zoom indicator UI: " + ex.Message);
        }
    }

    // Called every Update – updates text and visibility of the zoom indicator.
    private void UpdateZoomIndicator()
    {
        if (zoomLabelText == null)
            return;

        if (isZooming && originalFOV > 0f)
        {
            float multiplier = originalFOV / currentAppliedFOV;
            zoomLabelText.text = multiplier.ToString("0.0") + "x";
            if (!zoomLabelObject.activeSelf)
                zoomLabelObject.SetActive(true);
        }
        else
        {
            if (zoomLabelObject.activeSelf)
                zoomLabelObject.SetActive(false);
        }
    }

    // -------------------------------------------------------------------------
    // Scroll-blocking + mouse sensitivity scaling: targeted Harmony Transpilers
    //
    // We patch exactly the two methods we care about directly by type:
    //   - Hotbar.HandleHotbarSelection  (private) – blocks hotbar scroll while zooming
    //   - MouseLook.Update              (private) – scales mouse sensitivity by FOV ratio
    //
    // The Transpiler replaces every Input.GetAxis / Input.GetAxisRaw call in those
    // methods with our wrapper, which:
    //   "Mouse ScrollWheel" while zooming → return 0   (blocks hotbar slot switching)
    //   "Mouse X"/"Mouse Y" while zooming → value * FOV ratio (linear, feels natural)
    //   anything else, or not zooming    → pass through unchanged
    // -------------------------------------------------------------------------
    private static readonly string[] ScrollAxisNames    = { "Mouse ScrollWheel" };
    private static readonly string[] MouseLookAxisNames = { "Mouse X", "Mouse Y" };

    private void PatchTargetedMethods()
    {
        var transpiler = new HarmonyMethod(
            typeof(Zoomify).GetMethod(nameof(AxisTranspiler),
                BindingFlags.NonPublic | BindingFlags.Static));

        int patchCount = 0;

        // Hotbar.HandleHotbarSelection – private, handles scroll-wheel hotbar switching
        try
        {
            MethodInfo method = typeof(Hotbar).GetMethod(
                "HandleHotbarSelection",
                BindingFlags.NonPublic | BindingFlags.Instance);
            if (method != null)
            {
                harmony.Patch(method, transpiler: transpiler);
                patchCount++;
                Debug.Log("[Zoomify]: Patched Hotbar.HandleHotbarSelection");
            }
            else
            {
                Debug.LogWarning("[Zoomify]: Could not find Hotbar.HandleHotbarSelection");
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning("[Zoomify]: Could not patch Hotbar.HandleHotbarSelection – " + e.Message);
        }

        // MouseLook.Update – private, reads Mouse X/Y for camera rotation
        try
        {
            MethodInfo method = typeof(MouseLook).GetMethod(
                "Update",
                BindingFlags.NonPublic | BindingFlags.Instance);
            if (method != null)
            {
                harmony.Patch(method, transpiler: transpiler);
                patchCount++;
                Debug.Log("[Zoomify]: Patched MouseLook.Update");
            }
            else
            {
                Debug.LogWarning("[Zoomify]: Could not find MouseLook.Update");
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning("[Zoomify]: Could not patch MouseLook.Update – " + e.Message);
        }

        Debug.Log("[Zoomify]: Targeted patching complete, " + patchCount + " method(s) patched.");
    }

    // Returns the current FOV ratio (currentAppliedFOV / originalFOV).
    // At 70 FOV original and 15 FOV zoom this gives ~0.21.
    // Linear: half the FOV means half the mouse speed, which feels natural.
    public float GetFOVRatio()
    {
        if (originalFOV <= 0f)
            return 1f;
        return currentAppliedFOV / originalFOV;
    }

    // Wrapper replacing Input.GetAxis in patched methods.
    //   "Mouse ScrollWheel" while zooming → 0        (blocks hotbar slot switching)
    //   "Mouse X"/"Mouse Y" while zooming → value * FOV ratio (linear, feels natural)
    //   anything else, or not zooming    → unchanged
    public static float GetAxisWrapper(string axisName)
    {
        float raw = Input.GetAxis(axisName);

        if (Instance == null || !Instance.isZooming)
            return raw;

        // Block scroll wheel while zoom key is held
        foreach (string axis in ScrollAxisNames)
            if (axisName == axis) return 0f;

        // Scale mouse sensitivity linearly by FOV ratio:
        // half the FOV = half the mouse speed, which is physically correct
        foreach (string axis in MouseLookAxisNames)
        {
            if (axisName == axis)
            {
                float fovRatio = Instance.GetFOVRatio();
                return raw * fovRatio;
            }
        }

        return raw;
    }

    // Same wrapper but for GetAxisRaw calls.
    public static float GetAxisRawWrapper(string axisName)
    {
        float raw = Input.GetAxisRaw(axisName);

        if (Instance == null || !Instance.isZooming)
            return raw;

        foreach (string axis in ScrollAxisNames)
            if (axisName == axis) return 0f;

        foreach (string axis in MouseLookAxisNames)
        {
            if (axisName == axis)
            {
                float fovRatio = Instance.GetFOVRatio();
                return raw * fovRatio;
            }
        }

        return raw;
    }

    // Harmony Transpiler: replaces all Input.GetAxis and Input.GetAxisRaw
    // calls in the target method with our wrapper calls.
    private static IEnumerable<CodeInstruction> AxisTranspiler(
        IEnumerable<CodeInstruction> instructions)
    {
        MethodInfo getAxis    = typeof(Input).GetMethod("GetAxis",    new[] { typeof(string) });
        MethodInfo getAxisRaw = typeof(Input).GetMethod("GetAxisRaw", new[] { typeof(string) });
        MethodInfo wrapAxis    = typeof(Zoomify).GetMethod(nameof(GetAxisWrapper),    BindingFlags.Public | BindingFlags.Static);
        MethodInfo wrapAxisRaw = typeof(Zoomify).GetMethod(nameof(GetAxisRawWrapper), BindingFlags.Public | BindingFlags.Static);

        foreach (CodeInstruction instr in instructions)
        {
            if ((instr.opcode == OpCodes.Call || instr.opcode == OpCodes.Callvirt)
                && instr.operand is MethodInfo m)
            {
                if (m == getAxis)
                {
                    yield return new CodeInstruction(OpCodes.Call, wrapAxis);
                    continue;
                }
                if (m == getAxisRaw)
                {
                    yield return new CodeInstruction(OpCodes.Call, wrapAxisRaw);
                    continue;
                }
            }
            yield return instr;
        }
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------
    // Returns the default zoom factor from settings.
    private float TargetZoomFactor()
    {
        return Mathf.Clamp(defaultZoomFactor, 1f, 50f);
    }

    private void RestoreOriginalFOV()
    {
        Camera cam = Camera.main;
        if (cam != null)
            cam.fieldOfView = originalFOV;
    }

    private void LoadSettings()
    {
        if (!ExtraSettingsAPI_Loaded)
            return;
        // Mathf.Max guards prevent a 0-return from the stub breaking zoom entirely.
        defaultZoomFactor = Mathf.Max(1f,   ExtraSettingsAPI_GetSliderValue("defaultZoomFactor"));
        scrollStep        = Mathf.Max(0.5f,  ExtraSettingsAPI_GetSliderValue("scrollStep"));
        enableSmoothing   = ExtraSettingsAPI_GetCheckboxState("enableSmoothing");
        smoothingSpeed    = Mathf.Max(1f,   ExtraSettingsAPI_GetSliderValue("smoothingSpeed"));
    }

    // -------------------------------------------------------------------------
    // ExtraSettingsAPI events
    // -------------------------------------------------------------------------
    void ExtraSettingsAPI_Load()
    {
        ExtraSettingsAPI_Loaded = true;
        LoadSettings();
        zoomKeybindName = ExtraSettingsAPI_GetKeybindName("zoomKey");
        if (!isZooming)
            currentZoomFactor = TargetZoomFactor();
    }

    // Also apply settings when the panel is opened so the user can see
    // values take effect immediately without having to close the panel.
    void ExtraSettingsAPI_SettingsOpen()
    {
        LoadSettings();
    }

    void ExtraSettingsAPI_Unload()
    {
        ExtraSettingsAPI_Loaded = false;
        zoomKeybindName = null;
        RestoreOriginalFOV();
    }

    void ExtraSettingsAPI_SettingsClose()
    {
        LoadSettings();
        if (!isZooming)
            currentZoomFactor = TargetZoomFactor();
    }

    // -------------------------------------------------------------------------
    // ExtraSettingsAPI stub declarations
    // These method bodies are replaced by the API at runtime.
    // -------------------------------------------------------------------------
    [MethodImpl(MethodImplOptions.NoInlining)]
    public float ExtraSettingsAPI_GetSliderValue(string settingName) => 0f;

    [MethodImpl(MethodImplOptions.NoInlining)]
    public bool ExtraSettingsAPI_GetCheckboxState(string settingName) => false;

    [MethodImpl(MethodImplOptions.NoInlining)]
    public string ExtraSettingsAPI_GetKeybindName(string settingName) => null;
}
