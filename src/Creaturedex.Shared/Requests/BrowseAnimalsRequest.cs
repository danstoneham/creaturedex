namespace Creaturedex.Shared.Requests;

public class BrowseAnimalsRequest
{
    public string? Category { get; set; }
    public bool? IsPet { get; set; }
    public string? Tag { get; set; }
    public string? Search { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 24;
    public string SortBy { get; set; } = "name";
}
