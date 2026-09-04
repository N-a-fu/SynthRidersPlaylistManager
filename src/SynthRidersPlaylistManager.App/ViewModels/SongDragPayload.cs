namespace SynthRidersPlaylistManager.App.ViewModels;

public sealed record SongDragPayload(IReadOnlyList<SongItemViewModel> Songs, string Origin)
{
    public int Count => Songs.Count;
    public string Feedback => Count == 1 ? Songs[0].Title : $"{Count}曲";
}
