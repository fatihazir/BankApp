namespace BankingSystemCore.Security.Jwt;

public interface ITokenHelper
{
    AccessToken CreateToken(TokenUser user);
}
