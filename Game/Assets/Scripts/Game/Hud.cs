using System.Collections.Generic;
using UnityEngine;
using TrustNoWall.Core;

namespace TrustNoWall.Game
{
    /// <summary>
    /// Draws every screen and overlay with IMGUI, scaled from a 960x540 reference canvas up to the
    /// real screen via GUI.matrix (matching the WebGL build's 960x540 default resolution). Reads
    /// state from <see cref="GameFlow"/> only; owns no gameplay logic. Styles, textures and the
    /// mechanic legend layout are cached and only rebuilt when the level actually changes.
    /// </summary>
    public sealed class Hud : MonoBehaviour
    {
        private const float RefWidth = 960f;
        private const float RefHeight = 540f;
        private const float HudBandHeight = RefHeight * CameraFit.HudFraction; // ~64.8

        private static readonly Color ShadowColor = new Color(0f, 0f, 0f, 0.7f);
        private static readonly Color PanelColor = new Color(0.03f, 0.04f, 0.08f, 0.85f);
        private static readonly Color HudBandColor = new Color(0.02f, 0.02f, 0.04f, 0.55f);
        private static readonly Color MutedColor = new Color(0.85f, 0.86f, 0.92f, 1f);
        private static readonly Color TaglineColor = new Color(0.55f, 0.75f, 1f, 0.9f);
        private static readonly Color DeathRed = new Color(1f, 0.35f, 0.35f, 1f);
        private static readonly Color DemoTagColor = new Color(0.75f, 0.2f, 0.95f, 0.85f);

        private static readonly string[] ControlLines =
        {
            "MOVE: WASD / ARROW KEYS",
            "RESTART: R",
            "PAUSE: P / ESC",
            "MUTE: M",
        };

        private GameFlow _flow;
        public GameFlow Flow { set => _flow = value; }

        private Texture2D _pixel;
        private float _virtualWidth;
        private float _offsetX;

        private bool _stylesReady;
        private GUIStyle _titleStyle;
        private GUIStyle _taglineStyle;
        private GUIStyle _smallStyle;
        private GUIStyle _promptStyle;
        private GUIStyle _panelHeaderStyle;
        private GUIStyle _hudMainStyle;
        private GUIStyle _hudSubStyle;
        private GUIStyle _hudRightMainStyle;
        private GUIStyle _hudRightSubStyle;
        private GUIStyle _legendStyle;
        private GUIStyle _introNameStyle;
        private GUIStyle _introTextStyle;
        private GUIStyle _demoStyle;

        private int _legendBuiltForLevel = -1;
        private readonly List<(string name, Color color, float width)> _legend = new List<(string, Color, float)>();

        private void OnGUI()
        {
            if (_flow == null)
            {
                return;
            }

            EnsureResources();

            float scale = Screen.height / RefHeight;
            _virtualWidth = Screen.width / scale;
            _offsetX = (_virtualWidth - RefWidth) / 2f;
            GUI.matrix = Matrix4x4.TRS(new Vector3(_offsetX * scale, 0f, 0f), Quaternion.identity, new Vector3(scale, scale, 1f));

            bool hasRun = _flow.Layout != null;

            if (hasRun && _flow.CurrentState != GameFlow.State.Title)
            {
                DrawHudBand();
            }

            switch (_flow.CurrentState)
            {
                case GameFlow.State.Title:
                    DrawTitleScreen();
                    if (_flow.DemoActive)
                    {
                        DrawDemoTag();
                    }

                    break;
                case GameFlow.State.IntroCard:
                    DrawIntroCard();
                    break;
                case GameFlow.State.Dying:
                    DrawDeathBanner();
                    break;
                case GameFlow.State.LevelComplete:
                    DrawCompleteBanner();
                    break;
                case GameFlow.State.Paused:
                    DrawPausedScreen();
                    break;
            }
        }

        private void DrawHudBand()
        {
            DrawRect(new Rect(-_offsetX, 0f, _virtualWidth, HudBandHeight), HudBandColor);

            LevelLayout layout = _flow.Layout;
            DrawShadowedLabel(new Rect(16f, 6f, 220f, 22f), "LEVEL " + _flow.CurrentLevel, _hudMainStyle, Color.white);
            DrawShadowedLabel(new Rect(16f, 27f, 220f, 18f), layout.N + " x " + layout.N, _hudSubStyle, MutedColor);

            DrawShadowedLabel(new Rect(RefWidth - 226f, 6f, 210f, 22f), "DEATHS " + _flow.LevelDeaths, _hudRightMainStyle, Color.white);
            DrawShadowedLabel(new Rect(RefWidth - 226f, 27f, 210f, 18f), "TIME " + FormatTime(_flow.TotalRunTime), _hudRightSubStyle, MutedColor);

            DrawLegend(layout);

            if (_flow.DemoActive)
            {
                DrawDemoTag();
            }
        }

        private void DrawDemoTag()
        {
            var rect = new Rect(RefWidth / 2f - 32f, 8f, 64f, 22f);
            DrawRect(rect, DemoTagColor);
            DrawShadowedLabel(rect, "DEMO", _demoStyle, Color.white);
        }

        private void DrawLegend(LevelLayout layout)
        {
            if (_legendBuiltForLevel != _flow.CurrentLevel)
            {
                RebuildLegend(layout);
            }

            const float swatch = 12f;
            const float gap = 6f;
            const float itemGap = 20f;

            float totalWidth = 0f;
            foreach (var item in _legend)
            {
                totalWidth += swatch + gap + item.width + itemGap;
            }

            if (_legend.Count > 0)
            {
                totalWidth -= itemGap;
            }

            float x = (RefWidth - totalWidth) / 2f;
            const float y = 42f;

            foreach (var item in _legend)
            {
                DrawRect(new Rect(x, y + 3f, swatch, swatch), item.color);
                DrawShadowedLabel(new Rect(x + swatch + gap, y, item.width + 4f, 20f), item.name, _legendStyle, Color.white);
                x += swatch + gap + item.width + itemGap;
            }
        }

        private void RebuildLegend(LevelLayout layout)
        {
            _legend.Clear();
            foreach (Mechanic m in layout.Mechanics)
            {
                MechanicInfo info = MechanicInfo.Get(m);
                float width = _legendStyle.CalcSize(new GUIContent(info.DisplayName)).x;
                _legend.Add((info.DisplayName, Palette.MechanicColor(m, layout), width));
            }

            _legendBuiltForLevel = _flow.CurrentLevel;
        }

        private void DrawIntroCard()
        {
            LevelLayout layout = _flow.Layout;
            if (layout == null)
            {
                return;
            }

            int count = layout.NewMechanics.Count;
            float panelHeight = 100f + (count * 76f);
            Rect panel = new Rect((RefWidth / 2f) - 320f, (RefHeight - panelHeight) / 2f, 640f, panelHeight);
            DrawRect(panel, PanelColor);

            float y = panel.y + 20f;
            for (int i = 0; i < count; i++)
            {
                MechanicInfo info = MechanicInfo.Get(layout.NewMechanics[i]);
                DrawShadowedLabel(new Rect(panel.x + 30f, y, panel.width - 60f, 30f), "NEW: " + info.DisplayName, _introNameStyle, Readable(Palette.MechanicColor(layout.NewMechanics[i], layout)));
                y += 32f;
                DrawShadowedLabel(new Rect(panel.x + 40f, y, panel.width - 80f, 36f), info.Explanation, _introTextStyle, MutedColor);
                y += 44f;
            }

            float pulse = 0.6f + (0.4f * Mathf.Sin(Time.unscaledTime * 5f));
            DrawShadowedLabel(
                new Rect(panel.x, panel.y + panel.height - 34f, panel.width, 28f),
                "Press SPACE to continue",
                _promptStyle,
                new Color(1f, 1f, 1f, pulse));
        }

        private void DrawDeathBanner()
        {
            Rect panel = new Rect((RefWidth / 2f) - 260f, (RefHeight / 2f) - 66f, 520f, 132f);
            DrawRect(panel, PanelColor);
            DrawShadowedLabel(new Rect(panel.x, panel.y + 18f, panel.width, 40f), "YOU DIED", _panelHeaderStyle, DeathRed);

            string cause = string.IsNullOrEmpty(_flow.DeathCause) ? string.Empty : _flow.DeathCause;
            DrawShadowedLabel(new Rect(panel.x, panel.y + 66f, panel.width, 26f), cause, _smallStyle, Color.white);
            DrawShadowedLabel(new Rect(panel.x, panel.y + 96f, panel.width, 24f), "Restarting the attempt...", _smallStyle, MutedColor);
        }

        private void DrawCompleteBanner()
        {
            Rect panel = new Rect((RefWidth / 2f) - 260f, (RefHeight / 2f) - 50f, 520f, 100f);
            DrawRect(panel, PanelColor);
            DrawShadowedLabel(
                new Rect(panel.x, panel.y + 30f, panel.width, 40f),
                "LEVEL " + _flow.CurrentLevel + " COMPLETE",
                _panelHeaderStyle,
                Palette.Destination);
        }

        private void DrawPausedScreen()
        {
            Rect panel = new Rect((RefWidth / 2f) - 200f, (RefHeight / 2f) - 80f, 400f, 160f);
            DrawRect(panel, PanelColor);
            DrawShadowedLabel(new Rect(panel.x, panel.y + 22f, panel.width, 42f), "PAUSED", _panelHeaderStyle, Color.white);
            DrawShadowedLabel(new Rect(panel.x, panel.y + 78f, panel.width, 24f), "Press P or ESC to resume", _smallStyle, MutedColor);
            DrawShadowedLabel(new Rect(panel.x, panel.y + 108f, panel.width, 24f), "Press M to toggle mute", _smallStyle, MutedColor);
        }

        private void DrawTitleScreen()
        {
            Rect panel = new Rect((RefWidth / 2f) - 340f, 62f, 680f, 400f);
            DrawRect(panel, PanelColor);

            float y = panel.y + 24f;
            DrawShadowedLabel(new Rect(panel.x, y, panel.width, 56f), "TRUST NO WALL", _titleStyle, Color.white);
            y += 62f;

            DrawShadowedLabel(new Rect(panel.x, y, panel.width, 28f), "The maze is lying to you.", _taglineStyle, TaglineColor);
            y += 46f;

            for (int i = 0; i < ControlLines.Length; i++)
            {
                DrawShadowedLabel(new Rect(panel.x, y, panel.width, 22f), ControlLines[i], _smallStyle, Color.white);
                y += 24f;
            }

            y += 18f;
            DrawShadowedLabel(new Rect(panel.x, y, panel.width, 24f), "BEST LEVEL: " + _flow.BestLevel, _smallStyle, MutedColor);
            y += 40f;

            float pulse = 0.6f + (0.4f * Mathf.Sin(Time.unscaledTime * 5f));
            DrawShadowedLabel(
                new Rect(panel.x, y, panel.width, 32f),
                "Press SPACE to start",
                _promptStyle,
                new Color(1f, 1f, 1f, pulse));
        }

        /// <summary>Lifts dark mechanic colors (the chaser's purple) so intro titles stay legible.</summary>
        private static Color Readable(Color c)
        {
            float luminance = (0.299f * c.r) + (0.587f * c.g) + (0.114f * c.b);
            return luminance < 0.45f ? Color.Lerp(c, Color.white, 0.35f) : c;
        }

        private static string FormatTime(float seconds)
        {
            int total = Mathf.Max(0, Mathf.FloorToInt(seconds));
            return string.Format("{0:00}:{1:00}", total / 60, total % 60);
        }

        private void DrawShadowedLabel(Rect rect, string text, GUIStyle style, Color color)
        {
            Color previous = style.normal.textColor;

            style.normal.textColor = ShadowColor;
            GUI.Label(new Rect(rect.x + 1.5f, rect.y + 1.5f, rect.width, rect.height), text, style);

            style.normal.textColor = color;
            GUI.Label(rect, text, style);

            style.normal.textColor = previous;
        }

        private void DrawRect(Rect rect, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, _pixel);
            GUI.color = previous;
        }

        private void EnsureResources()
        {
            if (_pixel == null)
            {
                _pixel = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                _pixel.SetPixel(0, 0, Color.white);
                _pixel.Apply();
            }

            if (_stylesReady)
            {
                return;
            }

            _titleStyle = NewStyle(46, FontStyle.Bold, TextAnchor.UpperCenter);
            _taglineStyle = NewStyle(18, FontStyle.Italic, TextAnchor.UpperCenter);
            _smallStyle = NewStyle(16, FontStyle.Normal, TextAnchor.UpperCenter);
            _promptStyle = NewStyle(24, FontStyle.Bold, TextAnchor.UpperCenter);
            _panelHeaderStyle = NewStyle(34, FontStyle.Bold, TextAnchor.UpperCenter);
            _hudMainStyle = NewStyle(18, FontStyle.Bold, TextAnchor.UpperLeft);
            _hudSubStyle = NewStyle(13, FontStyle.Normal, TextAnchor.UpperLeft);
            _hudRightMainStyle = NewStyle(18, FontStyle.Bold, TextAnchor.UpperRight);
            _hudRightSubStyle = NewStyle(13, FontStyle.Normal, TextAnchor.UpperRight);
            _legendStyle = NewStyle(12, FontStyle.Bold, TextAnchor.UpperLeft);
            _introNameStyle = NewStyle(22, FontStyle.Bold, TextAnchor.UpperCenter);
            _introTextStyle = NewStyle(15, FontStyle.Normal, TextAnchor.UpperCenter);
            _introTextStyle.wordWrap = true;
            _demoStyle = NewStyle(14, FontStyle.Bold, TextAnchor.MiddleCenter);

            _stylesReady = true;
        }

        private static GUIStyle NewStyle(int fontSize, FontStyle fontStyle, TextAnchor alignment)
        {
            GUIStyle style = new GUIStyle(GUI.skin.label)
            {
                fontSize = fontSize,
                fontStyle = fontStyle,
                alignment = alignment,
            };
            style.normal.textColor = Color.white;
            return style;
        }
    }
}
