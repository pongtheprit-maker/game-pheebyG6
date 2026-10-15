using UnityEngine;

/// <summary>
/// Draws the "[E] ..." interaction prompt, short feedback messages and a small item list.
/// Uses IMGUI with an operating-system font (Tahoma etc.) so Thai text works without a TextMeshPro font asset.
/// Added automatically to the Player by PlayerDoorInteractor if missing.
/// </summary>
public class InteractionPrompt : MonoBehaviour
{
    public static InteractionPrompt Instance { get; private set; }

    [Header("Look")]
    public Color availableColor = new Color(0.78f, 0.6f, 1f, 1f);
    public Color unavailableColor = new Color(0.72f, 0.72f, 0.72f, 1f);
    [Range(0.4f, 0.9f)] public float promptHeightFraction = 0.62f;
    [Range(0.4f, 0.95f)] public float messageHeightFraction = 0.78f;
    public bool showInventoryHud = true;

    string promptText;
    bool promptAvailable;
    string message;
    float messageUntil;

    GUIStyle labelStyle;
    GUIStyle hudStyle;

    void Awake() { Instance = this; }
    void OnEnable() { Instance = this; }

    /// <summary>Shows "[E] text" under the crosshair. available = false dims it (player lacks the item).</summary>
    public void SetPrompt(string text, bool available)
    {
        promptText = text;
        promptAvailable = available;
    }

    public void ClearPrompt() { promptText = null; }

    public void ShowMessage(string text, float seconds = 2.5f)
    {
        message = text;
        messageUntil = Time.unscaledTime + seconds;
    }

    void BuildStyles()
    {
        if (labelStyle != null) return;
        Font font = Font.CreateDynamicFontFromOSFont(new[] { "Tahoma", "Leelawadee UI", "Leelawadee", "Segoe UI", "Arial" }, 32);
        labelStyle = new GUIStyle(GUI.skin.label);
        labelStyle.font = font;
        labelStyle.alignment = TextAnchor.UpperLeft;
        labelStyle.wordWrap = false;
        hudStyle = new GUIStyle(labelStyle);
    }

    static void DrawShadow(Rect r, string text, GUIStyle style, Color color)
    {
        Color old = GUI.color;
        style.normal.textColor = new Color(0f, 0f, 0f, color.a * 0.85f);
        GUI.Label(new Rect(r.x + 2f, r.y + 2f, r.width, r.height), text, style);
        style.normal.textColor = color;
        GUI.Label(r, text, style);
        GUI.color = old;
    }

    void OnGUI()
    {
        BuildStyles();
        float s = Screen.height / 1080f;
        labelStyle.fontSize = Mathf.Max(14, Mathf.RoundToInt(30f * s));
        hudStyle.fontSize = Mathf.Max(12, Mathf.RoundToInt(24f * s));

        // Small item list, top-left, low contrast so it does not break the mood.
        if (showInventoryHud && PlayerInventory.Instance != null && PlayerInventory.Instance.Count > 0)
        {
            float y = 24f * s;
            DrawShadow(new Rect(24f * s, y, 600f * s, 40f * s), "ไอเท็ม", hudStyle, new Color(0.8f, 0.8f, 0.8f, 0.55f));
            foreach (string id in PlayerInventory.Instance.ItemIds)
            {
                y += 30f * s;
                DrawShadow(new Rect(24f * s, y, 600f * s, 40f * s), "• " + PlayerInventory.Instance.NameOf(id), hudStyle, new Color(0.9f, 0.9f, 0.9f, 0.8f));
            }
        }

        // Prompt with a softly pulsing dot at the crosshair.
        if (!string.IsNullOrEmpty(promptText))
        {
            float pulse = 0.65f + 0.35f * Mathf.Sin(Time.unscaledTime * 4f);
            Color c = promptAvailable ? availableColor : unavailableColor;
            c.a = promptAvailable ? pulse : 0.7f;

            float d = 8f * s;
            Color old = GUI.color;
            GUI.color = new Color(c.r, c.g, c.b, c.a);
            GUI.DrawTexture(new Rect(Screen.width * 0.5f - d * 0.5f, Screen.height * 0.5f - d * 0.5f, d, d), Texture2D.whiteTexture);
            GUI.color = old;

            string full = promptAvailable ? "[E]  " + promptText : promptText;
            Vector2 size = labelStyle.CalcSize(new GUIContent(full));
            Rect r = new Rect((Screen.width - size.x) * 0.5f, Screen.height * promptHeightFraction, size.x + 10f, size.y + 6f);
            DrawShadow(r, full, labelStyle, c);
        }

        // Feedback message (picked up, locked, opened ...), fades at the end.
        if (!string.IsNullOrEmpty(message) && Time.unscaledTime < messageUntil)
        {
            float a = Mathf.Clamp01((messageUntil - Time.unscaledTime) / 0.5f);
            Vector2 size = labelStyle.CalcSize(new GUIContent(message));
            Rect r = new Rect((Screen.width - size.x) * 0.5f, Screen.height * messageHeightFraction, size.x + 10f, size.y + 6f);
            DrawShadow(r, message, labelStyle, new Color(0.95f, 0.95f, 0.95f, a));
        }
    }
}
