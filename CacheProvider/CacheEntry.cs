using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Carfac.Client.CacheProvider
{
    public class CacheEntry
    {
        public int Id { get; set; }

        //key of the entity "id"
        public string Key { get; set; }

        //methods that return this as (part of) their result
        public ConcurrentStack<string> Callers { get; set; }

        //the entity itsself
        public ICacheable Value { get; set; }

        //contains all links to other cacheItems e.g. customer has interactable
        public ConcurrentDictionary<Type,string> LinkedCacheEntries { get; set; }

        //contains all links to collections of cacheables e.g. customer has many bankaccounts
        public ConcurrentDictionary<Type, IEnumerable<string>> LinkedCacheCollection { get; set; } 

        public CacheEntry()
        {
            LinkedCacheEntries = new ConcurrentDictionary<Type, string>();
            LinkedCacheCollection = new ConcurrentDictionary<Type, IEnumerable<string>>();

            Callers = new ConcurrentStack<string>();
        }
    }
}
