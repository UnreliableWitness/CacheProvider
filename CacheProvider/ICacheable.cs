using System;

namespace Carfac.Client.CacheProvider
{
    /// <summary>
    /// Objects that must be cached have to implement ICacheable.
    /// This interface demands that you provide a unique cachekey.
    /// </summary>
    public interface ICacheable
    {

        /// <summary>
        /// The cachekey is the unique identifier of the current object. 
        /// Return this value in the getter. eg. return this.Id.ToString();
        /// </summary>
        /// <value>
        /// The cache key.
        /// </value>
        string CacheKey { get; }

        DateTime ReadFromCache { get; set; }

        DateTime AddedToCache { get; set; }

        DateTime Persisted { get; set; }

        CacheStateType CacheState { get; set; }
    }
}
