using Sys.Domain;
using Sys.Services;

namespace Sys.Services.Tests;

public class FakeUserRepository : IUserRepository
{
    public List<User> Users { get; } = new();
    private int _nextId = 1;

    public Task<User?> GetByUsernameAsync(string username)
        => Task.FromResult(Users.FirstOrDefault(u => u.Username == username));

    public Task<User?> GetByIdAsync(int id)
        => Task.FromResult(Users.FirstOrDefault(u => u.Id == id));

    public Task<List<User>> GetAllAsync() => Task.FromResult(Users.ToList());

    public Task AddAsync(User user)
    {
        user.Id = _nextId++;
        Users.Add(user);
        return Task.CompletedTask;
    }

    // Depo, çağıranın verdiği nesnenin AYNISINI tutuyor; testlerde servis nesneyi
    // yerinde güncellediği için ayrıca kopyalamaya gerek yok.
    public Task UpdateAsync(User user)
    {
        var index = Users.FindIndex(u => u.Id == user.Id);
        if (index >= 0) Users[index] = user;
        return Task.CompletedTask;
    }
}
