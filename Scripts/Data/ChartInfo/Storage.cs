using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Xml.Serialization;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.Events;

namespace JANOARG.Shared.Data.ChartInfo
{

    [XmlInclude(typeof(CollectionProxy))]
    public abstract class SerializeProxyList
    {
        [XmlElement("Item")]
        public List<SerializeProxy> Items = new List<SerializeProxy>();
    }

    public class SerializeProxy
    {
        [XmlAttribute]
        public string Key;
        public object Value;
        public static implicit operator SerializeProxy(KeyValuePair<string, object> item)
        {
            SerializeProxy proxy = new SerializeProxy
            {
                Key = item.Key,
                Value = item.Value,
            };
            if (proxy.Value is Array) proxy.Value = new CollectionProxy(proxy.Value as Array);
            return proxy;
        }

        public static implicit operator KeyValuePair<string, object>(SerializeProxy item)
        {
            object value = item.Value;
            if (value is CollectionProxy) value = ((CollectionProxy)value).Value;
            KeyValuePair<string, object> pair = new KeyValuePair<string, object>(item.Key, value);
            return pair;
        }

        public void AddPair(Dictionary<string, object> dict)
        {
            KeyValuePair<string, object> pair = this;
            dict.TryAdd(pair.Key, pair.Value);
        }
    }

    public class CollectionProxy
    {
        [XmlElement("Item")]
        public object[] Value;

        public CollectionProxy()
        {
        }

        public CollectionProxy(Array array)
        {
            Value = new object[array.Length];
            for (int a = 0; a < array.Length; a++) Value[a] = array.GetValue(a);
        }
    }
    
    public class Storage<TStore> where TStore : SerializeProxyList, new()
    {
        public Dictionary<string, object> values = new Dictionary<string, object>();
        public string                     SaveName;

        public Storage(string path)
        {
            SaveName = Application.persistentDataPath + "/" + path;
            Load();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Log(string message)                    => Debug.Log($"[Storage] {message}");
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void LogWarning(string message)             => Debug.LogWarning($"[Storage] {message}");
        [MethodImpl(MethodImplOptions.AggressiveInlining)] [AssertionMethod]
        private void Assert(bool condition, string message) => Debug.Assert(condition, $"[Storage] {message}");

        public T Get<T>(string key, T fallback)
        {
            try 
            {
                if (values.ContainsKey(key)) 
                {
                    return (T)values[key];
                }
                
                LogWarning( $"Key not found: {key} ({typeof(T)}), reverting to fallback of {fallback}");
                return fallback;
            }
            catch (InvalidCastException e)
            {
                LogWarning($"Key not found: {key} ({typeof(T)}) due to exception \"{e.Message}\", reverting to fallback of {fallback}");
                return fallback;
            }
        }
        public T[] Get<T>(string key, T[] fallback)
        {
            try 
            {
                if (values.ContainsKey(key)) 
                {
                    if (values[key] is object[]) return ((object[])values[key]).OfType<T>().ToArray();
                    return (T[])values[key];
                }
                
                LogWarning( $"Key not found: {key} ({typeof(T)}), reverting to fallback of {fallback}");
                return fallback;
            }
            catch (InvalidCastException e)
            {
                LogWarning($"Key not found: {key} ({typeof(T)}) due to exception \"{e.Message}\", reverting to fallback of {fallback}");
                return fallback;
            }
        }

        public void Set(string key, object value)
        {
            if (values.ContainsKey(key))
                Log($"Overwriting existing key {key} with value {value}");
            else
                Log($"Adding new key {key} as {value}");
            values[key] = value;
        }

        public UnityEvent OnSave = new UnityEvent();

        public UnityEvent OnLoad = new UnityEvent();

        public void Save()
        {
            TStore list = new TStore();
            foreach (KeyValuePair<string, object> pair in values)
                if (pair.Value != null) 
                    list.Items.Add(pair);
                else
                    LogWarning($"Tried to save null value for key {pair.Key}. Skipping.");

            XmlSerializer serializer = new XmlSerializer(typeof(TStore));
            FileStream fs;

            fs = new FileStream(SaveName + ".jas", FileMode.Create);
            serializer.Serialize(fs, list);
            Log($"Saved to {SaveName}.jas");
            fs.Close();
            fs = new FileStream(SaveName + ".backup.jas", FileMode.Create);
            serializer.Serialize(fs, list);
            Log($"Saved backup to {SaveName}.backup.jas");
            fs.Close();

            OnSave.Invoke();
        }

        public void Load()
        {
            TStore list = new TStore();

            XmlSerializer serializer = new XmlSerializer(typeof(TStore));
            FileStream fs = null;
            try 
            {
                Log($"Loading {SaveName}.jas");
                fs = new FileStream(SaveName + ".jas", FileMode.OpenOrCreate);
                Assert(fs != null,  "FileStream should never be null after successful creation");
                list = (TStore)serializer.Deserialize(fs);
                Debug.LogAssertion($"Fetched {list.Items.Count} from {SaveName}.jas deserialisation. \n {ListDeserialised()}");
                fs.Close();

                string ListDeserialised()
                {
                    string str = "";
                    foreach (var i in list.Items)
                    {
                        str += i.Key + " : " + i.Value + "\n";
                    }
                    return str;
                }
            }
            catch (Exception e)
            {
                try 
                {
                    fs?.Close();
                    fs = new FileStream(SaveName + ".backup.jas", FileMode.OpenOrCreate);
                    list = (TStore)serializer.Deserialize(fs);
                    fs.Close();
                }
                catch (Exception ee)
                {
                    fs?.Close();
                    Debug.Log(e + "\n" + ee);
                }
            }

            foreach (SerializeProxy pair in list.Items) pair.AddPair(values);

            OnLoad.Invoke();
        }
    }
}
