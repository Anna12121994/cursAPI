using PlantShop.API.Models;

namespace PlantShop.API.Data
{
    public static class DbInitializer
    {
        public static void SeedAdmin(AppDbContext context)
        {
            if (!context.Users.Any(u => u.Role == "Admin"))
            {
                var admin = new User
                {
                    UserName = "admin",
                    Email = "admin@gmail.com",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin123!"),
                    Role = "Admin"
                };

                context.Users.Add(admin);
                context.SaveChanges();
            }
        }
    }
}