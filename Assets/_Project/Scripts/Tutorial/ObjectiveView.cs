using Project.Audio;
using System.Collections.Generic;
using Project.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Project.Tutorial
{
    public class ObjectiveView : MonoBehaviour
    {
        public class Row
        {
            internal RectTransform rect;
            internal Image band;
            internal Image pip;
            internal TextMeshProUGUI text;
            internal string title;
            internal string keyHint;
            internal int target;
            internal int progress;
            internal bool complete;
            internal float fade;
            internal float lifeAfterComplete;
            internal float y;
            internal float pulse;

            public bool IsComplete => complete;
        }

        public static ObjectiveView Instance { get; private set; }

        [SerializeField] private TMP_FontAsset font;
        [SerializeField] private Shader ditherShader;
        [SerializeField] private Color bandColor = new Color(0.02f, 0.03f, 0.06f, 0.72f);
        [SerializeField] private Color labelColor = new Color(0.55f, 0.82f, 1f);
        [SerializeField] private Color keyColor = new Color(1f, 0.82f, 0.45f);
        [SerializeField] private Color doneColor = new Color(0.55f, 1f, 0.6f);
        [SerializeField] private Vector2 margin = new Vector2(48f, 56f);
        [SerializeField] private float rowWidth = 460f;
        [SerializeField] private float spacing = 10f;
        [SerializeField] private float textSize = 30f;
        [SerializeField] private float lingerWhenDone = 1.8f;

        private readonly List<Row> rows = new List<Row>();
        private RectTransform stack;

        public static ObjectiveView Resolve()
        {
            if (Instance != null) return Instance;

            Instance = FindFirstObjectByType<ObjectiveView>();
            if (Instance == null) Instance = new GameObject("ObjectiveView").AddComponent<ObjectiveView>();
            return Instance;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }

            Instance = this;
            Canvas canvas = RetroUi.CreateOverlayCanvas(transform, "ObjectiveCanvas", 55);
            stack = RetroUi.CreateRect(canvas.transform, "Objectives");
            RetroUi.Anchor(stack, Vector2.one, Vector2.one, -margin, new Vector2(rowWidth, 0f));
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public Row Add(string title, string keyHint, int target)
        {
            Row row = new Row
            {
                title = title,
                keyHint = keyHint,
                target = Mathf.Max(1, target),
                y = rows.Count > 0 ? rows[rows.Count - 1].y : 0f
            };

            row.rect = RetroUi.CreateRect(stack, "Objective_" + title);
            RetroUi.Anchor(row.rect, Vector2.one, Vector2.one, Vector2.zero, new Vector2(rowWidth, 80f));

            row.band = RetroUi.CreateDitherImage(row.rect, "Band", ditherShader, bandColor, 0.18f, 0f);
            RetroUi.Stretch(row.band.rectTransform);

            row.pip = RetroUi.CreateDitherImage(row.rect, "Pip", ditherShader, labelColor, 0f, 0f, 0f);
            RetroUi.Anchor(row.pip.rectTransform, new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-10f, 0f), new Vector2(6f, 44f));

            row.text = RetroUi.CreateText(row.rect, "Text", font != null ? font : RetroUi.DefaultFont(), textSize, TextAlignmentOptions.TopRight);
            row.text.textWrappingMode = TextWrappingModes.NoWrap;
            RetroUi.ApplyHardShadow(row.text, new Color(0f, 0f, 0f, 0.9f), 0.6f);
            RetroUi.Stretch(row.text.rectTransform);
            row.text.rectTransform.offsetMin = new Vector2(18f, 10f);
            row.text.rectTransform.offsetMax = new Vector2(-28f, -10f);

            rows.Add(row);
            Refresh(row);
            GameAudio.Play(SfxId.ObjectiveStart);
            return row;
        }

        public void SetProgress(Row row, int progress)
        {
            if (row == null || row.complete) return;
            if (progress == row.progress) return;

            row.progress = Mathf.Clamp(progress, 0, row.target);
            row.pulse = 1f;
            Refresh(row);
        }

        public void Complete(Row row)
        {
            if (row == null || row.complete) return;

            row.progress = row.target;
            row.complete = true;
            row.lifeAfterComplete = lingerWhenDone;
            row.pulse = 1f;
            row.pip.color = doneColor;
            Refresh(row);
            GameAudio.Play(SfxId.ObjectiveComplete);
        }

        public void Remove(Row row)
        {
            if (row == null) return;
            row.complete = true;
            row.lifeAfterComplete = 0f;
        }

        private void Refresh(Row row)
        {
            string label = row.complete
                ? $"<color={RetroUi.Hex(doneColor)}>COMPLETE</color>"
                : $"<color={RetroUi.Hex(labelColor)}>OBJECTIVE</color>";

            string title = row.complete ? $"<s>{row.title}</s>" : row.title;
            string key = string.IsNullOrEmpty(row.keyHint) ? string.Empty : $"  <color={RetroUi.Hex(keyColor)}>[{row.keyHint}]</color>";
            string count = row.target > 1 ? $"  <color=#9AA6BC>{row.progress}/{row.target}</color>" : string.Empty;

            row.text.text = $"<size=62%><cspace=0.35em>{label}</cspace></size>\n{title}{key}{count}";

            float height = row.text.GetPreferredValues(row.text.text, rowWidth, 0f).y + 22f;
            row.rect.sizeDelta = new Vector2(rowWidth, height);
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            float cursor = 0f;

            for (int i = rows.Count - 1; i >= 0; i--)
            {
                Row row = rows[i];
                if (!row.complete) continue;

                row.lifeAfterComplete -= dt;
                if (row.lifeAfterComplete <= 0f && row.fade <= 0.001f)
                {
                    Destroy(row.rect.gameObject);
                    rows.RemoveAt(i);
                }
            }

            foreach (Row row in rows)
            {
                bool leaving = row.complete && row.lifeAfterComplete <= 0f;
                row.fade = Mathf.MoveTowards(row.fade, leaving ? 0f : 1f, dt * 4f);
                row.pulse = Mathf.MoveTowards(row.pulse, 0f, dt * 3f);

                row.y = Mathf.Lerp(row.y, cursor, 1f - Mathf.Exp(-12f * dt));
                float slide = (1f - Mathf.SmoothStep(0f, 1f, row.fade)) * 60f;
                row.rect.anchoredPosition = new Vector2(slide, -row.y);
                row.rect.localScale = Vector3.one * (1f + row.pulse * 0.06f);

                row.text.alpha = row.fade;
                RetroUi.SetFade(row.band, row.fade);
                RetroUi.SetFade(row.pip, row.fade);

                cursor += (row.rect.sizeDelta.y + spacing) * row.fade;
            }
        }
    }
}
