namespace BankingSystemCore.Repositories;

/// <summary>
/// A single page of query results.
/// </summary>
public class Paginate<T>
{
    /// <summary>Zero-based page index.</summary>
    public int Index { get; set; }

    public int Size { get; set; }

    /// <summary>Total number of items across all pages.</summary>
    public int Count { get; set; }

    public int Pages { get; set; }

    public IList<T> Items { get; set; } = new List<T>();

    public bool HasPrevious => Index > 0;

    public bool HasNext => Index + 1 < Pages;
}
