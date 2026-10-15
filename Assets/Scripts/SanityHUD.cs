using UnityEngine;
using UnityEngine.UI;

/// <summary>Small runtime HUD: sanity meter at lower-left and ECG health trace at upper-right.</summary>
[DisallowMultipleComponent]
public class SanityHUD : MonoBehaviour
{
    Canvas canvas;
    Image sanityFill;
    Text sanityLabel;
    Text sanityValue;
    SanityHeartbeatGraphic heartbeat;
    float healthBaseRate = 1.05f;
    float healthBaseStrength = .20f;
    Color healthTint = new Color(.46f, .82f, .60f, .92f);
    bool healthDead;
    float fatigueAmount;

    Sprite whiteSprite;

    public void Initialize(Camera playerCamera, SanitySystem system)
    {
        if (canvas != null) return;

        Texture2D pixel = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        pixel.name = "SanityHUD_WhitePixel";
        pixel.hideFlags = HideFlags.DontSave;
        pixel.SetPixel(0, 0, Color.white);
        pixel.Apply(false, true);
        whiteSprite = Sprite.Create(pixel, new Rect(0f, 0f, 1f, 1f), new Vector2(.5f, .5f), 100f);
        whiteSprite.name = "SanityHUD_WhiteSprite";
        whiteSprite.hideFlags = HideFlags.DontSave;

        GameObject canvasObject = new GameObject("SanityHUD_Runtime", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = .5f;

        RectTransform root = canvasObject.GetComponent<RectTransform>();
        BuildSanityMeter(root);
        BuildHeartbeat(root);

        SetSanity(system.CurrentSanity, system.maxSanity, system.Distortion);
        SetHealth(system.Health, system.maxHealth);
    }

void BuildSanityMeter(RectTransform canvasRoot)
    {
        RectTransform group = MakeRect("SanityMeter", canvasRoot, new Vector2(268f, 58f),
                                      new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(56f, 28f));
        Image plate = group.gameObject.AddComponent<Image>();
        plate.sprite = whiteSprite;
        plate.color = new Color(.012f, .020f, .025f, .74f);
        plate.raycastTarget = false;

        sanityLabel = MakeText("SanityLabel", group, new Vector2(112f, 22f),
                               new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(10f, -5f),
                               16, TextAnchor.MiddleLeft);
        sanityLabel.text = "SANITY";
        sanityLabel.fontStyle = FontStyle.Bold;
        sanityLabel.color = new Color(.57f, .78f, .78f, .96f);

        sanityValue = MakeText("SanityValue", group, new Vector2(58f, 22f),
                               new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-10f, -5f),
                               16, TextAnchor.MiddleRight);
        sanityValue.fontStyle = FontStyle.Bold;
        sanityValue.color = new Color(.78f, .88f, .86f, .96f);

        RectTransform track = MakeRect("SanityTrack", group, new Vector2(248f, 18f),
                                       new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(10f, 9f));
        Image trackImage = track.gameObject.AddComponent<Image>();
        trackImage.sprite = whiteSprite;
        trackImage.color = new Color(.07f, .11f, .12f, .98f);
        trackImage.raycastTarget = false;

        RectTransform fill = MakeRect("SanityFill", track, new Vector2(248f, 18f),
                                      new Vector2(0f, .5f), new Vector2(0f, .5f), Vector2.zero);
        sanityFill = fill.gameObject.AddComponent<Image>();
        sanityFill.sprite = whiteSprite;
        sanityFill.type = Image.Type.Filled;
        sanityFill.fillMethod = Image.FillMethod.Horizontal;
        sanityFill.fillOrigin = (int)Image.OriginHorizontal.Left;
        sanityFill.fillAmount = 1f;
        sanityFill.color = new Color(.28f, .79f, .77f, 1f);
        sanityFill.raycastTarget = false;
    }

void BuildHeartbeat(RectTransform canvasRoot)
    {
        RectTransform pulse = MakeRect("HealthHeartbeat", canvasRoot, new Vector2(420f, 80f),
                                      new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-28f, -28f));
        if (pulse.GetComponent<CanvasRenderer>() == null)
            pulse.gameObject.AddComponent<CanvasRenderer>();
        heartbeat = pulse.gameObject.AddComponent<SanityHeartbeatGraphic>();
        heartbeat.raycastTarget = false;
    }

    RectTransform MakeRect(string objectName, RectTransform parent, Vector2 size,
                           Vector2 anchor, Vector2 pivot, Vector2 position)
    {
        GameObject go = new GameObject(objectName, typeof(RectTransform));
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        return rect;
    }

    Text MakeText(string objectName, RectTransform parent, Vector2 size,
                  Vector2 anchor, Vector2 pivot, Vector2 position, int fontSize, TextAnchor alignment)
    {
        RectTransform rect = MakeRect(objectName, parent, size, anchor, pivot, position);
        Text text = rect.gameObject.AddComponent<Text>();
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        text.font = font;
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        text.raycastTarget = false;
        return text;
    }

    public void SetSanity(float value, float maximum, float distortion)
    {
        if (sanityFill == null) return;
        float normalized = Mathf.Clamp01(value / Mathf.Max(1f, maximum));
        sanityFill.fillAmount = normalized;
        if (sanityValue != null) sanityValue.text = Mathf.RoundToInt(value).ToString();

        Color calm = new Color(.28f, .79f, .77f, 1f);
        Color strained = new Color(.80f, .61f, .31f, 1f);
        Color unstable = new Color(.68f, .25f, .18f, 1f);
        Color tint = normalized > .5f ? Color.Lerp(calm, strained, (1f - normalized) * 2f)
                                     : Color.Lerp(strained, unstable, 1f - normalized * 2f);
        sanityFill.color = tint;
        if (sanityLabel != null)
            sanityLabel.color = Color.Lerp(new Color(.57f, .78f, .78f, .96f),
                                           new Color(.82f, .42f, .32f, .98f), Mathf.Clamp01(distortion));
    }

public void SetHealth(int health, int maximum)
    {
        if (heartbeat == null) return;

        int max = Mathf.Max(1, maximum);
        int remaining = Mathf.Clamp(health, 0, max);
        int hits = max - remaining;
        healthDead = remaining == 0;

        if (healthDead) healthTint = new Color(1f, .08f, .06f, 1f);
        else if (hits == 0) healthTint = new Color(.46f, .82f, .60f, .92f);
        else if (hits == 1) healthTint = new Color(.18f, 1f, .34f, 1f);
        else if (hits == 2) healthTint = new Color(.60f, 1f, .18f, 1f);
        else if (hits == 3) healthTint = new Color(1f, .74f, .12f, 1f);
        else healthTint = new Color(1f, .28f, .10f, 1f);

        float[] rateByHit = { 1.05f, 1.35f, 1.65f, 1.95f, 2.25f };
        float[] strengthByHit = { .20f, .72f, .84f, .93f, 1f };
        healthBaseRate = rateByHit[Mathf.Clamp(hits, 0, rateByHit.Length - 1)];
        healthBaseStrength = strengthByHit[Mathf.Clamp(hits, 0, strengthByHit.Length - 1)];
        RefreshHeartbeat();
    }

public void SetFatigue(float staminaNormalized)
    {
        fatigueAmount = 1f - Mathf.Clamp01(staminaNormalized);
        RefreshHeartbeat();
    }

    void RefreshHeartbeat()
    {
        if (heartbeat == null) return;
        float rate = healthDead ? 0f : healthBaseRate + fatigueAmount * .85f;
        float strength = healthDead ? 0f : Mathf.Clamp01(healthBaseStrength + fatigueAmount * .20f);
        heartbeat.SetPulse(rate, healthTint, strength, healthDead);
    }


    void OnDestroy()
    {
        if (canvas != null) Destroy(canvas.gameObject);
        if (whiteSprite != null) Destroy(whiteSprite);
    }
}
