using Sys.Domain;

namespace Sys.Services;

public interface IUserRepository
{
    Task<User?> GetByUsernameAsync(string username);
    Task UpdateAsync(User user);
}