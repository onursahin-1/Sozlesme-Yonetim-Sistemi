using Sys.Domain;

namespace Sys.Services;

public interface IUserRepository
{
    Task<User?> GetByUsernameAsync(string username);
    Task UpdateAsync(User user);
    Task<List<User>> GetAllAsync();
    Task<User?> GetByIdAsync(int id);
    Task AddAsync(User user);
}