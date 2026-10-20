using System;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Text;

namespace ChatGPTLite
{
    internal static class JsonStore
    {
        private static readonly object Sync = new object();

        public static T Load<T>(string path) where T : class
        {
            lock (Sync)
            {
                if (!File.Exists(path))
                    return null;

                using (FileStream stream = File.OpenRead(path))
                {
                    DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(T));
                    return serializer.ReadObject(stream) as T;
                }
            }
        }

        public static void Save<T>(string path, T value)
        {
            lock (Sync)
            {
                string temporaryPath = path + ".tmp";
                using (FileStream stream = File.Create(temporaryPath))
                {
                    DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(T));
                    serializer.WriteObject(stream, value);
                    stream.Flush(true);
                }

                if (File.Exists(path))
                {
                    string backupPath = path + ".bak";
                    File.Replace(temporaryPath, path, backupPath, true);
                }
                else
                {
                    File.Move(temporaryPath, path);
                }
            }
        }

        public static T FromJson<T>(string json) where T : class
        {
            byte[] bytes = Encoding.UTF8.GetBytes(json);
            using (MemoryStream stream = new MemoryStream(bytes))
            {
                DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(T));
                return serializer.ReadObject(stream) as T;
            }
        }
    }
}
