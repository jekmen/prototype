using System.IO;
using System.Threading;
using System.Xml.Serialization;
using UnityEngine;

namespace SpaceAI.DataManagment
{
    public static class SA_FileManager
    {
        private const string baseDir = "Data";

        /// <summary>
        /// Save file
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="obj"></param>
        /// <param name="fileName"></param>
        public static void SaveXml<T>(T obj, string fileName)
        {
            string path = Path.Combine(Application.persistentDataPath, baseDir);
            string file = Path.Combine(path, fileName + ".xml");
            Debug.Log(file);
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
                XmlSerializer ser = new XmlSerializer(typeof(T));
                FileStream fileStream = new FileStream(file, FileMode.Create);
                ser.Serialize(fileStream, obj);
                fileStream.Close();
            }
            else
            {
                XmlSerializer ser = new XmlSerializer(typeof(T));
                FileStream fileStream = new FileStream(file, FileMode.Create);
                ser.Serialize(fileStream, obj);
                fileStream.Close();
            }
        }

        /// <summary>
        /// Load file
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="fileName"></param>
        /// <returns></returns>
        public static T LoadXml<T>(string fileName)
        {
            T result;
            string path = Path.Combine(Application.persistentDataPath, baseDir);
            string file = Path.Combine(path, fileName + ".xml");

            if (Directory.Exists(path) && File.Exists(file))
            {
                XmlSerializer ser = new XmlSerializer(typeof(T));
                using (FileStream fileStream = new FileStream(file, FileMode.Open))
                {
                    result = (T)ser.Deserialize(fileStream);
                }
                return result;
            }
            else
            {
                Debug.LogError("Xml file doesn't exist in: " + file);
                return default;
            }
        }

        public static void SaveXmlConfig<T>(T obj, string fileName)
        {
            string path = Path.Combine(Application.streamingAssetsPath, "Configurations");
            string file = Path.Combine(path, fileName + ".xml");

            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
                XmlSerializer ser = new XmlSerializer(typeof(T));
                FileStream fileStream = new FileStream(file, FileMode.Create);
                ser.Serialize(fileStream, obj);
                fileStream.Close();
            }
            else
            {
                XmlSerializer ser = new XmlSerializer(typeof(T));
                FileStream fileStream = new FileStream(file, FileMode.Create);
                ser.Serialize(fileStream, obj);
                fileStream.Close();
            }
        }

        public static T LoadXmlConfig<T>(string fileName)
        {
            T result;
            string path = Path.Combine(Application.streamingAssetsPath, "Configurations");
            string file = Path.Combine(path, fileName + ".xml");

            if (Directory.Exists(path) && File.Exists(file))
            {
                XmlSerializer ser = new XmlSerializer(typeof(T));
                using (FileStream fileStream = new FileStream(file, FileMode.Open))
                {
                    result = (T)ser.Deserialize(fileStream);
                }

                return result;
            }
            else
            {
                Debug.LogError("Xml file doesn't exist in: " + file);
                return default;
            }
        }
    }
}
