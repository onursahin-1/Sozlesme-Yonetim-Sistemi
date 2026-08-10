using Microsoft.EntityFrameworkCore;
using Sys.Domain;
using Sys.Services;

namespace Sys.Infrastructure;

public class UserRepository : IUserRepository
{
    private readonly string _connectionString;

    public UserRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<User?> GetByUsernameAsync(string username)
    {
        using var db = DbConnectionFactory.CreateContext(_connectionString);
        return await db.Users.FirstOrDefaultAsync(u => u.Username == username);
    }

    public async Task UpdateAsync(User user)
    {
        using var db = DbConnectionFactory.CreateContext(_connectionString);
        db.Users.Update(user);
        await db.SaveChangesAsync();
    }
}