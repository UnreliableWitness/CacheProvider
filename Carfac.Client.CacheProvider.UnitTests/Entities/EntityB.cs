using System;
using System.Globalization;

namespace Carfac.Client.CacheProvider.UnitTests.Entities
{
    [Serializable]
    public class EntityB : ICacheable
    {
        public int Id { get; set; }

        public EntityD EntityD { get; set; }

        public string CacheKey { get { return Id.ToString(CultureInfo.InvariantCulture); } }
        public DateTime StateTimestamp { get; set; }
        public DateTime ReadFromCache { get; set; }
        public DateTime AddedToCache { get; set; }
        public DateTime Persisted { get; set; }
        public CacheStateType CacheState { get; set; }
    }
}
