using Microsoft.Extensions.Caching.Memory;

public class CacheHelper
{
    private readonly IMemoryCache _cache;

    public CacheHelper(IMemoryCache cache)
    {
        _cache = cache;
    }

    public void Set<T>(
        string key,
        T value,
        TimeSpan? expiration = null
    )
    {
        var options = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = expiration ?? TimeSpan.FromMinutes(10)
        };

        _cache.Set(key, value, options);
    }

    public bool TryGet<T>(string key, out T? value)
    {
        return _cache.TryGetValue(key, out value);
    }

    public void Remove(string key)
    {
        _cache.Remove(key);
    }
}

public static class CacheKeys
{
    public static string FormProduct(int empresaId)
        => $"form-product:{empresaId}";

    public static string FormUsers(int empresaId)
        => $"form-users:{empresaId}";
    public static string FormUsersDashboard(int empresaId)
        => $"dashboard-form-users:{empresaId}";
}