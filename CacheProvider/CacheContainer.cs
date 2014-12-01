using System.Collections.Concurrent;
using System.Linq;

namespace Carfac.Client.CacheProvider
{
    public class CacheContainer
    {
        public ConcurrentBag<CacheArchive> CacheArchives;

        public CacheContainer()
        {
            Name = "Default";

            CacheArchives = new ConcurrentBag<CacheArchive>();
        }

        public string Name { get; set; }

        public CacheArchive AddContainerForType<T>(T type) where T : ICacheable
        {
            string typeName = type.GetType().FullName;
            IQueryable<CacheArchive> query =
                (from cc in CacheArchives where cc.Name.Equals(typeName) select cc).AsQueryable();
            if (query.Any())
                return query.First();

            var newArchive = new CacheArchive(typeName);

            CacheArchives.Add(newArchive);
            return newArchive;
        }

    }
}