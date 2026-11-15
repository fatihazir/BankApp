namespace BankingSystemCore.Repositories;

/// <summary>
/// Base class for all database entities.
/// </summary>
/// <typeparam name="T">Type of the primary key, e.g. <see cref="int"/>, <see cref="Guid"/> or <see cref="string"/>.</typeparam>
public abstract class Entity<T>
{
    public T Id { get; set; }

    public DateTime CreateDate { get; set; }

    public DateTime? UpdateDate { get; set; }

    public DateTime? DeleteDate { get; set; }

    protected Entity()
    {
        Id = default!;
    }

    protected Entity(T id)
    {
        Id = id;
    }
}
