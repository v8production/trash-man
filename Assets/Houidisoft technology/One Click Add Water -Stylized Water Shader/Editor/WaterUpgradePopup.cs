using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class WaterUpgradePopupLoader
{
    static WaterUpgradePopupLoader()
    {
        EditorApplication.delayCall += () => WaterUpgradePopup.Open();
    }
}

public class WaterUpgradePopup : EditorWindow
{
    const string STORE_URL = "https://assetstore.unity.com/packages/slug/essw-easy-setup-stylized-water-2-0-317597";

    // Colors
    static readonly Color BG          = new Color(0.027f, 0.051f, 0.090f);
    static readonly Color HERO_TOP    = new Color(0.000f, 0.094f, 0.157f);
    static readonly Color HERO_BOT    = new Color(0.000f, 0.180f, 0.220f);
    static readonly Color ACCENT      = new Color(0.000f, 0.722f, 0.847f);
    static readonly Color ACCENT_DIM  = new Color(0.000f, 0.722f, 0.847f, 0.18f);
    static readonly Color WARN        = new Color(1.000f, 0.800f, 0.267f);
    static readonly Color PANEL       = new Color(1f, 1f, 1f, 0.025f);
    static readonly Color BORDER      = new Color(1f, 1f, 1f, 0.06f);
    static readonly Color TEXT_DIM    = new Color(1f, 1f, 1f, 0.35f);
    static readonly Color TEXT_GHOST  = new Color(1f, 1f, 1f, 0.18f);

    GUIStyle _titleStyle, _heroTitleStyle, _badgeStyle, _warnBadgeStyle;
    GUIStyle _featNameStyle, _featDescStyle, _planLblStyle, _planItemStyle;
    GUIStyle _priceStyle, _priceOldStyle, _priceNoteStyle;
    GUIStyle _btnBuyStyle, _btnSkipStyle, _ratingStyle;
    bool _stylesBuilt;

    // ── Entry point ─────────────────────────────────────────────────────────
    public static void Open()
    {
        var w = GetWindow<WaterUpgradePopup>(true, "Easy Setup Stylized Water — Upgrade to Pro", true);
        w.minSize = new Vector2(480, 530);
        w.maxSize = new Vector2(480, 530);
        w.ShowUtility();
    }

    // ── Style builder ────────────────────────────────────────────────────────
    void BuildStyles()
    {
        if (_stylesBuilt) return;
        _stylesBuilt = true;

        _heroTitleStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 22, fontStyle = FontStyle.Bold,
            alignment = TextAnchor.LowerLeft,
            wordWrap = true,
            normal = { textColor = Color.white }
        };

        _badgeStyle = new GUIStyle(EditorStyles.miniLabel)
        {
            fontSize = 9, fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = ACCENT }
        };

        _warnBadgeStyle = new GUIStyle(_badgeStyle)
        {
            normal = { textColor = WARN }
        };

        _featNameStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 11, fontStyle = FontStyle.Bold,
            normal = { textColor = Color.white }
        };

        _featDescStyle = new GUIStyle(EditorStyles.miniLabel)
        {
            fontSize = 10, wordWrap = true,
            normal = { textColor = TEXT_DIM }
        };

        _planLblStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 9, fontStyle = FontStyle.Bold,
            normal = { textColor = TEXT_GHOST }
        };

        _planItemStyle = new GUIStyle(EditorStyles.miniLabel)
        {
            fontSize = 10,
            normal = { textColor = TEXT_DIM }
        };

        _priceStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 30, fontStyle = FontStyle.Bold,
            normal = { textColor = Color.white }
        };

        _priceOldStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 14,
            normal = { textColor = TEXT_GHOST }
        };

        _priceNoteStyle = new GUIStyle(EditorStyles.miniLabel)
        {
            fontSize = 9,
            normal = { textColor = TEXT_GHOST }
        };

        _btnBuyStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 12, fontStyle = FontStyle.Bold,
            normal   = { textColor = Color.white, background = MakeTex(2, 2, new Color(0.00f, 0.72f, 0.85f)) },
            hover    = { textColor = Color.white, background = MakeTex(2, 2, new Color(0.00f, 0.81f, 0.96f)) },
            active   = { textColor = Color.white, background = MakeTex(2, 2, new Color(0.00f, 0.60f, 0.72f)) },
            border   = new RectOffset(4,4,4,4),
            padding  = new RectOffset(14,14,10,10)
        };

        _btnSkipStyle = new GUIStyle(EditorStyles.miniLabel)
        {
            fontSize = 9, alignment = TextAnchor.MiddleRight,
            normal = { textColor = TEXT_GHOST }
        };

        _ratingStyle = new GUIStyle(EditorStyles.miniLabel)
        {
            fontSize = 9,
            normal = { textColor = TEXT_GHOST }
        };
    }

    // ── Main GUI ─────────────────────────────────────────────────────────────
    void OnGUI()
    {
        BuildStyles();

        // Full background
        EditorGUI.DrawRect(new Rect(0, 0, position.width, position.height), BG);

        float W = position.width;
        float y = 0;

        // ── Hero ──────────────────────────────────────────────────────────
        float heroH = 160f;
        DrawVertGradient(new Rect(0, y, W, heroH), HERO_TOP, HERO_BOT);

        // Badges
        float badgeY = y + 14;
        DrawBadge(new Rect(14, badgeY, 130, 20), "FREE VERSION ACTIVE", ACCENT);
        DrawBadge(new Rect(150, badgeY, 130, 20), "UPGRADE AVAILABLE", WARN);

        // Hero title
        GUI.Label(new Rect(14, y + heroH - 70, W - 28, 60),
            "Your Water Deserves More.", _heroTitleStyle);

        y += heroH + 2;

        // ── Body ──────────────────────────────────────────────────────────
        float pad = 16f;
        float bodyW = W - pad * 2;

        y += 12;

        // Features 2x2
        float featW = (bodyW - 8) / 2f;
        float featH = 52f;

        DrawFeature(new Rect(pad, y, featW, featH),           "PBR REFLECTIONS",  "Physically accurate surface");
        DrawFeature(new Rect(pad + featW + 8, y, featW, featH), "FLOW & FOAM",    "Animated direction & edge foam");
        y += featH + 6;
        DrawFeature(new Rect(pad, y, featW, featH),           "FULL INSPECTOR",   "Every param exposed in editor");
        DrawFeature(new Rect(pad + featW + 8, y, featW, featH), "MOBILE READY",   "Optimized for all platforms");
        y += featH + 14;

        // Separator
        DrawSeparator(new Rect(pad, y, bodyW, 1), "FREE VS PRO");
        y += 16;

        // Compare columns
        float colW = (bodyW - 8) / 2f;
        float colH = 88f;
        DrawPlanFree(new Rect(pad, y, colW, colH));
        DrawPlanPro(new Rect(pad + colW + 8, y, colW, colH));
        y += colH + 14;

        // Purchase bar
        float purchaseH = 66f;
        DrawPurchaseBar(new Rect(pad, y, bodyW, purchaseH));
        y += purchaseH + 10;

        // Footer
        GUI.Label(new Rect(pad, y, 260, 18), "★★★★★  Houidisoft Technology · Unity Asset Store", _ratingStyle);
        if (GUI.Button(new Rect(W - pad - 80, y, 80, 16), "Maybe later", _btnSkipStyle))
            Close();
    }

    // ── Draw helpers ─────────────────────────────────────────────────────────
    void DrawVertGradient(Rect r, Color top, Color bot)
    {
        int steps = 32;
        float h = r.height / steps;
        for (int i = 0; i < steps; i++)
        {
            float t = i / (float)(steps - 1);
            EditorGUI.DrawRect(new Rect(r.x, r.y + i * h, r.width, h + 1), Color.Lerp(top, bot, t));
        }
    }

    void DrawBadge(Rect r, string label, Color col)
    {
        var borderCol = new Color(col.r, col.g, col.b, 0.45f);
        var bgCol     = new Color(0, 0, 0, 0.5f);
        EditorGUI.DrawRect(r, bgCol);
        // border lines
        EditorGUI.DrawRect(new Rect(r.x, r.y, r.width, 1), borderCol);
        EditorGUI.DrawRect(new Rect(r.x, r.yMax - 1, r.width, 1), borderCol);
        EditorGUI.DrawRect(new Rect(r.x, r.y, 1, r.height), borderCol);
        EditorGUI.DrawRect(new Rect(r.xMax - 1, r.y, 1, r.height), borderCol);

        var s = new GUIStyle(_badgeStyle) { normal = { textColor = col } };
        GUI.Label(r, label, s);
    }

    void DrawFeature(Rect r, string name, string desc)
    {
        EditorGUI.DrawRect(r, PANEL);
        EditorGUI.DrawRect(new Rect(r.x, r.y, 2, r.height), ACCENT); // left accent bar
        // border
        EditorGUI.DrawRect(new Rect(r.x, r.y, r.width, 1), BORDER);
        EditorGUI.DrawRect(new Rect(r.x, r.yMax - 1, r.width, 1), BORDER);
        EditorGUI.DrawRect(new Rect(r.xMax - 1, r.y, 1, r.height), BORDER);

        GUI.Label(new Rect(r.x + 10, r.y + 8,  r.width - 14, 18), name, _featNameStyle);
        GUI.Label(new Rect(r.x + 10, r.y + 26, r.width - 14, 22), desc, _featDescStyle);
    }

    void DrawSeparator(Rect r, string label)
    {
        float lw = (r.width - 90) / 2f;
        EditorGUI.DrawRect(new Rect(r.x, r.y, lw, 1), BORDER);
        EditorGUI.DrawRect(new Rect(r.x + lw + 90, r.y, lw, 1), BORDER);
        GUI.Label(new Rect(r.x + lw, r.y - 6, 90, 14), label, new GUIStyle(_planLblStyle) { alignment = TextAnchor.MiddleCenter });
    }

    void DrawPlanFree(Rect r)
    {
        EditorGUI.DrawRect(r, PANEL);
        DrawBorderRect(r, BORDER);

        var lbl = new GUIStyle(_planLblStyle) { normal = { textColor = TEXT_GHOST } };
        GUI.Label(new Rect(r.x + 10, r.y + 8, r.width - 14, 14), "CURRENT", lbl);

        string[] items = { "✓  Basic shader", "✗  No reflections", "✗  No foam / flow", "✗  No depth color" };
        for (int i = 0; i < items.Length; i++)
        {
            var s = new GUIStyle(_planItemStyle) { normal = { textColor = i == 0 ? TEXT_DIM : TEXT_GHOST } };
            GUI.Label(new Rect(r.x + 10, r.y + 24 + i * 15, r.width - 14, 15), items[i], s);
        }
    }

    void DrawPlanPro(Rect r)
    {
        EditorGUI.DrawRect(r, ACCENT_DIM);
        DrawBorderRect(r, new Color(ACCENT.r, ACCENT.g, ACCENT.b, 0.25f));

        // PRO pill
        var pillR = new Rect(r.xMax - 46, r.y, 36, 14);
        EditorGUI.DrawRect(pillR, ACCENT);
        GUI.Label(pillR, "PRO", new GUIStyle(_badgeStyle) { normal = { textColor = Color.black }, fontSize = 8 });

        var lbl = new GUIStyle(_planLblStyle) { normal = { textColor = ACCENT } };
        GUI.Label(new Rect(r.x + 10, r.y + 8, r.width - 60, 14), "UPGRADE", lbl);

        string[] items = { "✓  Full PBR shader", "✓  Realtime reflections", "✓  Foam, flow & depth", "✓  Unlimited presets" };
        var itemStyle = new GUIStyle(_planItemStyle) { normal = { textColor = new Color(1f,1f,1f,0.82f) } };
        for (int i = 0; i < items.Length; i++)
            GUI.Label(new Rect(r.x + 10, r.y + 24 + i * 15, r.width - 14, 15), items[i], itemStyle);
    }

    void DrawPurchaseBar(Rect r)
    {
        EditorGUI.DrawRect(r, new Color(0,0,0,0.3f));
        DrawBorderRect(r, BORDER);

        // Price
        GUI.Label(new Rect(r.x + 12, r.y + 8,  60, 38), "$13", _priceStyle);
        GUI.Label(new Rect(r.x + 70, r.y + 16, 36, 20), "$20", _priceOldStyle);
        GUI.Label(new Rect(r.x + 12, r.y + 46, 200, 14), "ONE-TIME  ·  LIFETIME UPDATES  ·  FULL SOURCE", _priceNoteStyle);

        // CTA button
        if (GUI.Button(new Rect(r.xMax - 156, r.y + 14, 144, 38), "Get on Asset Store →", _btnBuyStyle))
        {
            Application.OpenURL(STORE_URL);
            Close();
        }
    }

    void DrawBorderRect(Rect r, Color col)
    {
        EditorGUI.DrawRect(new Rect(r.x, r.y, r.width, 1), col);
        EditorGUI.DrawRect(new Rect(r.x, r.yMax - 1, r.width, 1), col);
        EditorGUI.DrawRect(new Rect(r.x, r.y, 1, r.height), col);
        EditorGUI.DrawRect(new Rect(r.xMax - 1, r.y, 1, r.height), col);
    }

    static Texture2D MakeTex(int w, int h, Color col)
    {
        var t = new Texture2D(w, h);
        var px = new Color[w * h];
        for (int i = 0; i < px.Length; i++) px[i] = col;
        t.SetPixels(px);
        t.Apply();
        return t;
    }
}
