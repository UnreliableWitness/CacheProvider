using System;
using System.Globalization;
using Caliburn.Micro;

namespace Carfac.Client.CacheProvider.UnitTests.Entities
{
    [Serializable]
    public class Customer : ICacheable
    {
        public int Id { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }

        public BindableCollection<BankAccount> BankAccounts { get; set; }

        #region Cacheable
        public string CacheKey
        {
            get { return Id.ToString(CultureInfo.InvariantCulture); }
        }

        public DateTime StateTimestamp { get; set; }
        public DateTime ReadFromCache { get; set; }
        public DateTime AddedToCache { get; set; }
        public DateTime Persisted { get; set; }
        public CacheStateType CacheState { get; set; }

        #endregion

        public Customer()
        {
            BankAccounts = new BindableCollection<BankAccount>();
        }
    }
}
