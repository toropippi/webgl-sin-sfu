using System;
using System.Text;
using System.Globalization;
using System.Collections.Generic;
using UnityEngine;

public class WebGLSinProbe : MonoBehaviour
{
    // 入力は 9 個、ビットプレーンの横幅は 32bit
    const int N = 9;
    const int BIT_W = 32;
    const int BIT_H = N;

    RenderTexture rt;     // 32x9, ARGB32 (UNorm)
    Texture2D readTex;    // 32x9, RGBA32
    [SerializeField] Shader shader;
    Material mat;

    float[] inputs = new float[N];
    uint[] inputHex = new uint[N];

    // 画面描画
    Vector2 scroll = Vector2.zero;
    GUIStyle labelStyle = null, headerStyle = null, cellStyle = null;

    // 行データ
    struct Row
    {
        public string idx, inDec, inHex, sinDec, sinHex, vendor, correctHex, ulp;
    }
    Row[] rows;

    // === 参照（ベンダ別） ===
    class RefRow
    {
        public string arg_hex;
        public string sin_hex_amd;
        public string sin_hex_intel;
        public string sin_hex_nvidia;
    }
    Dictionary<string, RefRow> refsByHex;

    // === 正解（高精度） ===
    Dictionary<string, string> correctHexByArgHex;

    static float UIntToFloat(uint bits) => BitConverter.ToSingle(BitConverter.GetBytes(bits), 0);
    static uint FloatToUInt(float v) => BitConverter.ToUInt32(BitConverter.GetBytes(v), 0);

    // ULP 距離（IEEE754 並べ替え）
    static int UlpDistance(uint a, uint b)
    {
        int ia = (int)a;
        int ib = (int)b;
        // 並べ替え：負は大きく、正は小さくならないよう単調化
        ia = (ia < 0) ? int.MinValue - ia : ia + int.MinValue;
        ib = (ib < 0) ? int.MinValue - ib : ib + int.MinValue;
        long d = (long)ia - ib;
        if (d < 0) d = -d;
        if (d > int.MaxValue) return int.MaxValue;
        return (int)d;
    }

    string overallVendorGuess = "";
    string runtimeInfoLine = "";

    // ====== 参照テーブル（ベンダ別：最新版 table2） ======
    void BuildReference()
    {
        var list = new List<RefRow>
        {
            new RefRow{ arg_hex="0x00000100", sin_hex_amd="0x00000102", sin_hex_intel="0x00000000", sin_hex_nvidia="0x00000000"},
            new RefRow{ arg_hex="0x007FFFFF", sin_hex_amd="0x007FFFFA", sin_hex_intel="0x00000000", sin_hex_nvidia="0x00000000"},
            new RefRow{ arg_hex="0x807FFFFF", sin_hex_amd="0x807FFFFA", sin_hex_intel="0x80000000", sin_hex_nvidia="0x80000000"},
            new RefRow{ arg_hex="0x80000000", sin_hex_amd="0x80000000", sin_hex_intel="0x80000000", sin_hex_nvidia="0x80000000"},
            new RefRow{ arg_hex="0x00000000", sin_hex_amd="0x00000000", sin_hex_intel="0x00000000", sin_hex_nvidia="0x00000000"},
            new RefRow{ arg_hex="0x3900F990", sin_hex_amd="0x3900F98C", sin_hex_intel="0x3900E07E", sin_hex_nvidia="0x3900CF88"},
            new RefRow{ arg_hex="0x40490FF1", sin_hex_amd="0xB6AFEDE4", sin_hex_intel="0xB6B400B4", sin_hex_nvidia="0xB6A35EA0"},
            new RefRow{ arg_hex="0xC015C28F", sin_hex_amd="0xBF37ED50", sin_hex_intel="0xBF37ED50", sin_hex_nvidia="0xBF37ED4F"},
            new RefRow{ arg_hex="0x47DFA900", sin_hex_amd="0x3E47C5C3", sin_hex_intel="0x3F68C7B7", sin_hex_nvidia="0x3E5414F6"},
        };
        refsByHex = new Dictionary<string, RefRow>(StringComparer.OrdinalIgnoreCase);
        foreach (var r in list) refsByHex[r.arg_hex] = r;

        // ====== 正解（高精度） hex ======
        correctHexByArgHex = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "0x00000100", "0x00000100" },
            { "0x007FFFFF", "0x007FFFFF" },
            { "0x807FFFFF", "0x807FFFFF" },
            { "0x80000000", "0x80000000" }, // -0.0
            { "0x00000000", "0x00000000" },
            { "0x3900F990", "0x3900F990" },
            { "0x40490FF1", "0xB6B2EEF5" }, // -5.33e-06
            { "0xC015C28F", "0xBF37ED50" }, // -0.7184648514
            { "0x47DFA900", "0x3E453A34" }, // 0.1926048398
        };
    }

    string DetectVendor(string inHex, string outHex)
    {
        if (refsByHex != null && refsByHex.TryGetValue(inHex, out var r))
        {
            if (string.Equals(outHex, r.sin_hex_nvidia, StringComparison.OrdinalIgnoreCase)) return "NVIDIA";
            if (string.Equals(outHex, r.sin_hex_intel, StringComparison.OrdinalIgnoreCase)) return "Intel";
            if (string.Equals(outHex, r.sin_hex_amd, StringComparison.OrdinalIgnoreCase)) return "AMD";
        }
        return ""; // 不明なら空欄
    }

    void Awake()
    {
        // 実行環境の基本情報
        runtimeInfoLine =
            $"Platform={Application.platform}, API={SystemInfo.graphicsDeviceType}, " +
            $"Version=\"{SystemInfo.graphicsDeviceVersion}\", Name=\"{SystemInfo.graphicsDeviceName}\", Vendor=\"{SystemInfo.graphicsDeviceVendor}\"";

        BuildReference();

        // 入力 9 個（順序固定）
        uint[] bitPatterns = { 0x00000100u, 0x007FFFFFu, 0x807FFFFFu, 0x80000000u, 0x00000000u };
        float[] decimals = { 0.0001230000052601099f, 3.1415979862213135f, -2.3399999141693115f, 114514.0f };

        int idx = 0;
        foreach (var b in bitPatterns) { var f = UIntToFloat(b); inputs[idx] = f; inputHex[idx] = b; idx++; }
        foreach (var d in decimals) { inputs[idx] = d; inputHex[idx] = FloatToUInt(d); idx++; }

        // マテリアル／RT
        mat = new Material(shader);

        rt = new RenderTexture(BIT_W, BIT_H, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };
        rt.Create();

        readTex = new Texture2D(BIT_W, BIT_H, TextureFormat.RGBA32, false, true);
        readTex.filterMode = FilterMode.Point;

        // uniform
        mat.SetFloatArray("_Inputs", inputs);
        mat.SetInt("_RTWidth", BIT_W);
        mat.SetInt("_RTHeight", BIT_H);

        // 実行 & 読み戻し
        Graphics.Blit(null, rt, mat, 0);
        var prev = RenderTexture.active;
        RenderTexture.active = rt;
        readTex.ReadPixels(new Rect(0, 0, BIT_W, BIT_H), 0, 0);
        readTex.Apply(false);
        RenderTexture.active = prev;

        // 復元＆行データ作成
        rows = new Row[N];
        var px = readTex.GetPixels(); // len = 32*9

        for (int row = 0; row < N; ++row)
        {
            uint bits = 0u;
            int y = row;
            for (int x = 0; x < 32; ++x)
            {
                float r = px[y * BIT_W + x].r;
                if (r > 0.5f) bits |= (1u << x);
            }

            float sinVal = UIntToFloat(bits);
            string inDec = inputs[row].ToString("G9", CultureInfo.InvariantCulture);
            string inHexS = "0x" + inputHex[row].ToString("X8");
            string outDec = sinVal.ToString("G9", CultureInfo.InvariantCulture);
            string outHex = "0x" + bits.ToString("X8");
            string vendor = DetectVendor(inHexS, outHex);

            // 正解＆ULP
            string correctHex = correctHexByArgHex.TryGetValue(inHexS, out var ch) ? ch : "";
            string ulpStr = "";
            if (!string.IsNullOrEmpty(correctHex))
            {
                uint correctBits = Convert.ToUInt32(correctHex.Substring(2), 16);
                int ulp = UlpDistance(bits, correctBits);
                ulpStr = ulp.ToString(CultureInfo.InvariantCulture);
            }

            rows[row] = new Row
            {
                idx = row.ToString(),
                inDec = inDec,
                inHex = inHexS,
                sinDec = outDec,
                sinHex = outHex,
                vendor = vendor,
                correctHex = correctHex,
                ulp = ulpStr
            };
        }

        // 総合推定
        {
            string v = null; bool consistent = true; int counted = 0;
            foreach (var r in rows)
            {
                if (refsByHex.TryGetValue(r.inHex, out _))
                {
                    if (!string.IsNullOrEmpty(r.vendor))
                    {
                        if (v == null) v = r.vendor;
                        else if (v != r.vendor) { consistent = false; break; }
                        counted++;
                    }
                    else { consistent = false; break; }
                }
            }
            overallVendorGuess = (consistent && counted > 0) ? v : "";
        }

        // Console 出力
        var sb = new StringBuilder();
        sb.AppendLine("WebGL sin() bitplane probe (32x9, 1bit/px, ARGB32)");
        sb.AppendLine(runtimeInfoLine);
        sb.AppendLine(string.Format("{0,-3} | {1,14} | {2,12} | {3,14} | {4,12} | {5,12} | {6,10}",
            "idx", "in_dec", "in_hex", "sin_dec", "sin_hex", "correct_hex", "ulp_error"));
        sb.AppendLine(new string('-', 110));
        foreach (var r in rows)
            sb.AppendFormat("{0,-3} | {1,14} | {2,12} | {3,14} | {4,12} | {5,12} | {6,10}  {7}\n",
                r.idx, r.inDec, r.inHex, r.sinDec, r.sinHex, r.correctHex, r.ulp,
                string.IsNullOrEmpty(r.vendor) ? "" : ("[" + r.vendor + "]"));
        sb.AppendLine(new string('-', 110));
        sb.AppendLine("Overall vendor guess: " + (string.IsNullOrEmpty(overallVendorGuess) ? "(unknown)" : overallVendorGuess));
        Debug.Log(sb.ToString());
    }

    void OnGUI()
    {
        if (labelStyle == null)
        {
            labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 16, alignment = TextAnchor.MiddleLeft };
            headerStyle = new GUIStyle(labelStyle) { fontStyle = FontStyle.Bold };
            cellStyle = new GUIStyle(labelStyle) { clipping = TextClipping.Clip };
        }

        // ビットプレーン・プレビュー
        if (rt != null) GUI.DrawTexture(new Rect(10, 10, 256, 72), rt, ScaleMode.StretchToFill, false);

        float pad = 10f;
        var rect = new Rect(pad, 100f, Screen.width - pad * 2, Screen.height - 110f);
        GUILayout.BeginArea(rect);

        GUILayout.Label("WebGL sin() bitplane probe (32x9, 1bit/px, ARGB32)", headerStyle);
        GUILayout.Label(runtimeInfoLine, labelStyle);
        GUILayout.Space(6);

        // 列幅
        float wIdx = 40f, wInDec = 200f, wInHex = 140f, wSinDec = 200f, wSinHex = 150f, wCorrect = 150f, wUlp = 90f, wMatch = 120f;
        float rowH = 22f;

        using (new GUILayout.HorizontalScope())
        {
            GUILayout.Label("idx", headerStyle, GUILayout.Width(wIdx), GUILayout.Height(rowH));
            GUILayout.Label("in_dec", headerStyle, GUILayout.Width(wInDec), GUILayout.Height(rowH));
            GUILayout.Label("in_hex", headerStyle, GUILayout.Width(wInHex), GUILayout.Height(rowH));
            GUILayout.Label("sin_dec", headerStyle, GUILayout.Width(wSinDec), GUILayout.Height(rowH));
            GUILayout.Label("sin_hex", headerStyle, GUILayout.Width(wSinHex), GUILayout.Height(rowH));
            GUILayout.Label("correct_hex", headerStyle, GUILayout.Width(wCorrect), GUILayout.Height(rowH));
            GUILayout.Label("ULP error", headerStyle, GUILayout.Width(wUlp), GUILayout.Height(rowH));
            GUILayout.Label("bitwise match", headerStyle, GUILayout.Width(wMatch), GUILayout.Height(rowH));
        }
        GUILayout.Box("", GUILayout.ExpandWidth(true), GUILayout.Height(1));

        scroll = GUILayout.BeginScrollView(scroll);
        if (rows != null)
        {
            foreach (var r in rows)
            {
                using (new GUILayout.HorizontalScope())
                {
                    GUILayout.Label(r.idx, cellStyle, GUILayout.Width(wIdx), GUILayout.Height(rowH));
                    GUILayout.Label(r.inDec, cellStyle, GUILayout.Width(wInDec), GUILayout.Height(rowH));
                    GUILayout.Label(r.inHex, cellStyle, GUILayout.Width(wInHex), GUILayout.Height(rowH));
                    GUILayout.Label(r.sinDec, cellStyle, GUILayout.Width(wSinDec), GUILayout.Height(rowH));
                    GUILayout.Label(r.sinHex, cellStyle, GUILayout.Width(wSinHex), GUILayout.Height(rowH));
                    GUILayout.Label(r.correctHex, cellStyle, GUILayout.Width(wCorrect), GUILayout.Height(rowH));
                    GUILayout.Label(string.IsNullOrEmpty(r.ulp) ? "" : r.ulp,
                                    cellStyle, GUILayout.Width(wUlp), GUILayout.Height(rowH));
                    GUILayout.Label(string.IsNullOrEmpty(r.vendor) ? "" : (r.vendor),
                                    cellStyle, GUILayout.Width(wMatch), GUILayout.Height(rowH));
                }
            }
        }
        GUILayout.EndScrollView();

        GUILayout.Space(6);
        GUILayout.Label("Overall vendor guess: " + (string.IsNullOrEmpty(overallVendorGuess) ? "(unknown)" : overallVendorGuess), headerStyle);

        GUILayout.EndArea();
    }
}
