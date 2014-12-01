using System;
using System.Collections.Generic;

namespace Carfac.Client.CacheProvider
{
    public interface ICacheProvider
    {
        /// <summary>
        /// Adds the object to the cache.
        /// </summary>
        /// <param name="method">The method used to get the object.</param>
        /// <param name="arguments">The arguments.</param>
        /// <param name="result">The object found in cache.</param>
        /// <param name="isRoot">if set to <c>true</c> [is root]. Usually doesn't need to be changed by end user.</param>
        void Add(string method, object[] arguments, ICacheable result, bool isRoot = true);

        /// <summary>
        /// Adds the objects to the cache.
        /// </summary>
        /// <param name="method">The method used to retrieve the objects.</param>
        /// <param name="arguments">The arguments.</param>
        /// <param name="results">The objects to cache.</param>
        void Add(string method, object[] arguments, IEnumerable<ICacheable> results);

        /// <summary>
        /// Use to update a cacheable. When it is not yet present in cache it will be added.
        /// </summary>
        /// <param name="toUpdate">The icacheable that was altered.</param>
        /// <returns>The amount of cached items that were marked as altered.</returns>
        int Update(ICacheable toUpdate);

        /// <summary>
        /// Deletes the specified Cacheable to delete.
        /// </summary>
        /// <param name="toDelete">The cacheable to delete.</param>
        /// <returns>The amount of cacheables that were marked as 'Deleted'.</returns>
        int Delete(ICacheable toDelete);

        /// <summary>
        /// Gets all the instances from cache that are linked to a method with specific arguments
        /// </summary>
        /// <param name="typeToGet"></param>
        /// <param name="method"></param>
        /// <param name="arguments"></param>
        /// <returns></returns>
        IEnumerable<ICacheable> Get(Type typeToGet, string method, object[] arguments);

        /// <summary>
        /// Gets all the instances from a certain type and with a certain key from the cache
        /// </summary>
        /// <param name="typeToGet"></param>
        /// <param name="key"></param>
        /// <returns></returns>
        IEnumerable<ICacheable> Get(Type typeToGet, string key);


        /// <summary>
        /// Reinstalls the data store.
        /// Deletes the cache.db and re creates it.
        /// </summary>
        void ReinstallDataStore();

        /// <summary>
        /// Clears the cache in memory and deletes all the cache in de datastore.
        /// </summary>
        void ClearCache();

        /// <summary>
        /// Clears the cache in memory.
        /// All collections of entries are cleared.
        /// This means that when all references to the objects are lost, they will be GC'ed.
        /// </summary>
        void ClearCacheInMemory();

        /// <summary>
        /// Loads the cached objects to memory.
        /// Current cache in memory will be discarded.
        /// </summary>
        void LoadCacheFromDatabase();

        /// <summary>
        /// Saves the current cached objects in memory to the datastore.
        /// </summary>
        void SaveCache();
    }
}
