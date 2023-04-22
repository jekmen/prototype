using System.Collections;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Serialization;
using UnityEngine;
using UnityEngine.Networking;

namespace SpaceAI.DataManagment
{
    public static class SA_FileManager
    {
        private const string baseDir = "Data";
        private const string configDir = "Configurations";

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
                Debug.LogError($"Xml file doesn't exist in: {file}");
                return default;
            }
        }

        public static void SaveXmlConfigUnityOnly<T>(T obj, string fileName)
        {
            string path = Path.Combine(Application.streamingAssetsPath, configDir);
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

        public static async Task<T> LoadXmlConfigUnityOnlyAsync<T>(string fileName)
        {
            T result;
            string path = Path.Combine(Application.streamingAssetsPath, configDir);
            string file = Path.Combine(path, fileName + ".xml");

            if (Directory.Exists(path) && File.Exists(file))
            {
                XmlSerializer ser = new XmlSerializer(typeof(T));
                using (FileStream fileStream = new FileStream(file, FileMode.Open))
                {
                    result = (T)await Task.Run(() => ser.Deserialize(fileStream));
                }

                return result;
            }
            else
            {
                Debug.LogError($"Xml file doesn't exist in: {file}");
                return default;
            }
        }

        public static T LoadXmlConfigUnityOnly<T>(string fileName)
        {
            T result;
            string path = Path.Combine(Application.streamingAssetsPath, configDir);
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
                Debug.LogError($"Xml file doesn't exist in: {file}");
                return default;
            }
        }

        public static IEnumerator CopyShipTemplateFile(string fileName)
        {
            string streamingAssetsPath = Application.streamingAssetsPath;
            string persistentDataPath = Application.persistentDataPath;

            string sourcePath = Path.Combine(streamingAssetsPath + $"/{configDir}", fileName + ".xml");
            string destinationPath = Path.Combine(persistentDataPath, baseDir);
            string destinationPathToFile = Path.Combine(destinationPath, fileName + ".xml");

            if (File.Exists(destinationPathToFile)) yield break;

            using (UnityWebRequest www = UnityWebRequest.Get(sourcePath))
            {
                yield return www.SendWebRequest();

                if (www.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError("Failed to read file from StreamingAssets: " + www.error);
                    yield break;
                }

                byte[] bytes = www.downloadHandler.data;

                File.WriteAllBytes(destinationPathToFile, bytes);
            }

            Debug.Log($"File copied from {sourcePath} to {destinationPathToFile}");
        }
    }
}