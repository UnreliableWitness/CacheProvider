using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;

namespace Carfac.Client.CacheProvider
{
    /// <summary>
    /// Saves the cache residing in memory of the system to a local datastore.
    /// Provides CRUD methods to this local datastore.
    /// </summary>
    public class CachePersistor
    {
        private string _connectionString;

        public CachePersistor()
        {
            InstallCache();
        }

        #region Saving and updating
        /// <summary>
        /// Saves the cache to the datastore.
        /// </summary>
        /// <param name="container">The container to save.</param>
        public void SaveCache(CacheContainer container)
        {
            foreach (CacheArchive cacheArchive in container.CacheArchives)
            {
                SaveArchive(cacheArchive);
            }

            foreach (CacheArchive cacheArchive in container.CacheArchives)
            {
                SaveEntries(cacheArchive.Name, cacheArchive.CacheEntries);
            }
        }

        /// <summary>
        /// Inserts the archive.
        /// </summary>
        /// <param name="archive">The archive.</param>
        private void SaveArchive(CacheArchive archive)
        {
            using (var conn = new SQLiteConnection(_connectionString))
            {
                using (var cmd = new SQLiteCommand(conn))
                {
                    conn.Open();
                    cmd.CommandText = "select count(*) from \"archive\" where name = :name";
                    cmd.Parameters.AddWithValue("name", archive.Name);

                    object r = cmd.ExecuteScalar();
                    if (int.Parse(r.ToString()) == 0)
                    {
                        cmd.CommandText = "insert into \"archive\" (name) values (:name)";
                        cmd.ExecuteNonQuery();
                    }
                }
            }
        }

        /// <summary>
        /// Inserts the entries in the cache.
        /// Also inserts nested objects (when they are icacheable).
        /// Also inserts nested collections (when they are icacheable).
        /// </summary>
        /// <param name="name">The name.</param>
        /// <param name="cacheEntries">The cache entries.</param>
        private void SaveEntries(string name, IEnumerable<CacheEntry> cacheEntries)
        {
            using (var conn = new SQLiteConnection(_connectionString))
            {
                conn.Open();
                using (SQLiteTransaction transaction = conn.BeginTransaction())
                {
                    ProcessUnsaved(conn, name, cacheEntries);
                    ProcessAltered(conn, name, cacheEntries);
                    ProcessDeleted(conn, name, cacheEntries);

                    transaction.Commit();
                }
                conn.Close();
            }
        }

        private void ProcessDeleted(SQLiteConnection conn, string archiveName, IEnumerable<CacheEntry> cacheEntries)
        {
            using (var cmd = new SQLiteCommand(conn))
            {
                cmd.CommandText =
                    "delete from \"entry\" where archivename = :archivename and key = :key;";
                var an = cmd.CreateParameter();
                an.ParameterName = "archivename";
                var key = cmd.CreateParameter();
                key.ParameterName = "key";
                cmd.Parameters.AddRange(new SQLiteParameter[] { an, key });

                an.Value = archiveName;

                var toDelete = new List<CacheEntry>();
                
                //delete everything from the database
                foreach (var cacheEntry in cacheEntries.Where(p => p.Value.CacheState == CacheStateType.Deleted)
                    )
                {
                    key.Value = cacheEntry.Key;
                    cmd.ExecuteNonQuery();

                    DeleteCallers(conn, archiveName, cacheEntry.Key);
                    DeleteLinkedCacheCollection(conn, archiveName, cacheEntry.Key);
                    DeleteLinkedCacheEntries(conn, archiveName, cacheEntry.Key);

                    toDelete.Add(cacheEntry);
                }

                //delete from the memory as well
                foreach (var cacheEntry in toDelete)
                {
                    cacheEntries.ToList().Remove(cacheEntry);
                }
            }
        }

        private void ProcessAltered(SQLiteConnection conn, string typeName, IEnumerable<CacheEntry> cacheEntries)
        {
            using (var cmd = new SQLiteCommand(conn))
            {
                cmd.CommandText =
                    "update \"entry\" set entryvalue = :entryvalue where archivename = :archivename and key = :key;";
                var ev = cmd.CreateParameter();
                ev.ParameterName = "entryvalue";
                var an = cmd.CreateParameter();
                an.ParameterName = "archivename";
                var key = cmd.CreateParameter();
                key.ParameterName = "key";

                cmd.Parameters.AddRange(new SQLiteParameter[] { ev, an, key });

                an.Value = typeName;
                foreach (var cacheEntry in cacheEntries.Where(p => p.Value.CacheState == CacheStateType.Altered)
                    )
                {
                    ev.Value = Serialize(cacheEntry.Value);
                    key.Value = cacheEntry.Key;

                    cmd.ExecuteNonQuery();

                    DeleteCallers(conn, typeName, cacheEntry.Key);
                    InsertCallers(conn, typeName, cacheEntry.Key, cacheEntry.Callers);

                    DeleteLinkedCacheEntries(conn, typeName, cacheEntry.Key);
                    InsertLinkedCacheEntries(conn, typeName, cacheEntry.Key, cacheEntry.LinkedCacheEntries);

                    DeleteLinkedCacheCollection(conn, typeName, cacheEntry.Key);
                    InsertLinkedCacheEntriesCollection(conn, typeName, cacheEntry.Key, cacheEntry.LinkedCacheCollection);

                    cacheEntry.Value.CacheState = CacheStateType.Saved;
                    cacheEntry.Value.Persisted = DateTime.Now;
                }
            }
        }

        private void ProcessUnsaved(SQLiteConnection conn, string name, IEnumerable<CacheEntry> cacheEntries)
        {
            using (var cmd = new SQLiteCommand(conn))
            {
                cmd.CommandText =
                    "insert into \"entry\" (ARCHIVENAME,ENTRYVALUE,key) values (:archivename, :entryvalue, :key);";

                var archiveNameParam = cmd.CreateParameter();
                archiveNameParam.ParameterName = "archivename";
                var entryValueParam = cmd.CreateParameter();
                entryValueParam.ParameterName = "entryvalue";
                var keyParam = cmd.CreateParameter();
                keyParam.ParameterName = "key";

                cmd.Parameters.Add(archiveNameParam);
                cmd.Parameters.Add(entryValueParam);
                cmd.Parameters.Add(keyParam);

                foreach (var cacheEntry in cacheEntries.Where(p => p.Value.CacheState == CacheStateType.Unsaved)
                    )
                {
                    archiveNameParam.Value = name;
                    entryValueParam.Value = Serialize(cacheEntry.Value);
                    keyParam.Value = cacheEntry.Key;

                    cmd.ExecuteNonQuery();

                    cacheEntry.Value.CacheState = CacheStateType.Saved;
                    cacheEntry.Value.Persisted = DateTime.Now;

                    if (cacheEntry.Callers.Any())
                    {
                        InsertCallers(conn, name, cacheEntry.Key, cacheEntry.Callers);
                    }

                    if (cacheEntry.LinkedCacheEntries.Any())
                    //if there are any nested objects, save them
                    {
                        InsertLinkedCacheEntries(conn, name, cacheEntry.Key,
                                                                      cacheEntry.LinkedCacheEntries);
                    }

                    if (cacheEntry.LinkedCacheCollection.Any())
                    //if there are any nested collections, save them
                    {
                        InsertLinkedCacheEntriesCollection(conn, name, cacheEntry.Key,
                                                                                cacheEntry.LinkedCacheCollection);
                    }
                }
            }
        } 
        #endregion

        #region Delete Methods
        internal void EmptyDatabase()
        {
            using (var conn = new SQLiteConnection(_connectionString))
            {
                using (var cmd = new SQLiteCommand(conn))
                {
                    conn.Open();
                    cmd.CommandText =
                        "delete from archive; delete from callers; delete from entry; delete from linkedcollectionentries; delete from linkedentries;";
                    cmd.ExecuteNonQuery();
                }
            }
        }

        private void DeleteLinkedCacheCollection(SQLiteConnection conn, string typeName, string key)
        {
            using (var cmd = new SQLiteCommand(conn))
            {
                cmd.CommandText = "delete from linkedcollectionentries where rootarchivename = :rootarchivename and rootkey = :rootkey;";
                cmd.Parameters.AddWithValue("rootarchivename", typeName);
                cmd.Parameters.AddWithValue("rootkey", key);
                cmd.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// Deletes the nested icacheables from an entry.
        /// </summary>
        /// <param name="conn">The connection</param>
        /// <param name="archiveName"></param>
        /// <param name="key"></param>
        private void DeleteLinkedCacheEntries(SQLiteConnection conn, string archiveName, string key)
        {
            using (var cmd = new SQLiteCommand(conn))
            {
                cmd.CommandText = "delete from linkedentries where rootarchivename = :rootarchivename and rootkey = :rootkey;";
                cmd.Parameters.AddWithValue("rootarchivename", archiveName);
                cmd.Parameters.AddWithValue("rootkey", key);
                cmd.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// Deletes the caller methods of an entry.
        /// </summary>
        /// <param name="conn">The connection</param>
        /// <param name="typeName"></param>
        /// <param name="key"></param>
        private void DeleteCallers(SQLiteConnection conn, string typeName, string key)
        {
            using (var cmd = new SQLiteCommand(conn))
            {
                cmd.CommandText = "delete from callers where archivename = :archivename and key = :key";
                cmd.Parameters.AddWithValue("archivename", typeName);
                cmd.Parameters.AddWithValue("key", key);
                cmd.ExecuteNonQuery();
            }
        } 
        #endregion

        #region Insert Cache Meta info
        /// <summary>
        /// Inserts the linked cache entries collection.
        /// These are nested collections of icacheables.
        /// </summary>
        /// <param name="conn"></param>
        /// <param name="archiveName"></param>
        /// <param name="key"></param>
        /// <param name="concurrentDictionary">The concurrent dictionary.</param>
        private void InsertLinkedCacheEntriesCollection(SQLiteConnection conn, string archiveName, string key, ConcurrentDictionary<Type, IEnumerable<string>> concurrentDictionary)
        {
            using (var cmd = new SQLiteCommand(conn))
            {
                cmd.CommandText =
                    "insert into \"linkedcollectionentries\" (rootarchivename, rootkey, childkey, childarchivename) values (:rootarchivename, :rootkey, :childkey, :childarchivename);";
                var ra = cmd.CreateParameter();
                var rk = cmd.CreateParameter();
                var ca = cmd.CreateParameter();
                var ck = cmd.CreateParameter();
                ra.ParameterName = "rootarchivename";
                rk.ParameterName = "rootkey";
                ca.ParameterName = "childarchivename";
                ck.ParameterName = "childkey";
                cmd.Parameters.AddRange(new SQLiteParameter[] { ra, rk, ca, ck });

                ra.Value = archiveName;
                rk.Value = key;

                foreach (var linkedCollection in concurrentDictionary)
                {
                    Type type = linkedCollection.Key;
                    foreach (string collection in linkedCollection.Value)
                    {
                        ck.Value = collection;
                        ca.Value = type.AssemblyQualifiedName;

                        cmd.ExecuteNonQuery();
                    }
                }
            }
        }

        /// <summary>
        /// Inserts the linked cache entries.
        /// </summary>
        /// <param name="conn">The connection.</param>
        /// <param name="archiveName"></param>
        /// <param name="key"></param>
        /// <param name="concurrentDictionary">The concurrent dictionary.</param>
        private void InsertLinkedCacheEntries(SQLiteConnection conn, string archiveName, string key, ConcurrentDictionary<Type, string> concurrentDictionary)
        {
            using (var cmd = new SQLiteCommand(conn))
            {
                cmd.CommandText =
                    "insert into \"linkedentries\" (rootarchivename, rootkey, childarchivename,childkey) values (:rootarchivename, :rootkey, :childarchivename, :childkey);";
                var ra = cmd.CreateParameter();
                ra.ParameterName = "rootarchivename";
                var rk = cmd.CreateParameter();
                rk.ParameterName = "rootkey";
                var ca = cmd.CreateParameter();
                ca.ParameterName = "childarchivename";
                var ck = cmd.CreateParameter();
                ck.ParameterName = "childkey";

                cmd.Parameters.AddRange(new SQLiteParameter[] { ra, rk, ca, ck });

                ra.Value = archiveName;
                rk.Value = key;

                foreach (var linkedCacheEntry in concurrentDictionary)
                {
                    ca.Value = linkedCacheEntry.Key.AssemblyQualifiedName;
                    ck.Value = linkedCacheEntry.Value;

                    cmd.ExecuteNonQuery();
                }
            }
        }

        /// <summary>
        /// Inserts the caller methods.
        /// </summary>
        /// <param name="conn">The connection</param>
        /// <param name="archiveName"></param>
        /// <param name="key"></param>
        /// <param name="callers">The callers.</param>
        private void InsertCallers(SQLiteConnection conn, string archiveName, string key, IEnumerable<string> callers)
        {
            using (var cmd = new SQLiteCommand(conn))
            {
                cmd.CommandText =
                    "insert into \"callers\" (caller, archivename, key) values (:caller,:archivename, :key);";
                var callerParam = cmd.CreateParameter();
                callerParam.ParameterName = "caller";
                var archiveNameParam = cmd.CreateParameter();
                archiveNameParam.ParameterName = "archivename";
                var keyParam = cmd.CreateParameter();
                keyParam.ParameterName = "key";

                cmd.Parameters.Add(callerParam);
                cmd.Parameters.Add(archiveNameParam);
                cmd.Parameters.Add(keyParam);

                archiveNameParam.Value = archiveName;
                keyParam.Value = key;
                foreach (string caller in callers)
                {
                    callerParam.Value = caller;

                    cmd.ExecuteNonQuery();
                }
            }
        } 
        #endregion

        #region Utility methods
        /// <summary>
        /// Gets the latest id.
        /// Use this to get the id of the record you have just inserted.
        /// </summary>
        /// <param name="tableName">Name of the table.</param>
        /// <param name="conn">The connection.</param>
        /// <returns></returns>
        private int GetLatestId(string tableName, SQLiteConnection conn)
        {
            int result = 0;
            using (var cmd = new SQLiteCommand(conn))
            {
                cmd.CommandText = "select max(id) from " + tableName;
                result = int.Parse(cmd.ExecuteScalar().ToString());
            }
            return result;
        }

        /// <summary>
        /// Serializes the specified ICacheable.
        /// </summary>
        /// <param name="obj">The obj.</param>
        /// <returns></returns>
        private byte[] Serialize(ICacheable obj)
        {
            var bformatter = new BinaryFormatter();
            var stream = new MemoryStream();
            bformatter.Serialize(stream, obj);
            return stream.ToArray();
        }

        /// <summary>
        /// Deserializes the specified byte array.
        /// </summary>
        /// <param name="bytes">The bytes.</param>
        /// <returns></returns>
        private object Deserialize(byte[] bytes)
        {
            var bformatter = new BinaryFormatter();
            return bformatter.Deserialize(new MemoryStream(bytes));
        } 
        #endregion

        #region Installation methods
        private string GetDataStoreDestinationFolder()
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CarfacCache");
        }

        private void InstallCache()
        {
            var destination = GetDataStoreDestinationFolder();
            if (!Directory.Exists(destination))
            {
                Directory.CreateDirectory(destination);
            }

            destination = Path.Combine(destination, "Cache.db");

            if (!File.Exists(destination))
            {
                var cacheStoreStream =
                    Assembly.GetExecutingAssembly().GetManifestResourceStream("Carfac.Client.CacheProvider.Cache.db");
                using (var fs = File.Create(destination))
                {
                    if (cacheStoreStream != null)
                    {
                        byte[] bytes = new byte[cacheStoreStream.Length];
                        cacheStoreStream.Read(bytes, 0, (int)cacheStoreStream.Length);
                        fs.Write(bytes, 0, bytes.Length);
                    }
                    fs.Close();
                    if (cacheStoreStream != null) cacheStoreStream.Close();
                }
            }


            //string[] auxList = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceNames();

            if (!File.Exists(destination))
                throw new FileNotFoundException(string.Format("Cache db not found at {0}", destination));

            TestConnection(destination);
        }


        internal void ReinstallDataStore()
        {
            var destination = GetDataStoreDestinationFolder();
            destination = Path.Combine(destination, "Cache.db");

            if (File.Exists(destination))
                File.Delete(destination);

            InstallCache();
        }

        /// <summary>
        /// Tests the connection.
        /// Does this by opening a connection to the given path.
        /// </summary>
        /// <param name="path">The path.</param>
        public void TestConnection(string path)
        {
            _connectionString = string.Format("Data Source={0}", path);
            using (var conn = new SQLiteConnection(_connectionString))
            {
                try
                {
                    conn.Open();
                }
                catch (Exception)
                {
                    throw;
                }
                finally
                {
                }
            }
        }
        #endregion

        #region Load from database
        public List<CacheArchive> LoadArchivesFromDatabase()
        {
            var result = new List<CacheArchive>();

            using (var conn = new SQLiteConnection(_connectionString))
            {
                using (var cmd = new SQLiteCommand(conn))
                {
                    conn.Open();

                    cmd.CommandText = "select * from archive";
                    SQLiteDataReader r = cmd.ExecuteReader();

                    while (r.Read())
                    {
                        var newArchive = new CacheArchive(r["name"].ToString());
                        result.Add(newArchive);
                    }

                    r.Dispose();
                }
            }

            return result;
        }

        public void LoadEntriesFromDatabase(List<CacheArchive> archives)
        {
            using (var conn = new SQLiteConnection(_connectionString))
            {
                foreach (CacheArchive cacheArchive in archives)
                {
                    using (var cmd = new SQLiteCommand(conn))
                    {
                        if (conn.State != ConnectionState.Open)
                            conn.Open();

                        cmd.Parameters.Clear();
                        cmd.CommandText = "select * from entry where archivename = :archivename";
                        cmd.Parameters.AddWithValue("archivename", cacheArchive.Name);

                        SQLiteDataReader r = cmd.ExecuteReader();
                        while (r.Read())
                        {
                            var newEntry = new CacheEntry();
                            newEntry.Id = int.Parse(r["id"].ToString());
                            newEntry.Key = r["key"].ToString();
                            newEntry.Value = Deserialize((byte[])r["entryvalue"]) as ICacheable;
                            try
                            {
                                newEntry.Callers = GetCallersForEntry(conn, cacheArchive.Name, newEntry.Key);
                                newEntry.LinkedCacheEntries = GetLinkedCacheEntries(conn, cacheArchive.Name,
                                                                                    newEntry.Key);
                                newEntry.LinkedCacheCollection = GetLinkedCacheCollection(conn, cacheArchive.Name,
                                                                                          newEntry.Key);
                            }
                            catch (Exception)
                            {
                                throw;
                            }

                            cacheArchive.CacheEntries.Push(newEntry);
                        }
                        r.Dispose();
                    }
                }
            }
        } 

        private ConcurrentDictionary<Type, IEnumerable<string>> GetLinkedCacheCollection(SQLiteConnection conn,
                                                                                         string archiveName,
                                                                                         string key)
        {
            var result = new ConcurrentDictionary<Type, IEnumerable<string>>();

            using (var cmd = new SQLiteCommand(conn))
            {
                cmd.CommandText =
                    "select childarchivename, childkey from linkedcollectionentries where rootarchivename = :archivename and rootkey = :key;";
                cmd.Parameters.AddWithValue("archivename", archiveName);
                cmd.Parameters.AddWithValue("key", key);

                SQLiteDataReader r = cmd.ExecuteReader();
                while (r.Read())
                {
                    Type type = Type.GetType(r["childarchivename"].ToString());
                    if (result.ContainsKey(type))
                    {
                        IEnumerable<string> found;
                        result.TryGetValue(type, out found);
                        if (found != null)
                        {
                            var foundList = found.ToList();
                            foundList.Add(r["childkey"].ToString());
                            result.TryUpdate(type, foundList, found);
                        }
                    }
                    else
                    {
                        var keys = new List<string>();
                        keys.Add(r["childkey"].ToString());
                        result.TryAdd(type, keys);
                    }
                }
                r.Dispose();
            }

            return result;
        }

        private ConcurrentDictionary<Type, string> GetLinkedCacheEntries(SQLiteConnection conn, string archiveName,
                                                                         string key)
        {
            var result = new ConcurrentDictionary<Type, string>();

            using (var cmd = new SQLiteCommand(conn))
            {
                cmd.CommandText =
                    "select childarchivename, childkey from linkedentries where rootarchivename = :archivename and rootkey = :rootkey;";
                cmd.Parameters.AddWithValue("archivename", archiveName);
                cmd.Parameters.AddWithValue("rootkey", key);

                SQLiteDataReader r = cmd.ExecuteReader();
                while (r.Read())
                {
                    result.TryAdd(Type.GetType(r["childarchivename"].ToString()), r["childkey"].ToString());
                }
                r.Dispose();
            }

            return result;
        }

        private ConcurrentStack<string> GetCallersForEntry(SQLiteConnection conn, string archiveName, string key)
        {
            var result = new ConcurrentStack<string>();

            using (var cmd = new SQLiteCommand(conn))
            {
                cmd.CommandText = "select * from callers where archivename = :archivename and key = :key";
                cmd.Parameters.AddWithValue("archivename", archiveName);
                cmd.Parameters.AddWithValue("key", key);

                SQLiteDataReader r = cmd.ExecuteReader();
                while (r.Read())
                {
                    result.Push(r["caller"].ToString());
                }
                r.Dispose();
            }
            return result;
        }
        #endregion
    }
}