using UnityEngine;

namespace Project.Dialogue
{
    [CreateAssetMenu(menuName = "Stories End/Dialogue/Speaker", fileName = "Speaker_")]
    public class DialogueSpeaker : ScriptableObject
    {
        [SerializeField] private string displayName = "UNKNOWN";
        [SerializeField] private Color nameColor = new Color(0.55f, 0.82f, 1f);
        [SerializeField] private Color textColor = Color.white;

        [Header("Voice Blips")]
        [SerializeField] private AudioClip blip;
        [SerializeField] private Vector2 blipPitch = new Vector2(0.95f, 1.05f);
        [SerializeField, Min(1)] private int charactersPerBlip = 3;

        public string DisplayName => displayName;
        public Color NameColor => nameColor;
        public Color TextColor => textColor;
        public AudioClip Blip => blip;
        public Vector2 BlipPitch => blipPitch;
        public int CharactersPerBlip => charactersPerBlip;
    }
}
