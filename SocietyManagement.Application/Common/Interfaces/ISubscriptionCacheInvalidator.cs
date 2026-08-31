namespace SocietyManagement.Application.Common.Interfaces;

/// <summary>Lets a Super Admin command clear SubscriptionActiveFilter's
/// short-lived cache for one society immediately after changing its
/// subscription/suspension state — implemented in the API layer, which owns
/// the IMemoryCache and the cache key format (see SubscriptionActiveFilter).
/// Without this, a manual "restrict now" would still let that society
/// through for up to the cache's TTL.</summary>
public interface ISubscriptionCacheInvalidator
{
    void Invalidate(int societyId);
}
