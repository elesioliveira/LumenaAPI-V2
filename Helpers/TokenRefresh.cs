using Npgsql;
using System.Security.Cryptography;
using System.Text;

public static class RefreshTokenHelper
{
    public static string GenerateRefreshToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(64); // 512 bits
        return Convert.ToBase64String(bytes);
    }

    public static string HashToken(string token)
    {
        using var sha = SHA256.Create();
        var hashed = sha.ComputeHash(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(hashed); // .NET 5+ method; hex string uppercase
    }
}

public interface IRefreshTokenRepository
{
    Task SaveAsync(int usuarioId, string tokenHash, DateTime expiresAt);
    Task<(int id, int usuarioId)> ValidateAndGetOwnerAsync(string tokenHash);
    Task RevokeAsync(int usuarioId, string tokenHash);
    Task RevokeAllForUserAsync(int usuarioId);
    Task ReplaceTokenAsync(int usuarioId, string oldHash, string newHash, DateTime newExpiresAt);
}



public class RefreshTokenRepository : IRefreshTokenRepository
{
    private readonly string _conn;

    public RefreshTokenRepository(IConfiguration config)
    {
        _conn = config.GetConnectionString("DefaultConnection");
    }

    public async Task SaveAsync(int usuarioId, string tokenHash, DateTime expiresAt)
    {
        const string sql = @"
            INSERT INTO refresh_tokens (usuario_id, token_hash, expires_at)
            VALUES (@usuario_id, @token_hash, @expires_at);
        ";

        await using var conn = new NpgsqlConnection(_conn);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("usuario_id", usuarioId);
        cmd.Parameters.AddWithValue("token_hash", tokenHash);
        cmd.Parameters.AddWithValue("expires_at", expiresAt);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<(int id, int usuarioId)> ValidateAndGetOwnerAsync(string tokenHash)
    {
        const string sql = @"
            SELECT id, usuario_id FROM refresh_tokens
            WHERE token_hash = @token_hash AND revoked = false AND expires_at > now()
            LIMIT 1;
        ";

        await using var conn = new NpgsqlConnection(_conn);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("token_hash", tokenHash);

        await using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) return (0, 0);

        return (reader.GetInt32(0), reader.GetInt32(1));
    }

    public async Task RevokeAsync(int usuarioId, string tokenHash)
    {
        const string sql = @"
            UPDATE refresh_tokens SET revoked = true WHERE usuario_id = @usuario_id AND token_hash = @token_hash;
        ";
        await using var conn = new NpgsqlConnection(_conn);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("usuario_id", usuarioId);
        cmd.Parameters.AddWithValue("token_hash", tokenHash);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task RevokeAllForUserAsync(int usuarioId)
    {
        const string sql = "UPDATE refresh_tokens SET revoked = true WHERE usuario_id = @usuario_id;";
        await using var conn = new NpgsqlConnection(_conn);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("usuario_id", usuarioId);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task ReplaceTokenAsync(int usuarioId, string oldHash, string newHash, DateTime newExpiresAt)
    {
        // Revoke old and insert new within the same connection
        const string sqlRevoke = @"UPDATE refresh_tokens SET revoked = true WHERE usuario_id = @usuario_id AND token_hash = @old_hash;";
        const string sqlInsert = @"INSERT INTO refresh_tokens (usuario_id, token_hash, expires_at) VALUES (@usuario_id, @new_hash, @expires_at);";

        await using var conn = new NpgsqlConnection(_conn);
        await conn.OpenAsync();
        await using var tx = await conn.BeginTransactionAsync();
        try
        {
            await using (var cmd = new NpgsqlCommand(sqlRevoke, conn, tx))
            {
                cmd.Parameters.AddWithValue("usuario_id", usuarioId);
                cmd.Parameters.AddWithValue("old_hash", oldHash);
                await cmd.ExecuteNonQueryAsync();
            }

            await using (var cmd = new NpgsqlCommand(sqlInsert, conn, tx))
            {
                cmd.Parameters.AddWithValue("usuario_id", usuarioId);
                cmd.Parameters.AddWithValue("new_hash", newHash);
                cmd.Parameters.AddWithValue("expires_at", newExpiresAt);
                await cmd.ExecuteNonQueryAsync();
            }

            await tx.CommitAsync();
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }
}
