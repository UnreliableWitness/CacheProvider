using System;
using System.Collections.Generic;
using System.Globalization;
using Caliburn.Micro;

namespace Carfac.Client.CacheProvider.Tests.Entities
{
    [Serializable]
    public class EntityC : ICacheable
    {
        public int Id { get; set; }

        public string CacheKey { get { return Id.ToString(CultureInfo.InvariantCulture); } }
        public DateTime StateTimestamp { get; set; }
        public DateTime ReadFromCache { get; set; }
        public DateTime AddedToCache { get; set; }
        public DateTime Persisted { get; set; }
        public CacheStateType CacheState { get; set; }

        public EntityA EntityAEntry { get; set; }

        public BindableCollection<EntityB> EntityBList { get; private set; }

        public EntityC()
        {
            EntityBList = new BindableCollection<EntityB>();
        }
    }
}
