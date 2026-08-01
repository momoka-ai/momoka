namespace Momoka.Home.Services;

public class SelectionService
{
    public List<Guid> SelectedIds { get; } = new();
    public Guid? FocusId { get; set; }

    public void Select(Guid id) => SelectedIds.Add(id);
    public void Deselect(Guid id) => SelectedIds.Remove(id);
    public void Clear() { SelectedIds.Clear(); FocusId = null; }
    public bool IsSelected(Guid id) => SelectedIds.Contains(id);
}
