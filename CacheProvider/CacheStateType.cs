using System;

namespace Carfac.Client.CacheProvider
{
    /// <summary>
    /// The state of the object in the cache.
    /// This represent wheter the object was persisted to the local store.
    /// </summary>
    public enum CacheStateType
    {
        /// <summary>
        /// Object was saved. All is well.
        /// </summary>
        Saved,
        /// <summary>
        /// Object has not yet been saved. Awaiting next CacheSave.
        /// </summary>
        Unsaved,
        /// <summary>
        /// The object was altered. Awaiting next CacheSave.
        /// </summary>
        Altered,
        /// <summary>
        /// The object was deleted. Awaiting next CacheSave.
        /// </summary>
        Deleted
    }
}
