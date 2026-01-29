using PbesApi.Models;

namespace PbesApi.Services;

public interface ITokenService
{
    string GenerateToken(Officer officer);
}
