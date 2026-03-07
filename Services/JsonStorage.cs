using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace KHSX.Services
{
    public static class JsonStorage
    {
        private static readonly string DataDirectory;
        private static readonly JsonSerializerOptions StandardOptions;

        static JsonStorage()
        {
            DataDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data");
            if (!Directory.Exists(DataDirectory))
            {
                Directory.CreateDirectory(DataDirectory);
            }

            string factoryDir = Path.Combine(DataDirectory, "factory");
            if (!Directory.Exists(factoryDir))
            {
                Directory.CreateDirectory(factoryDir);
            }

            StandardOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                Converters = { new JsonStringEnumConverter() },
                PropertyNameCaseInsensitive = true
            };
        }

        public static T Load<T>(string filename) where T : new()
        {
            string path = Path.Combine(DataDirectory, filename);
            if (!File.Exists(path))
            {
                return new T(); // Trả về dạng mặc định nếu file không tồn tại
            }

            try
            {
                string json = File.ReadAllText(path);
                return JsonSerializer.Deserialize<T>(json, StandardOptions) ?? new T();
            }
            catch
            {
               return new T();
            }
        }

        public static void Save<T>(string filename, T data)
        {
            string path = Path.Combine(DataDirectory, filename);
            try
            {
                string json = JsonSerializer.Serialize(data, StandardOptions);
                File.WriteAllText(path, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving JSON {filename}: {ex.Message}");
            }
        }
        
        // Xóa tất cả các file data (dùng trong test hoặc reset)
        public static void ClearAllData()
        {
             DirectoryInfo di = new DirectoryInfo(DataDirectory);
             foreach (FileInfo file in di.GetFiles())
             {
                 file.Delete(); 
             }
        }
    }
}
