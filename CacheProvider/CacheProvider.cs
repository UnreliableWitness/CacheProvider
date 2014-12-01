using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Caliburn.Micro;

namespace Carfac.Client.CacheProvider
{
    public class CacheProvider : ICacheProvider
    {
        private readonly CacheContainer _cacheContainer;
        private readonly PropertiesCache _propertiesCache;
        private readonly CachePersistor _cachePersistor;

        public CacheProvider()
        {
            _cachePersistor = new CachePersistor();
            _propertiesCache = new PropertiesCache();
            _cacheContainer = new CacheContainer();
        }

        public void ReinstallDataStore()
        {
            _cachePersistor.ReinstallDataStore();
        }

        /// <summary>
        /// Adds the object to the cache.
        /// </summary>
        /// <param name="method">The method used to get the object.</param>
        /// <param name="arguments">The arguments.</param>
        /// <param name="result">The object found in cache.</param>
        /// <param name="isRoot">if set to <c>true</c> [is root]. Usually doesn't need to be changed by end user.</param>
        public void Add(string method, object[] arguments, ICacheable result, bool isRoot = true)
        {
            //todo: check if typeto add is cacheable
            //todo: check if typeto add is serializable

            result.CacheState = CacheStateType.Unsaved;
            result.AddedToCache = DateTime.Now;

            string callerKey = BuildCacheKeyFrom(method, arguments);

            //get root and children
            PropertyInfo[] properties = _propertiesCache.GetPropertyInfo(result);//get all properties of the current type 

            //get all properties that are cacheables
            IEnumerable<PropertyInfo> children =
                properties.Where(prop => prop.PropertyType.GetInterfaces().Any(i => i == typeof(ICacheable)));


            var cacheableChildren = new ConcurrentBag<ICacheable>();

            //adds all the children to the archives (this is recursive)
            foreach (var child in children)
            {
                var childValue = ((child.GetValue(result, null)) as ICacheable); //cast the property to a real cacheable
                if (childValue == null)
                    continue;

                Add(method, arguments, childValue, false); //add it to the archives
                cacheableChildren.Add(childValue);

                //clear the child
                //child.SetValue(result, null, new object[]{} );
            }

            var cacheableChildrenCollections = new ConcurrentBag<IEnumerable<ICacheable>>();
            foreach (PropertyInfo propertyInfo in properties)
            {
                var indexerCount = propertyInfo.GetIndexParameters().Count();
                if(indexerCount > 0)
                    continue;

                var collectionValue = propertyInfo.GetValue(result, null);
                if(collectionValue != null){
                    //var collectionType = collectionValue.GetType();
                    var typeOfT = GetEnumerableType(collectionValue.GetType());
                    if (typeOfT != null && typeof(ICacheable).IsAssignableFrom(typeOfT))
                    {
                        var childCollection = CreateNewCollection(typeof(ICacheable));
                        var realCollection = (IEnumerable) collectionValue;
                        foreach (var entry in realCollection)
                        {
                            childCollection.Add(entry);
                        }

                        //var childCollection = collectionValue as BindableCollection<ICacheable>;
                        if (childCollection != null && childCollection.Count > 0)
                        {
                            var temp = new List<ICacheable>();
                            foreach (var cacheable in childCollection)
                            {
                                if (cacheable == null)
                                    continue;
                                Add(method, arguments, (cacheable as ICacheable), false);
                                temp.Add((cacheable as ICacheable)); //add this nested object as a link to the current object
                            }
                            cacheableChildrenCollections.Add(temp);
                        }
                    }
                }
            }

            //get the correct archive for given type
            CacheArchive ca = _cacheContainer.AddContainerForType(result);
            //add or update entry in this archive
            ca.AddOrUpdateEntry(result, callerKey, cacheableChildren, cacheableChildrenCollections, isRoot);
        }


        static Type GetEnumerableType(Type type)
        {
            foreach (Type intType in type.GetInterfaces())
            {
                if (intType.IsGenericType
                    && intType.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                {
                    return intType.GetGenericArguments()[0];
                }
            }
            return null;
        }

        /// <summary>
        /// Adds the objects to the cache.
        /// </summary>
        /// <param name="method">The method used to retrieve the objects.</param>
        /// <param name="arguments">The arguments.</param>
        /// <param name="results">The objects to cache.</param>
        public void Add(string method, object[] arguments, IEnumerable<ICacheable> results)
        {
            //Parallel.ForEach(results, cacheable => Add(method, arguments, cacheable));

            foreach (ICacheable cacheable in results)
            {
                Add(method, arguments, cacheable);
            }
        }

        /// <summary>
        /// Use to update a cacheable. When it is not yet present in cache it will be added.
        /// </summary>
        /// <param name="toUpdate">To update.</param>
        /// <returns>
        /// The amount of cached items that were marked as altered.
        /// </returns>
        public int Update(ICacheable toUpdate)
        {
            var oldValueCollection = Get(toUpdate.GetType(), toUpdate.CacheKey);
            if (oldValueCollection.Any())
            {
                var oldValue = oldValueCollection.First();
                if (oldValue.CacheState == CacheStateType.Unsaved)
                    return 0;

                oldValue.CacheState = CacheStateType.Altered;

                return 1; //there can be only one cacheable with a type and a key, really
            }
            return 0; //nothing was found so nothing was marked as 'altered'
        }

        /// <summary>
        /// Deletes the specified Cacheable to delete.
        /// </summary>
        /// <param name="toDelete">The cacheable to delete.</param>
        /// <returns>
        /// The amount of cacheables that were marked as 'Deleted'.
        /// </returns>
        public int Delete(ICacheable toDelete)
        {
            var oldValueCollection = Get(toDelete.GetType(), toDelete.CacheKey);
            if (oldValueCollection.Any())
            {
                var oldValue = oldValueCollection.First();
                oldValue.CacheState = CacheStateType.Deleted;

                return 1; //there can be only one cacheable with a type and a key, really
            }
            return 0; //nothing was found so nothing was marked as 'altered'
        }

        /// <summary>
        /// Gets all the instances from cache that are linked to a method with specific arguments.
        /// </summary>
        /// <param name="typeToGet"></param>
        /// <param name="method"></param>
        /// <param name="arguments"></param>
        /// <returns></returns>
        public IEnumerable<ICacheable> Get(Type typeToGet, string method, object[] arguments)
        {
            //todo: check if typetoget is cacheable
            //todo: check if typetoget is serializable

            string callerKey = BuildCacheKeyFrom(method, arguments);
            string archiveType = typeToGet.FullName;
            CacheArchive archiveToSearch =
                (from ca in _cacheContainer.CacheArchives where ca.Name.Equals(archiveType) select ca).FirstOrDefault();
            if (archiveToSearch == null)
                return null;

            List<CacheEntry> entries =
                (from ce in archiveToSearch.CacheEntries where ce.Callers.Contains(callerKey) select ce).ToList();

            foreach (CacheEntry parent in entries)
            {
                parent.Value.ReadFromCache = DateTime.Now;

                //are there any nested objects? 
                if (parent.LinkedCacheEntries.Any())
                {
                    //get all properties of the real cached object
                    ICacheable realEntity = parent.Value;

                    
                    PropertyInfo[] propertiesOfRealEntity = _propertiesCache.GetPropertyInfo(realEntity);
                    

                    foreach (PropertyInfo propertyInfo in propertiesOfRealEntity)
                    {
                        foreach (var linkedCacheEntry in parent.LinkedCacheEntries)
                        {
                            Type x = propertyInfo.PropertyType;
                            if (linkedCacheEntry.Key == x)
                            {
                                //hit: fill up with GET from other cache archive
                                IEnumerable<ICacheable> foundChildren = Get(x, linkedCacheEntry.Value);

                                propertyInfo.SetValue(realEntity, null, new object[] {});
                                propertyInfo.SetValue(realEntity, foundChildren.First(), new object[] {});
                            }
                        }
                    }
                }

                //are there any nested collections?
                if (parent.LinkedCacheCollection.Any())
                {
                    ICacheable realEntity = parent.Value;
                    PropertyInfo[] propertiesOfRealEntity = _propertiesCache.GetPropertyInfo(realEntity);

                    foreach (var cacheCollection in parent.LinkedCacheCollection)
                    {
                        foreach (PropertyInfo propertyInfo in propertiesOfRealEntity)
                        {
                            Type type = propertyInfo.PropertyType;
                            if (type.GetGenericArguments().Any()) //just checking if there are any collections, really
                            {
                                Type genericType = type.GetGenericArguments().First(); //getting the T of the collection
                                if (cacheCollection.Key == genericType)
                                {
                                    IList newCollection = CreateNewCollection(genericType);
                                        //creating a brand new List<T> 

                                    foreach (string key in cacheCollection.Value)
                                    {
                                        IEnumerable<ICacheable> foundChildren = Get(genericType, key);
                                            //getting the cached item by its key
                                        newCollection.Add(foundChildren.First()); //adding it to the new collection
                                    }

                                    propertyInfo.SetValue(realEntity, null, new object[] {});
                                        //clearing the existing items
                                    
                                        //adding the freshly fetched items
                                    var newlist = CreateNewCollection(genericType);

                                    foreach (var cacheable in newCollection)
                                    {
                                        (cacheable as ICacheable).ReadFromCache = DateTime.Now;
                                        newlist.Add(cacheable);
                                    }

                                    propertyInfo.SetValue(realEntity, newlist, new object[] { });
                                }
                            }
                        }
                    }
                }
            }

            return entries.Select(p => p.Value).Where(p=>p.CacheState != CacheStateType.Deleted);
        }

        /// <summary>
        /// Creates the new collection using Reflection. 
        /// Returns a List of T.
        /// </summary>
        /// <param name="t">The type you want to make a List of.</param>
        /// <returns>List of T</returns>
        private IList CreateNewCollection(Type t)
        {
            Type listType = typeof(BindableCollection<>);
            Type constructedListType = listType.MakeGenericType(t);
            return (IList)Activator.CreateInstance(constructedListType);
        }

        /// <summary>
        /// Gets all the instances from a certain type and with a certain key from the cache.
        /// </summary>
        /// <param name="typeToGet"></param>
        /// <param name="key"></param>
        /// <returns></returns>
        public IEnumerable<ICacheable> Get(Type typeToGet, string key)
        {
            string archiveType = typeToGet.FullName;
            CacheArchive archiveToSearch =
                (from ca in _cacheContainer.CacheArchives where ca.Name.Equals(archiveType) select ca).FirstOrDefault();
            if (archiveToSearch == null)
                return null;

            List<CacheEntry> entries =
                (from ce in archiveToSearch.CacheEntries where ce.Key.Equals(key) select ce).ToList();

            foreach (CacheEntry cacheable in entries)
            {
                cacheable.Value.ReadFromCache = DateTime.Now;

                if (cacheable.LinkedCacheEntries.Any())
                {
                    ICacheable realEntity = cacheable.Value;
                    PropertyInfo[] propertiesOfRealEntity = _propertiesCache.GetPropertyInfo(realEntity);
                    // realEntity.GetType().GetProperties();
                    foreach (PropertyInfo propertyInfo in propertiesOfRealEntity)
                    {
                        foreach (var linkedCacheEntry in cacheable.LinkedCacheEntries)
                        {
                            Type x = propertyInfo.PropertyType;
                            if (linkedCacheEntry.Key == x)
                            {
                                //hit: fill up with GET from other cache archive
                                IEnumerable<ICacheable> found = Get(x, realEntity.CacheKey);
                            }
                        }
                    }
                }
            }

            return entries.Select(p => p.Value).Where(p => p.CacheState != CacheStateType.Deleted);
        }

        /// <summary>
        /// Builds the cache key from the method and the arguments.
        /// </summary>
        /// <param name="method">The method.</param>
        /// <param name="arguments">The arguments.</param>
        /// <returns></returns>
        private string BuildCacheKeyFrom(string method, IEnumerable<object> arguments)
        {
            string methodName = method;

            string[] workingArguments = (from a in arguments select a.ToString()).ToArray();
            string argsString = string.Join(",", workingArguments);

            string cacheKey = methodName + "-" + argsString;

            return cacheKey;
        }

        /// <summary>
        /// Saves the current cached objects in memory to the datastore.
        /// </summary>
        public void SaveCache()
        {
            _cachePersistor.SaveCache(_cacheContainer);
        }

        /// <summary>
        /// Clears the cache in memory and deletes all the cache in de datastore.
        /// </summary>
        public void ClearCache()
        {
            //empty the cache in memory
            while (!_cacheContainer.CacheArchives.IsEmpty)
            {
                CacheArchive someItem;
                _cacheContainer.CacheArchives.TryTake(out someItem);
            }
            //empty the cache in database
            _cachePersistor.EmptyDatabase();
        }

        /// <summary>
        /// Clears the cache in memory.
        /// All collections of entries are cleared.
        /// This means that when all references to the objects are lost, they will be GC'ed.
        /// </summary>
        public void ClearCacheInMemory()
        {
            //empty the cache in memory
            while (!_cacheContainer.CacheArchives.IsEmpty)
            {
                CacheArchive someItem;
                _cacheContainer.CacheArchives.TryTake(out someItem);
            }
        }

        /// <summary>
        /// Loads the cached objects to memory.
        /// Current cache in memory will be discarded.
        /// </summary>
        public void LoadCacheFromDatabase()
        {
            ClearCacheInMemory();
            var archives = _cachePersistor.LoadArchivesFromDatabase();
            _cachePersistor.LoadEntriesFromDatabase(archives);
            foreach (var cacheArchive in archives)
            {
                _cacheContainer.CacheArchives.Add(cacheArchive);
            }
        }
    }
}