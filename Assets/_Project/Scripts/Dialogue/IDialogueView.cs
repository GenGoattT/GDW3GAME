namespace Project.Dialogue
{
    public interface IDialogueView
    {
        void Open();
        void ShowLine(DialogueLine line, float revealTime);
        void Close();
        int VisibleCharacters { get; }
    }
}
