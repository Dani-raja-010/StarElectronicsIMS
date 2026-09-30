using System;
using System.Data;
using System.Security.Cryptography;

namespace StarElectronicsIMS.Data
{
    public class AppUser
    {
        public int UserID { get; set; }
        public string Username { get; set; }
        public string FullName { get; set; }
        public string Role { get; set; }
        public bool IsAdmin => Role == Roles.Admin;
        public string DisplayName => string.IsNullOrWhiteSpace(FullName) ? Username : FullName;
    }

    public static class Roles
    {
        public const string Admin = "Admin";
        public const string Staff = "Staff";
    }

    public static class Session
    {
        public static AppUser CurrentUser { get; set; }
        public static bool IsAdmin => CurrentUser?.IsAdmin == true;
        public static int? UserID => CurrentUser?.UserID;
    }

    /// <summary>PBKDF2 (SHA-256) password hashing.</summary>
    public static class PasswordHasher
    {
        const int SaltSize = 16, HashSize = 32, Iterations = 100_000;

        public static (byte[] hash, byte[] salt) Hash(string password)
        {
            var salt = new byte[SaltSize];
            using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(salt);
            return (Derive(password, salt), salt);
        }

        public static bool Verify(string password, byte[] hash, byte[] salt)
        {
            var actual = Derive(password, salt);
            if (actual.Length != hash.Length) return false;
            int diff = 0;
            for (int i = 0; i < hash.Length; i++) diff |= actual[i] ^ hash[i];
            return diff == 0;
        }

        static byte[] Derive(string password, byte[] salt)
        {
            using (var kdf = new Rfc2898DeriveBytes(password, salt, Iterations, HashAlgorithmName.SHA256))
                return kdf.GetBytes(HashSize);
        }
    }

    public static class UserRepository
    {
        public const int MinPasswordLength = 6;

        public static bool AnyUsers() => Convert.ToInt32(Db.Scalar("SELECT COUNT(*) FROM Users")) > 0;

        /// <returns>The user, or null if the username/password is wrong or the account is disabled.</returns>
        public static AppUser Authenticate(string username, string password)
        {
            var dt = Db.Query("SELECT UserID, Username, FullName, Role, PasswordHash, PasswordSalt FROM Users WHERE Username=@u AND IsActive=1",
                Db.P("@u", username.Trim()));
            if (dt.Rows.Count == 0) return null;
            var r = dt.Rows[0];
            if (!PasswordHasher.Verify(password, (byte[])r["PasswordHash"], (byte[])r["PasswordSalt"])) return null;
            return FromRow(r);
        }

        public static int Create(string username, string fullName, string role, string password)
        {
            var (hash, salt) = PasswordHasher.Hash(password);
            return Convert.ToInt32(Db.Scalar(
                "INSERT INTO Users (Username, FullName, Role, PasswordHash, PasswordSalt) OUTPUT INSERTED.UserID VALUES (@u,@f,@r,@h,@s)",
                Db.P("@u", username.Trim()), Db.P("@f", fullName?.Trim()), Db.P("@r", role), Db.P("@h", hash), Db.P("@s", salt)));
        }

        public static void Update(int userId, string fullName, string role, bool isActive)
        {
            Db.Execute("UPDATE Users SET FullName=@f, Role=@r, IsActive=@a WHERE UserID=@id",
                Db.P("@f", fullName?.Trim()), Db.P("@r", role), Db.P("@a", isActive), Db.P("@id", userId));
        }

        public static void SetPassword(int userId, string password)
        {
            var (hash, salt) = PasswordHasher.Hash(password);
            Db.Execute("UPDATE Users SET PasswordHash=@h, PasswordSalt=@s WHERE UserID=@id",
                Db.P("@h", hash), Db.P("@s", salt), Db.P("@id", userId));
        }

        public static bool UsernameExists(string username) =>
            Convert.ToInt32(Db.Scalar("SELECT COUNT(*) FROM Users WHERE Username=@u", Db.P("@u", username.Trim()))) > 0;

        /// <summary>Number of active admins other than the given user — used to stop the last admin being removed.</summary>
        public static int OtherActiveAdmins(int userId) =>
            Convert.ToInt32(Db.Scalar("SELECT COUNT(*) FROM Users WHERE Role='Admin' AND IsActive=1 AND UserID<>@id", Db.P("@id", userId)));

        static AppUser FromRow(DataRow r) => new AppUser
        {
            UserID = (int)r["UserID"],
            Username = (string)r["Username"],
            FullName = r["FullName"] as string,
            Role = (string)r["Role"],
        };
    }
}
