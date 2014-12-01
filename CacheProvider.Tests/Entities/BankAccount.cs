using System;

namespace Carfac.Client.CacheProvider.Tests.Entities
{
    [Serializable]
    public class BankAccount : ICacheable
    {
        public string Iban { get; set; }
        public string Bic { get; set; }

        public string CacheKey { get { return Iban + Bic; } }
        public DateTime StateTimestamp { get; set; }
        public DateTime ReadFromCache { get; set; }
        public DateTime AddedToCache { get; set; }
        public DateTime Persisted { get; set; }
        public CacheStateType CacheState { get; set; }
    }
}
