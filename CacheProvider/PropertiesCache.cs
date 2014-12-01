using System;
using System.Collections.Concurrent;
using System.Reflection;

namespace Carfac.Client.CacheProvider
{
    public class PropertiesCache
    {
        private readonly ConcurrentDictionary<Type, PropertyInfo[]> _propertyDictionary;

        public PropertiesCache()
        {
            _propertyDictionary = new ConcurrentDictionary<Type, PropertyInfo[]>();
        }

        /// <summary>
        /// Gets all the properties of the provided object. These properties are then saved to a dictionary.
        /// Subsequent requests will get the properties from the dictionary.
        /// </summary>
        /// <param name="objectToAnalyze">The object to analyze.</param>
        /// <returns></returns>
        /// <exception cref="System.Exception">properties found in property cache but could not get them from dictionary</exception>
        public PropertyInfo[] GetPropertyInfo(object objectToAnalyze)
        {
            if(objectToAnalyze == null)
                return new PropertyInfo[]{};

            PropertyInfo[] result;


            Type type = objectToAnalyze.GetType();
            if (_propertyDictionary.ContainsKey(type))
            {
                if (_propertyDictionary.TryGetValue(type, out result))
                {
                    return result;
                }

                throw new Exception("properties found in property cache but could not get them from dictionary");
            }

            result = type.GetProperties();
            if (_propertyDictionary.TryAdd(type, result))
            {
                return result;
            }


            return result;
        }
    }
}