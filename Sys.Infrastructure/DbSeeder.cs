using Microsoft.EntityFrameworkCore;
using Sys.Domain;

namespace Sys.Infrastructure;

public static class DbSeeder
{
    public static async Task SeedAsync(SysDbContext db)
    {
        if (await db.Users.AnyAsync()) return; // zaten seed edilmiş

        var personel = new User
        {
            Username = "personel",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Personel123!"),
            FullName = "Onur Akkaya",
            Role = UserRole.Personel,
            Department = "Satın Alma"
        };
        var syb = new User
        {
            Username = "syb",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Syb123!"),
            FullName = "Emin Ramazanoğlu",
            Role = UserRole.SYB,
            Department = "SYB"
        };
        var mudur = new User
        {
            Username = "mudur",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Mudur123!"),
            FullName = "Murat Yılmaz",
            Role = UserRole.Mudur,
            Department = "Yönetim"
        };

        db.Users.AddRange(personel, syb, mudur);
        await db.SaveChangesAsync();

        db.Contracts.Add(new Contract
        {
            RequestRefNo = "SAS-2026-001",
            Title = "Temizlik Hizmetleri Sözleşmesi",
            CompanyName = "Temiz A.Ş.",
            TaxNo = "1234567890",
            Type = "Hizmet",
            Status = ContractStatus.Aktif,
            Stage = 9,
            StartDate = new DateTime(2026, 1, 1),
            EndDate = new DateTime(2026, 12, 31),
            TotalAmount = 576000,
            CreatedByUserId = syb.Id,
            CreatedAt = DateTime.Now
        });

        await db.SaveChangesAsync();
    }
}