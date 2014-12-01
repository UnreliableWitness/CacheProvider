using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Carfac.Client.CacheProvider.UnitTests.Entities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Carfac.Client.CacheProvider.UnitTests
{
    [TestClass]
    public class UnitTest1
    {
        [TestMethod]
        public void CreateCacheProvider()
        {
            var provider = new Carfac.Client.CacheProvider.CacheProvider();
            Assert.IsNotNull(provider);
        }

        [TestMethod]
        public void CacheTypes()
        {
            var propertiesCache = new PropertiesCache();

            EntityA a = CreateEntityA();

            PropertyInfo[] propsA = propertiesCache.GetPropertyInfo(a);
            PropertyInfo[] propsACached = propertiesCache.GetPropertyInfo(a);

            Assert.AreEqual(propsA, propsACached);
        }

        [TestMethod]
        public void AddSingleObjectToCache()
        {
            var provider = new Carfac.Client.CacheProvider.CacheProvider();

            string method = "GetEntitiesThatContain";
            var arguments = new object[] { "a" };
            EntityA entity = CreateEntityA();

            provider.Add(method, arguments, entity);

            IEnumerable<ICacheable> fromCache = provider.Get(typeof(EntityA), method, arguments);

            Assert.AreEqual(1, fromCache.Count());
        }

        [TestMethod]
        public void AddSameObjectTwiceSameCaller()
        {
            var provider = new Carfac.Client.CacheProvider.CacheProvider();
            EntityA entity = CreateEntityA();

            string method = "GetEntitiesThatContain";
            var arguments = new object[] { "a" };

            for (int i = 0; i < 2; i++)
            {
                provider.Add(method, arguments, entity);
            }

            IEnumerable<ICacheable> fromCache = provider.Get(typeof(EntityA), method, arguments);

            Assert.AreEqual(1, fromCache.Count());
        }

        [TestMethod]
        public void AddSameObjectTwiceDifferentCaller()
        {
            var provider = new Carfac.Client.CacheProvider.CacheProvider();
            EntityA entity = CreateEntityA();

            string method = "GetEntitiesThatContain";

            for (int i = 0; i < 2; i++)
            {
                provider.Add(method, new object[] { i }, entity);
            }

            IEnumerable<ICacheable> fromCacheFirst = provider.Get(typeof(EntityA), method, new object[] { 0 });
            IEnumerable<ICacheable> fromCacheSecond = provider.Get(typeof(EntityA), method, new object[] { 1 });

            Assert.AreEqual(1, fromCacheFirst.Count());
            Assert.AreEqual(1, fromCacheSecond.Count());

            IEnumerable<ICacheable> totalFromCache = provider.Get(typeof(EntityA), entity.CacheKey);
            Assert.AreEqual(1, totalFromCache.Count());
        }

        [TestMethod]
        public void AddListOfObjectsToCache()
        {
            var provider = new Carfac.Client.CacheProvider.CacheProvider();

            string method = "GetEntitiesThatContain";
            var arguments = new object[] { "a" };

            var entities = new List<EntityA>();
            for (int i = 0; i < 100; i++)
            {
                EntityA entity = CreateEntityA();
                entity.Name += i;
                entities.Add(entity);
            }

            provider.Add(method, arguments, entities);

            IEnumerable<ICacheable> fromCache = provider.Get(typeof(EntityA), method, arguments);

            Assert.AreEqual(100, fromCache.Count());
        }


        [TestMethod]
        public void AddObjectWithSubObjectToCache()
        {
            EntityA a = CreateEntityA();
            EntityC c = CreateEntityC();
            c.EntityAEntry = a;

            var provider = new Carfac.Client.CacheProvider.CacheProvider();

            string method = "GetEntitiesThatContain";
            var arguments = new object[] { "a" };

            provider.Add(method, arguments, c);

            IEnumerable<ICacheable> fromCache = provider.Get(typeof(EntityC), method, arguments);

            object realEntity = Convert.ChangeType(fromCache.First(), typeof(EntityC));

            Assert.AreEqual(1, fromCache.Count());
            Assert.AreEqual((realEntity as EntityC).EntityAEntry.Name, a.Name);
        }

        [TestMethod]
        public void AddObjectWithSubObjectToCache3Deep()
        {
            EntityB b = CreateEntityB();
            EntityA a = CreateEntityA();
            EntityC c = CreateEntityC();
            EntityD d = CreateEntityD();
            a.EntityB = b;
            c.EntityAEntry = a;
            b.EntityD = d;

            var provider = new Carfac.Client.CacheProvider.CacheProvider();

            string method = "GetEntitiesThatContain";
            var arguments = new object[] { "a" };

            provider.Add(method, arguments, c);

            IEnumerable<ICacheable> fromCache = provider.Get(typeof(EntityC), method, arguments);

            object realEntity = Convert.ChangeType(fromCache.First(), typeof(EntityC));

            Assert.AreEqual(1, fromCache.Count());
            Assert.AreEqual((realEntity as EntityC).EntityAEntry.Name, a.Name);
            Assert.AreEqual((realEntity as EntityC).EntityAEntry.EntityB, b);
            Assert.AreEqual((realEntity as EntityC).EntityAEntry.EntityB.EntityD, d);
        }


        [TestMethod]
        public void AddListOfObjectsWithSubObjectToCache()
        {
            EntityA a = CreateEntityA();
            EntityC c = CreateEntityC();
            c.EntityAEntry = a;

            var provider = new Carfac.Client.CacheProvider.CacheProvider();

            string method = "GetEntitiesThatContain";

            for (int i = 0; i < 100; i++)
            {
                provider.Add(method, new object[] { i }, c);
            }


            IEnumerable<ICacheable> fromCache = provider.Get(typeof(EntityC), method, new object[] { 0 });

            var realEntity = Convert.ChangeType(fromCache.First(), typeof(EntityC)) as EntityC;


            Assert.AreEqual(1, fromCache.Count());
            Assert.AreEqual(c, realEntity);
            Assert.AreEqual(a, realEntity.EntityAEntry);
        }

        [TestMethod]
        public void AddListOfObjectsWithListOfSubObjectToCache()
        {
            var provider = new Carfac.Client.CacheProvider.CacheProvider();

            string method = "GetEntitiesThatContain";

            for (int i = 0; i < 100; i++)
            {
                EntityC c = CreateEntityC();
                c.Id = i;

                for (int j = 0; j < 20; j++)
                {
                    c.EntityBList.Add(new EntityB
                    {
                        Id = j
                    });
                }
                provider.Add(method, new object[] { "a" }, c);
            }


            IEnumerable<ICacheable> fromCache = provider.Get(typeof(EntityC), method, new object[] { "a" });

            Assert.AreEqual(100, fromCache.Count());
            foreach (var cacheable in fromCache)
            {
                Assert.AreEqual((cacheable as EntityC).EntityBList.Count, 20);
            }

        }

        [TestMethod]
        public void AddObjectWithListOfSubObjects()
        {
            var provider = new Carfac.Client.CacheProvider.CacheProvider();
            string method = "GetEntitiesThatContain";
            var arguments = new object[] { "a" };

            EntityC c = CreateEntityC();
            for (int i = 0; i < 100; i++)
            {
                c.EntityBList.Add(new EntityB
                {
                    Id = i
                });
            }

            provider.Add(method, arguments, c);

            IEnumerable<ICacheable> fromCache = provider.Get(typeof(EntityC), method, arguments);

            var realEntity = Convert.ChangeType(fromCache.First(), typeof(EntityC)) as EntityC;


            Assert.AreEqual(1, fromCache.Count());
            Assert.AreEqual(c, realEntity);
            Assert.AreEqual(100, realEntity.EntityBList.Count);

        }

        [TestMethod]
        public void UpdateUnsavedCacheableInMemory()
        {
            var provider = new Carfac.Client.CacheProvider.CacheProvider();

            var dries = new Customer();
            dries.FirstName = "Dries";
            dries.LastName = "Hoebeke";
            dries.Id = 0;

            var kbc = new BankAccount
            {
                Bic = "d5f5d1",
                Iban = "qmsdklj"
            };
            var rabobank = new BankAccount
            {
                Bic = "oiuoiuoiu",
                Iban = "mkljmkljç"
            };
            dries.BankAccounts.Add(kbc);
            dries.BankAccounts.Add(rabobank);

            provider.Add("GetCustomer", new object[] { "Dries" }, dries);

            dries.LastName = "HoebekeEdited";
            var itemsUpdated = provider.Update(dries);

            Assert.AreEqual(0, itemsUpdated);
        }

        [TestMethod]
        public void UpdateSavedCacheableInMemory()
        {
            var provider = new Carfac.Client.CacheProvider.CacheProvider();

            var dries = new Customer();
            dries.FirstName = "Dries";
            dries.LastName = "Hoebeke";
            dries.Id = 0;

            var kbc = new BankAccount
            {
                Bic = "d5f5d1",
                Iban = "qmsdklj"
            };
            var rabobank = new BankAccount
            {
                Bic = "oiuoiuoiu",
                Iban = "mkljmkljç"
            };
            dries.BankAccounts.Add(kbc);
            dries.BankAccounts.Add(rabobank);

            provider.Add("GetCustomer", new object[] { "Dries" }, dries);
            provider.SaveCache();

            dries.LastName = "HoebekeEdited";
            var itemsUpdated = provider.Update(dries);

            Assert.AreEqual(1, itemsUpdated);
        }

        [TestMethod]
        public void DeleteUnsavedCacheableInMemory()
        {
            var provider = new Carfac.Client.CacheProvider.CacheProvider();

            var dries = new Customer();
            dries.FirstName = "Dries";
            dries.LastName = "Hoebeke";
            dries.Id = 0;

            var kbc = new BankAccount
            {
                Bic = "d5f5d1",
                Iban = "qmsdklj"
            };
            var rabobank = new BankAccount
            {
                Bic = "oiuoiuoiu",
                Iban = "mkljmkljç"
            };
            dries.BankAccounts.Add(kbc);
            dries.BankAccounts.Add(rabobank);

            provider.Add("GetCustomer", new object[] { "Dries" }, dries);

            var rowCount = provider.Delete(dries);

            Assert.AreEqual(rowCount, 1);

            var result = provider.Get(typeof(Customer), dries.CacheKey);
            Assert.AreEqual(result.Count(), 0);
        }

        [TestMethod]
        public void DeleteSavedCacheableInMemory()
        {
            var provider = new Carfac.Client.CacheProvider.CacheProvider();

            var dries = new Customer();
            dries.FirstName = "Dries";
            dries.LastName = "Hoebeke";
            dries.Id = 0;

            var kbc = new BankAccount
            {
                Bic = "d5f5d1",
                Iban = "qmsdklj"
            };
            var rabobank = new BankAccount
            {
                Bic = "oiuoiuoiu",
                Iban = "mkljmkljç"
            };
            dries.BankAccounts.Add(kbc);
            dries.BankAccounts.Add(rabobank);

            provider.Add("GetCustomer", new object[] { "Dries" }, dries);
            provider.SaveCache();

            var rowCount = provider.Delete(dries);
            Assert.AreEqual(rowCount, 1);

            provider.SaveCache();

            var result = provider.Get(typeof(Customer), dries.CacheKey);
            Assert.AreEqual(result.Count(),0);
        }

        [TestMethod]
        public void SaveToCacheAndClearCache()
        {
            var provider = new Carfac.Client.CacheProvider.CacheProvider();

            provider.ClearCache();

            var dries = new Customer();
            dries.FirstName = "Dries";
            dries.LastName = "Hoebeke";
            dries.Id = 0;

            var kbc = new BankAccount
            {
                Bic = "d5f5d1",
                Iban = "qmsdklj"
            };
            var rabobank = new BankAccount
            {
                Bic = "oiuoiuoiu",
                Iban = "mkljmkljç"
            };
            dries.BankAccounts.Add(kbc);
            dries.BankAccounts.Add(rabobank);

            provider.Add("GetCustomer", new object[] { "Dries" }, dries);

            var result = provider.Get(typeof(Customer), "GetCustomer", new object[] { "Dries" });
            Assert.AreEqual(result.Count(), 1);
            Assert.AreEqual((result.First() as Customer).BankAccounts.Count, 2);

            provider.SaveCache();

            provider.ClearCache();

            var resultAfterClear = provider.Get(typeof(Customer), "GetCustomer", new object[] { "Dries" });
            Assert.IsNull(resultAfterClear);

            provider.ClearCache();
            provider.ReinstallDataStore();
        }

        [TestMethod]
        public void LoadCacheFromDatabase()
        {
            var provider = new Carfac.Client.CacheProvider.CacheProvider();
            provider.ClearCache();

            var dries = new Customer();
            dries.FirstName = "Dries";
            dries.LastName = "Hoebeke";
            dries.Id = 0;

            var kbc = new BankAccount
            {
                Bic = "d5f5d1",
                Iban = "qmsdklj"
            };
            var rabobank = new BankAccount
            {
                Bic = "oiuoiuoiu",
                Iban = "mkljmkljç"
            };
            dries.BankAccounts.Add(kbc);
            dries.BankAccounts.Add(rabobank);

            provider.Add("GetCustomer", new object[] { "Dries" }, dries);

            provider.SaveCache();

            provider.ClearCacheInMemory();
            provider.LoadCacheFromDatabase();

            var result = provider.Get(typeof(Customer), "GetCustomer", new object[] { "Dries" });
            Assert.AreEqual(result.Count(), 1);
            Assert.AreEqual((result.First() as Customer).BankAccounts.Count, 2);

            provider.ClearCache();

            provider.ReinstallDataStore();
        }

        [TestMethod]
        public void SaveCacheTwice()
        {
            var provider = new Carfac.Client.CacheProvider.CacheProvider();
            provider.ClearCache();

            var dries = new Customer();
            dries.FirstName = "Dries";
            dries.LastName = "Hoebeke";
            dries.Id = 0;

            var kbc = new BankAccount
            {
                Bic = "d5f5d1",
                Iban = "qmsdklj"
            };
            var rabobank = new BankAccount
            {
                Bic = "oiuoiuoiu",
                Iban = "mkljmkljç"
            };
            dries.BankAccounts.Add(kbc);
            dries.BankAccounts.Add(rabobank);

            provider.Add("GetCustomer", new object[] { "Dries" }, dries);

            provider.SaveCache();

            provider.SaveCache();

            provider.ClearCache();
            provider.ReinstallDataStore();
        }

        [TestMethod]
        public void SaveSpeedTest()
        {
            var provider = new Carfac.Client.CacheProvider.CacheProvider();
            provider.ClearCache();
            provider.ReinstallDataStore();


            for (int i = 0; i < 2000; i++)
            {
                var dries = new Customer();
                dries.FirstName = "Dries";
                dries.LastName = "Hoebeke";
                dries.Id = i;

                var kbc = new BankAccount
                {
                    Bic = "d5f5d1" + i,
                    Iban = "qmsdklj"
                };
                var rabobank = new BankAccount
                {
                    Bic = "oiuoiuoiu" + i,
                    Iban = "mkljmkljç"
                };
                dries.BankAccounts.Add(kbc);
                dries.BankAccounts.Add(rabobank);

                provider.Add("GetCustomer", new object[] { "Dries" }, dries);
            }

            provider.SaveCache();

            provider.ClearCache();
            provider.ReinstallDataStore();
        }

        [TestMethod]
        public void SaveSpeedTestTwice()
        {
            var provider = new Carfac.Client.CacheProvider.CacheProvider();
            provider.ClearCache();
            provider.ReinstallDataStore();


            for (int i = 0; i < 2000; i++)
            {
                var dries = new Customer();
                dries.FirstName = "Dries";
                dries.LastName = "Hoebeke";
                dries.Id = i;

                var kbc = new BankAccount
                {
                    Bic = "d5f5d1" + i,
                    Iban = "qmsdklj"
                };
                var rabobank = new BankAccount
                {
                    Bic = "oiuoiuoiu" + i,
                    Iban = "mkljmkljç"
                };
                dries.BankAccounts.Add(kbc);
                dries.BankAccounts.Add(rabobank);

                provider.Add("GetCustomer", new object[] { "Dries" }, dries);
            }
            provider.SaveCache();
            provider.SaveCache();

            provider.ClearCache();
            provider.ReinstallDataStore();
        }

        #region Helpers

        private static readonly Random random = new Random();

        private EntityC CreateEntityC()
        {
            return new EntityC();
        }

        private EntityA CreateEntityA()
        {
            return new EntityA { Name = RandomString(255) };
        }

        private EntityB CreateEntityB()
        {
            return new EntityB();
        }

        private EntityD CreateEntityD()
        {
            return new EntityD();
        }

        private string RandomString(int size)
        {
            var builder = new StringBuilder(size);
            for (int i = 0; i < size; i++)
                builder.Append((char)random.Next(0x41, 0x5A));
            return builder.ToString();
        }

        #endregion
    }
}
