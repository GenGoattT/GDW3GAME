using Project.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Project.Dialogue
{
    public class CaptionView : MonoBehaviour, IDialogueView
    {
        [Header("Look")]
        [SerializeField] private TMP_FontAsset font;
        [SerializeField] private Shader ditherShader;
        [SerializeField] private Color bandColor = new Color(0.02f, 0.03f, 0.06f, 0.78f);
        [SerializeField] private Color fallbackNameColor = new Color(0.55f, 0.82f, 1f);
        [SerializeField] private Color shadowColor = new Color(0f, 0f, 0f, 0.9f);

        [Header("Layout")]
        [SerializeField] private float bottomOffset = 64f;
        [SerializeField] private float bandWidth = 1500f;
        [SerializeField] private float textWidth = 1100f;
        [SerializeField] private float nameSize = 24f;
        [SerializeField] private float bodySize = 36f;
        [SerializeField] private float padding = 20f;

        [Header("Motion")]
        [SerializeField] private float fadeSpeed = 5f;
        [SerializeField] private float nameSlide = 18f;

        private RectTransform root;
        private CanvasGroup group;
        private Image band;
        private Image accent;
        private TextMeshProUGUI nameText;
        private TextMeshProUGUI bodyText;

        private float fade;
        private float targetFade;
        private float revealTime;
        private float revealClock;
        private int totalCharacters;
        private float nameClock;

        public int VisibleCharacters => bodyText != null ? bodyText.maxVisibleCharacters : 0;

        private void Awake()
        {
            Build();
            ApplyFade(0f);
        }

        private void Build()
        {
            Canvas canvas = RetroUi.CreateOverlayCanvas(transform, "CaptionCanvas", 60);

            root = RetroUi.CreateRect(canvas.transform, "Caption");
            RetroUi.Anchor(root, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, bottomOffset), new Vector2(bandWidth, 140f));
            group = root.gameObject.AddComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;

            band = RetroUi.CreateDitherImage(root, "Band", ditherShader, bandColor, 0.24f, 0.12f);
            RetroUi.Stretch(band.rectTransform);

            accent = RetroUi.CreateDitherImage(root, "Accent", ditherShader, fallbackNameColor, 0.4f, 0f, 0f);
            RetroUi.Anchor(accent.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(textWidth * 0.9f, 4f));

            TMP_FontAsset resolved = font != null ? font : RetroUi.DefaultFont();

            nameText = RetroUi.CreateText(root, "Speaker", resolved, nameSize, TextAlignmentOptions.Center);
            nameText.characterSpacing = 18f;
            nameText.fontStyle = FontStyles.UpperCase;
            RetroUi.ApplyHardShadow(nameText, shadowColor, 0.7f);

            bodyText = RetroUi.CreateText(root, "Line", resolved, bodySize, TextAlignmentOptions.Top);
            bodyText.lineSpacing = -8f;
            RetroUi.ApplyHardShadow(bodyText, shadowColor, 0.6f);
        }

        public void Open()
        {
            targetFade = 1f;
        }

        public void Close()
        {
            targetFade = 0f;
        }

        public void ShowLine(DialogueLine line, float reveal)
        {
            DialogueSpeaker speaker = line.speaker;
            Color nameColor = speaker != null ? speaker.NameColor : fallbackNameColor;

            nameText.text = speaker != null ? speaker.DisplayName : string.Empty;
            nameText.color = nameColor;
            accent.color = nameColor;

            bodyText.color = speaker != null ? speaker.TextColor : Color.white;
            bodyText.text = line.text;
            bodyText.ForceMeshUpdate();
            totalCharacters = bodyText.textInfo.characterCount;
            bodyText.maxVisibleCharacters = 0;

            revealTime = Mathf.Max(0.01f, reveal);
            revealClock = 0f;
            nameClock = 0f;

            Layout();
            targetFade = 1f;
        }

        private void Layout()
        {
            float nameHeight = string.IsNullOrEmpty(nameText.text) ? 0f : nameSize + 8f;
            float bodyHeight = bodyText.GetPreferredValues(bodyText.text, textWidth, 0f).y;
            float total = padding + nameHeight + bodyHeight + padding;

            root.sizeDelta = new Vector2(bandWidth, total);
            RetroUi.Anchor(nameText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -padding), new Vector2(textWidth, nameHeight));
            RetroUi.Anchor(bodyText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -padding - nameHeight), new Vector2(textWidth, bodyHeight + 4f));
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;

            if (!Mathf.Approximately(fade, targetFade))
            {
                fade = Mathf.MoveTowards(fade, targetFade, fadeSpeed * dt);
                ApplyFade(fade);
            }

            if (bodyText.maxVisibleCharacters < totalCharacters)
            {
                revealClock += dt;
                bodyText.maxVisibleCharacters = Mathf.CeilToInt(totalCharacters * Mathf.Clamp01(revealClock / revealTime));
            }

            if (nameClock < 1f)
            {
                nameClock = Mathf.Min(1f, nameClock + dt * 6f);
                float k = 1f - (1f - nameClock) * (1f - nameClock);
                Vector2 p = nameText.rectTransform.anchoredPosition;
                p.x = Mathf.Lerp(-nameSlide, 0f, k);
                nameText.rectTransform.anchoredPosition = p;
                nameText.alpha = k;
            }
        }

        private void ApplyFade(float value)
        {
            group.alpha = Mathf.SmoothStep(0f, 1f, value);
            RetroUi.SetFade(band, value);
            RetroUi.SetFade(accent, value);
        }
    }
}
