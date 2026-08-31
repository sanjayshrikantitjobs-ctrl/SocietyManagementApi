using Microsoft.Extensions.Caching.Memory;
using SocietyManagement.Application.Common.Interfaces;

namespace SocietyManagement.API.Authorization;

/// <summary>API-layer implementation of ISubscriptionCacheInvalidator — lives
/// here (not Infrastructure) because it shares SubscriptionActiveFilter's
/// cache key format and IMemoryCache instance.</summary>
public class SubscriptionCacheInvalidator : ISubscriptionCacheInvalidator
{
    private readonly IMemoryCache _cache;

    public SubscriptionCacheInvalidator(IMemoryCache cache)
    {
        _cache = cache;
    }

    public void Invalidate(int societyId) => _cache.Remove(SubscriptionActiveFilter.CacheKey(societyId));
}
