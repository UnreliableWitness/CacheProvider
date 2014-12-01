using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace Carfac.Client.CacheProvider
{
    public class CacheArchive
    {
        public CacheArchive(string typeName)
        {
            CacheEntries = new ConcurrentStack<CacheEntry>();
            Name = typeName;
        }

        public string Name { get; set; }
        public ConcurrentStack<CacheEntry> CacheEntries { get; set; }


        public void AddOrUpdateEntry(ICacheable value, string caller, ConcurrentBag<ICacheable> children,
            ConcurrentBag<IEnumerable<ICacheable>> cacheableChilCollections, bool isRoot)
        {
            if(string.IsNullOrEmpty(value.CacheKey))
                throw new ArgumentNullException(string.Format("CacheKey may never be null: dit you implement it? ({0})", value.GetType().FullName));

            IQueryable<CacheEntry> query =
                (from ce in CacheEntries where ce.Value.CacheKey.Equals(value.CacheKey) select ce).AsQueryable();

            if (query.Any())
            {
                if(query.Count() > 1)
                    throw new Exception("More then one entry with same key found.");

                //there is allready an object cached with the same key
                var found = query.First();
                if(!(from c in found.Callers where c.Equals(caller) select c).Any())
                    found.Callers.Push(caller); //update callers with new caller if it's not present yet

                //replace value with new value
                found.Value = value;
            }
            else
            {
                //key not found, adding new entry
                var newEntry = new CacheEntry();
                
                if(isRoot)
                    newEntry.Callers.Push(caller);
                
                newEntry.Key = value.CacheKey;
                newEntry.Value = value;

                //children are all the nested objects
                foreach (var child in children)
                {
                    if (!newEntry.LinkedCacheEntries.TryAdd(child.GetType(), child.CacheKey))
                    {
                        //could not add the nested object :/ is the type already present in the dictionary?
                    }
                }

                foreach (var child in cacheableChilCollections)
                {
                        //since we're dealing with a nested collection we loop through the cacheables and add them 
                        //to a specialized collection container


                        //we're figuring the type is the same throughout the collection
                        var type = child.First().GetType();
                        //next we'll need a list of the cachekeys
                        var cacheKeyList = child.Select(cacheable => cacheable.CacheKey).ToList();
                        newEntry.LinkedCacheCollection.TryAdd(type, cacheKeyList);
                }


                CacheEntries.Push(newEntry);
            }
        }
    }
}