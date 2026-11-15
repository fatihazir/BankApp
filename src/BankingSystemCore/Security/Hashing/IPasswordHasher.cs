namespace BankingSystemCore.Security.Hashing;

public interface IPasswordHasher
{
    /// <summary>Returns a self-describing hash (algorithm, cost, salt, hash). Never store the password itself.</summary>
    string Hash(string password);

    bool Verify(string password, string passwordHash);
}
