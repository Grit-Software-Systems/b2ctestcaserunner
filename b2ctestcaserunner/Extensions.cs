using System.Collections.Generic;
using System.Dynamic;
using System.Text.Json;

namespace Tools
{
    public static class ExpandoObject_Extensions
    {
        /// <summary>
        /// convert an ExpandoObject to an IDictionary<string,object>
        /// </summary>
        /// <param name="eo"></param>
        /// <returns>an IDictionary</returns>
        public static IDictionary<string, object> ToDictionary(this ExpandoObject eo)
        {
            return eo as IDictionary<string,object>;
        }


        public static string ToJson(this ExpandoObject eo)
        {
            return JsonSerializer.Serialize(eo);
        }


        /// <summary>
        /// convert a json string into an IDictionary<string,object>
        /// </summary>
        /// <param name="json"></param>
        /// <returns>IDictionary<string,object></returns>
        public static IDictionary<string, object> ToDictionary(this string json)
        {
            ExpandoObject eo = JsonSerializer.Deserialize<ExpandoObject>(json);
            return eo.ToDictionary();
        }


        public static ExpandoObject ToExpandoObject(this string json)
        {
            return JsonSerializer.Deserialize<ExpandoObject>(json);
        }


        public static ExpandoObject[] ToExpandoObjectArray(this string json)
        {
            return JsonSerializer.Deserialize<ExpandoObject[]>(json);
        }


        /// <summary>
        /// update or insert key value pair
        /// </summary>
        /// <param name="dictionary"></param>
        /// <param name="key"></param>
        /// <param name="value"></param>
        public static void Upsert(this IDictionary<string,object> dictionary, string key, string value)
        {
            if(dictionary.ContainsKey(key))
            {
                dictionary[key] = value;
            }
            else
            {
                dictionary.Add(key, value);
            }
        }
    }   // class Extensions
}