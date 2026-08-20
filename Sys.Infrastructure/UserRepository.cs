using System.Linq;
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

    public async Task<List<User>> GetAllAsync()
    {
        using var db = DbConnectionFactory.CreateContext(_connectionString);
        return await db.Users.AsNoTracking().OrderBy(u => u.FullName).ToListAsync();
    }

    public async Task<User?> GetByIdAsync(int id)
    {
        using var db = DbConnectionFactory.CreateContext(_connectionString);
        return await db.Users.FirstOrDefaultAsync(u => u.Id == id);
    }

    public async Task AddAsync(User user)
    {
        using var db = DbConnectionFactory.CreateContext(_connectionString);
        db.Users.Add(user);

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            // Username üzerindeki unique index (daha önce eklenmişti) burada devreye girer.
            throw new InvalidOperationException(
                $"\"{user.Username}\" kullanıcı adı zaten kullanılıyor.", ex);
        }
    }
}