using Microsoft.Extensions.Caching.Memory;

public class CacheHelper
{
    private readonly IMemoryCache _cache;

    public CacheHelper(IMemoryCache cache)
    {
        _cache = cache;
    }

    public void Remove(int key)
    {
        _cache.Remove(key);
    }

    public void RemoveByEmpresa(int empresaId)
    {
        _cache.Remove($"form-product:{empresaId}");
    }
}
