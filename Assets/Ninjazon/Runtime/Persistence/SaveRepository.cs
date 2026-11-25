using System;
using System.IO;
using UnityEngine;

namespace Ninjazon.Runtime.Persistence
{
    public sealed class SaveRepository
    {
        private readonly string path;

        public SaveRepository(string path)
        {
            this.path = path ?? throw new ArgumentNullException(nameof(path));
        }

        public SaveDataV1 Load()
        {
            try
            {
                if (!File.Exists(path))
                {
                    return new SaveDataV1();
                }

                var data = JsonUtility.FromJson<SaveDataV1>(File.ReadAllText(path));
                return data == null || data.schemaVersion != 1 ? new SaveDataV1() : Sanitize(data);
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException || exception is ArgumentException)
            {
                return new SaveDataV1();
            }
        }

        public void Save(SaveDataV1 data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));

            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            var temporaryPath = path + ".tmp";
            File.WriteAllText(temporaryPath, JsonUtility.ToJson(Sanitize(data), true));

            if (File.Exists(path))
            {
                try
                {
                    File.Replace(temporaryPath, path, null);
                    return;
                }
                catch (PlatformNotSupportedException)
                {
                    File.Delete(path);
                }
            }

            File.Move(temporaryPath, path);
        }

        private static SaveDataV1 Sanitize(SaveDataV1 data)
        {
            data.schemaVersion = 1;
            data.musicVolume = Mathf.Clamp01(data.musicVolume);
            data.sfxVolume = Mathf.Clamp01(data.sfxVolume);
            data.bestScore = Mathf.Max(0, data.bestScore);
            data.bestDistance = Mathf.Max(0f, data.bestDistance);
            data.lifetimeCredits = Mathf.Max(0, data.lifetimeCredits);
            return data;
        }
    }
}

