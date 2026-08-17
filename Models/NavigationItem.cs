namespace TG.Payroll.Web.Models;

public sealed class NavigationItem
{
    public int Id { get; init; }
    public int? ParentId { get; init; }
    public string Name { get; init; } = string.Empty;
    public List<NavigationItem> Children { get; } = [];
}
