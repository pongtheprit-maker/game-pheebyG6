using UnityEngine;
using UnityEngine.UI;

/// <summary>Procedurally draws a compact ECG trace without requiring an image asset.</summary>
public class SanityHeartbeatGraphic : MaskableGraphic
{
    static readonly float[] WaveX = { 0f, .18f, .25f, .31f, .42f, .46f, .49f, .525f, .56f, .61f, .70f, .78f, .84f, 1f };
    static readonly float[] WaveY = { .50f, .50f, .57f, .50f, .50f, .40f, .95f, .22f, .56f, .50f, .50f, .60f, .50f, .50f };

    float phase;
    float beatsPerSecond = 1.2f;
    float strength = .7f;
    bool flatline;

    public void SetPulse(float rate, Color tint, float pulseStrength, bool stopped)
    {
        beatsPerSecond = Mathf.Max(0f, rate);
        color = tint;
        strength = Mathf.Clamp01(pulseStrength);
        flatline = stopped;
        SetVerticesDirty();
    }

    void Update()
    {
        if (!flatline)
        {
            phase = Mathf.Repeat(phase + Time.unscaledDeltaTime * beatsPerSecond, 1f);
            SetVerticesDirty();
        }
    }

protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect r = GetPixelAdjustedRect();
        int segments = 180;
        float amplitude = r.height * .80f * Mathf.Lerp(.15f, 1.30f, strength);
        float thickness = Mathf.Lerp(1.5f, 3.0f, strength);

        Vector2 previous = PointAt(0f, r, amplitude);
        for (int i = 1; i <= segments; i++)
        {
            Vector2 next = PointAt((float)i / segments, r, amplitude);
            AddLine(vh, previous, next, thickness, color);
            previous = next;
        }
    }

    Vector2 PointAt(float u, Rect rect, float amplitude)
    {
        float y = .5f;
        if (!flatline)
        {
            float cycle = Mathf.Repeat(u * 3f + phase, 1f);
            int index = 0;
            while (index < WaveX.Length - 2 && cycle > WaveX[index + 1]) index++;
            float t = Mathf.InverseLerp(WaveX[index], WaveX[index + 1], cycle);
            y = Mathf.Lerp(WaveY[index], WaveY[index + 1], t);
        }

        float vertical = flatline ? rect.height * .012f : (y - .5f) * amplitude;
        return new Vector2(Mathf.Lerp(rect.xMin, rect.xMax, u), rect.center.y + vertical);
    }

    static void AddLine(VertexHelper vh, Vector2 a, Vector2 b, float thickness, Color tint)
    {
        Vector2 direction = (b - a).normalized;
        if (direction.sqrMagnitude < .0001f) return;
        Vector2 normal = new Vector2(-direction.y, direction.x) * thickness * .5f;
        int start = vh.currentVertCount;
        Color32 c = tint;
        vh.AddVert(a - normal, c, Vector2.zero);
        vh.AddVert(a + normal, c, Vector2.zero);
        vh.AddVert(b + normal, c, Vector2.zero);
        vh.AddVert(b - normal, c, Vector2.zero);
        vh.AddTriangle(start, start + 1, start + 2);
        vh.AddTriangle(start, start + 2, start + 3);
    }
}
