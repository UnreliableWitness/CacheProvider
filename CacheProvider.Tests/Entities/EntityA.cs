using System;

namespace Carfac.Client.CacheProvider.Tests.Entities
{
    [Serializable]
    public class EntityA : ICacheable
    {
        public string Name { get; set; }

        public string CacheKey
        {
            get { return Name; }
        }

        public DateTime StateTimestamp { get; set; }
        public DateTime ReadFromCache { get; set; }
        public DateTime AddedToCache { get; set; }
        public DateTime Persisted { get; set; }
        public CacheStateType CacheState { get; set; }


        public EntityB EntityB { get; set; }
    }
}