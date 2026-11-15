using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dekotube.Services;

namespace Dekotube.Services
{
    /// <summary>
    /// Модель профиля аватара для сохранения конфигурации.
    /// Содержит пути к изображениям и настройки, но не BitmapImage объекты.
    /// </summary>
    public class AvatarProfile
    {
        public List<AvatarEmotion> Emotions { get; set; } = new();
        public string SelectedEmotionId { get; set; } = "";
        public string MouthOpenEyesOpenPath { get; set; } = "";
        public string MouthOpenEyesClosedPath { get; set; } = "";
        public string MouthClosedEyesOpenPath { get; set; } = "";
        public string MouthClosedEyesClosedPath { get; set; } = "";
        [JsonInclude]
        public string FormatVersion { get; set; } = "1.0";

        [JsonInclude]
        public string ProfileName { get; set; } = "";

        [JsonInclude]
        public string BaseImagePath { get; set; } = "";

        [JsonInclude]
        public string LeftEyeImagePath { get; set; } = "";

        [JsonInclude]
        public string RightEyeImagePath { get; set; } = "";

        [JsonInclude]
        public string LeftPupilImagePath { get; set; } = "";

        [JsonInclude]
        public string RightPupilImagePath { get; set; } = "";

        [JsonInclude]
        public string BlinkImagePath { get; set; } = "";

        [JsonInclude]
        public string MouthImagePath { get; set; } = "";

        [JsonInclude]
        public float TalkThreshold { get; set; } = 20f;
        public double MicrophoneSensitivity { get; set; } = 1;
        public VoiceCalibration VoiceCalibration { get; set; } = new();
        public int MouthCloseDelayMs { get; set; } = 200;

        [JsonInclude]
        public bool IsMicrophoneMonitoring { get; set; } = false;

        [JsonInclude]
        public string SelectedDeviceId { get; set; } = "";

        [JsonInclude]
        public string SceneBackgroundColor { get; set; } = "#00FF00";
        public List<string> RecentBackgroundColors { get; set; } = new();
        public int PupilSettingsVersion { get; set; }
        public bool AutoHideInterface { get; set; } = true;
        public int InterfaceIdleSeconds { get; set; } = 8;

        // Eye tracking settings
        [JsonInclude]
        public float LeftEyeCenterX { get; set; } = 0.5f;

        [JsonInclude]
        public float LeftEyeCenterY { get; set; } = 0.5f;

        [JsonInclude]
        public float LeftEyeWidth { get; set; } = 0.8f;

        [JsonInclude]
        public float LeftEyeHeight { get; set; } = 0.8f;

        [JsonInclude]
        public float LeftMaxDisplacement { get; set; } = 0.3f;

        [JsonInclude]
        public float RightEyeCenterX { get; set; } = 0.5f;

        [JsonInclude]
        public float RightEyeCenterY { get; set; } = 0.5f;

        [JsonInclude]
        public float RightEyeWidth { get; set; } = 0.8f;

        [JsonInclude]
        public float RightEyeHeight { get; set; } = 0.8f;

        [JsonInclude]
        public float RightMaxDisplacement { get; set; } = 0.3f;
        public float PupilSize { get; set; } = 28f;
        public string HandsIdleImagePath { get; set; } = "";
        public string HandsActiveImagePath { get; set; } = "";
        public string HandsInputMode { get; set; } = "auto";

        // Blink settings
        [JsonInclude]
        public bool BlinkEnabled { get; set; } = true;

        [JsonInclude]
        public int MinBlinkIntervalMs { get; set; } = 3000;

        [JsonInclude]
        public int MaxBlinkIntervalMs { get; set; } = 8000;

        [JsonInclude]
        public int BlinkCloseDurationMs { get; set; } = 150;
    }

    /// <summary>
    /// Сервис для управления профилями аватара через JSON-конфигурацию.
    /// Хранит профили в папке профилей приложения AppData.
    /// </summary>
    public static class ConfigurationService
    {
        // Папка профилей находится в %APPDATA%\VeadotubeMy\Profiles (совместимость с прежними профилями)
        private static readonly string ProfilesDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "VeadotubeMy", "Profiles");

        // Путь к файлу профиля по умолчанию
        private static readonly string DefaultProfilePath = Path.Combine(ProfilesDirectory, "default.json");

        static ConfigurationService()
        {
            // Создаем папку при инициализации, если её нет
            if (!Directory.Exists(ProfilesDirectory))
            {
                try
                {
                    Directory.CreateDirectory(ProfilesDirectory);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to create profiles directory: {ex}");
                }
            }
        }

        /// <summary>
        /// Сохраняет профиль аватара в файл JSON.
        /// </summary>
        /// <param name="profile">Профиль для сохранения (пути к изображениям должны быть заполнены)</param>
        /// <returns>Имя сохранённого профиля или пустую строку в случае ошибки</returns>
        public static string SaveProfile(AvatarProfile profile)
        {
            if (profile == null)
            {
                return "";
            }

            try
            {
                // Убеждаемся, что папка существует
                if (!Directory.Exists(ProfilesDirectory))
                {
                    Directory.CreateDirectory(ProfilesDirectory);
                }

                // Генерируем имя профиля: если имя пустое, используем timestamp
                string fileName = string.IsNullOrEmpty(profile.ProfileName)
                    ? $"profile_{DateTime.Now:yyyyMMdd_HHmmss}.json"
                    : $"{profile.ProfileName}.json";

                string filePath = Path.Combine(ProfilesDirectory, fileName);

                // Сериализуем в JSON с опциями для читаемости
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    DefaultIgnoreCondition = JsonIgnoreCondition.Never,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };

                string json = JsonSerializer.Serialize(profile, options);
                var temporaryPath = filePath + ".tmp";
                File.WriteAllText(temporaryPath, json);
                File.Move(temporaryPath, filePath, overwrite: true);

                return fileName;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SaveProfile error: {ex}");
                return "";
            }
        }

        /// <summary>
        /// Загружает профиль аватара из файла JSON.
        /// </summary>
        /// <param name="profileName">Имя профиля (без расширения .json)</param>
        /// <returns>Загруженный профиль или null в случае ошибки</returns>
        public static AvatarProfile? LoadProfile(string profileName)
        {
            if (string.IsNullOrEmpty(profileName))
            {
                return LoadDefaultProfile();
            }

            try
            {
                // Сначала пробуем загрузить как пользовательский профиль
                string userProfilePath = Path.Combine(ProfilesDirectory, $"{profileName}.json");
                if (File.Exists(userProfilePath))
                {
                    return LoadProfileFromPath(userProfilePath);
                }

                // Если не найден пользовательский, пробуем профиль по умолчанию
                return LoadDefaultProfile();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LoadProfile error for '{profileName}': {ex}");
                return null;
            }
        }

        /// <summary>
        /// Загружает список всех доступных профилей.
        /// </summary>
        /// <returns>Список имен профилей (без путей и расширений)</returns>
        public static List<string> LoadAllProfiles()
        {
            var profiles = new List<string>();

            try
            {
                if (!Directory.Exists(ProfilesDirectory))
                {
                    return profiles;
                }

                foreach (var file in Directory.GetFiles(ProfilesDirectory, "*.json"))
                {
                    try
                    {
                        var json = File.ReadAllText(file);
                        var options = new JsonSerializerOptions
                        {
                            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                        };

                        // Пытаемся прочитать минимальные данные для проверки валидности
                        using var doc = JsonDocument.Parse(json);
                        if (doc.RootElement.TryGetProperty("FormatVersion", out var versionElem))
                        {
                            profiles.Add(System.IO.Path.GetFileNameWithoutExtension(file));
                        }
                    }
                    catch (JsonException)
                    {
                        // Пропускаем повреждённые файлы, не добавляем в список
                        continue;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error reading profile file {file}: {ex}");
                        continue;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LoadAllProfiles error: {ex}");
            }

            return profiles;
        }

        /// <summary>
        /// Удаляет профиль из файла.
        /// </summary>
        /// <param name="profileName">Имя профиля (без расширения .json)</param>
        /// <returns>True если профиль удален успешно, false если произошла ошибка</returns>
        public static bool DeleteProfile(string profileName)
        {
            if (string.IsNullOrEmpty(profileName))
            {
                return false;
            }

            try
            {
                string filePath = Path.Combine(ProfilesDirectory, $"{profileName}.json");
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"DeleteProfile error: {ex}");
                return false;
            }
        }

        /// <summary>
        /// Создает новый профиль с текущими настройками из AvatarService.
        /// </summary>
        /// <returns>Созданный профиль или null в случае ошибки</returns>
        public static AvatarProfile? CreateDefaultProfile()
        {
            try
            {
                var profile = new AvatarProfile
                {
                    ProfileName = "Default",
                    FormatVersion = "1.0",
                    BaseImagePath = "",
                    LeftEyeImagePath = "",
                    RightEyeImagePath = "",
                    LeftPupilImagePath = "",
                    RightPupilImagePath = "",
                    BlinkImagePath = "",
                    MouthImagePath = "",
                    TalkThreshold = 20f,
                    IsMicrophoneMonitoring = false,
                    SelectedDeviceId = "",
                    LeftEyeCenterX = 0.5f,
                    LeftEyeCenterY = 0.5f,
                    LeftEyeWidth = 0.8f,
                    LeftEyeHeight = 0.8f,
                    LeftMaxDisplacement = 0.3f,
                    RightEyeCenterX = 0.5f,
                    RightEyeCenterY = 0.5f,
                    RightEyeWidth = 0.8f,
                    RightEyeHeight = 0.8f,
                    RightMaxDisplacement = 0.3f,
                    BlinkEnabled = true,
                    MinBlinkIntervalMs = 3000,
                    MaxBlinkIntervalMs = 8000,
                    BlinkCloseDurationMs = 150
                };

                // Сохраняем профиль
                string savedName = SaveProfile(profile);
                if (!string.IsNullOrEmpty(savedName))
                {
                    profile.ProfileName = savedName.Replace(".json", "");
                    return profile;
                }

                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CreateDefaultProfile error: {ex}");
                return null;
            }
        }

        /// <summary>
        /// Загружает профиль по умолчанию (если пользовательский не найден).
        /// </summary>
        /// <returns>Профиль по умолчанию или null</returns>
        private static AvatarProfile? LoadDefaultProfile()
        {
            try
            {
                if (File.Exists(DefaultProfilePath))
                {
                    return LoadProfileFromPath(DefaultProfilePath);
                }

                // Возвращаем новый профиль по умолчанию, если файла нет
                return CreateDefaultProfile();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LoadDefaultProfile error: {ex}");
                return CreateDefaultProfile();
            }
        }

        /// <summary>
        /// Загружает профиль из указанного пути файла.
        /// </summary>
        /// <param name="filePath">Полный путь к файлу профиля</param>
        /// <returns>Загруженный профиль</returns>
        private static AvatarProfile LoadProfileFromPath(string filePath)
        {
            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.Never
            };

            string json = File.ReadAllText(filePath);
            var profile = JsonSerializer.Deserialize<AvatarProfile>(json, options);

            // Устанавливаем значения по умолчанию для полей, которые могли не десериализоваться
            if (profile != null)
            {
                // Убеждаемся, что пути существует или пустые
                if (string.IsNullOrEmpty(profile.BaseImagePath)) profile.BaseImagePath = "";
                if (string.IsNullOrEmpty(profile.LeftEyeImagePath)) profile.LeftEyeImagePath = "";
                if (string.IsNullOrEmpty(profile.RightEyeImagePath)) profile.RightEyeImagePath = "";
                if (string.IsNullOrEmpty(profile.LeftPupilImagePath)) profile.LeftPupilImagePath = "";
                if (string.IsNullOrEmpty(profile.RightPupilImagePath)) profile.RightPupilImagePath = "";
                if (string.IsNullOrEmpty(profile.BlinkImagePath)) profile.BlinkImagePath = "";
                if (string.IsNullOrEmpty(profile.MouthImagePath)) profile.MouthImagePath = "";

                // Убеждаемся, что настройки имеют значения по умолчанию
                profile.TalkThreshold = float.IsFinite(profile.TalkThreshold) ? Math.Clamp(profile.TalkThreshold, 0, 100) : 20;
                profile.MicrophoneSensitivity = double.IsFinite(profile.MicrophoneSensitivity) ? Math.Clamp(profile.MicrophoneSensitivity, .5, 8) : 1;
                profile.MouthCloseDelayMs = Math.Clamp(profile.MouthCloseDelayMs, 0, 2000);
                if (profile.MinBlinkIntervalMs <= 0) profile.MinBlinkIntervalMs = 3000;
                if (profile.MaxBlinkIntervalMs <= 0 || profile.MaxBlinkIntervalMs < profile.MinBlinkIntervalMs)
                    profile.MaxBlinkIntervalMs = Math.Max(8000, profile.MinBlinkIntervalMs);
                if (profile.BlinkCloseDurationMs <= 0) profile.BlinkCloseDurationMs = 150;

                if (string.IsNullOrEmpty(profile.SceneBackgroundColor)) profile.SceneBackgroundColor = "#00FF00";

                // Корректируем координаты глаз (0-1 range)
                profile.LeftEyeCenterX = Math.Clamp(profile.LeftEyeCenterX, 0f, 1f);
                profile.LeftEyeCenterY = Math.Clamp(profile.LeftEyeCenterY, 0f, 1f);
                profile.LeftEyeWidth = Math.Clamp(profile.LeftEyeWidth, 0.1f, 1f);
                profile.LeftEyeHeight = Math.Clamp(profile.LeftEyeHeight, 0.1f, 1f);
                profile.LeftMaxDisplacement = Math.Clamp(profile.LeftMaxDisplacement, 0f, 0.5f);

                profile.RightEyeCenterX = Math.Clamp(profile.RightEyeCenterX, 0f, 1f);
                profile.RightEyeCenterY = Math.Clamp(profile.RightEyeCenterY, 0f, 1f);
                profile.RightEyeWidth = Math.Clamp(profile.RightEyeWidth, 0.1f, 1f);
                profile.RightEyeHeight = Math.Clamp(profile.RightEyeHeight, 0.1f, 1f);
                profile.RightMaxDisplacement = Math.Clamp(profile.RightMaxDisplacement, 0f, 0.5f);
                profile.PupilSize = Math.Clamp(profile.PupilSize, 8f, 160f);
            }

            if (profile == null) throw new JsonException("Profile must be a JSON object.");
            AvatarEmotion.Normalize(profile);
            return profile;
        }

        /// <summary>Loads the default profile, creating it if it does not exist.</summary>
        public static AvatarProfile LoadOrCreateDefault()
        {
            var p = LoadProfile("") ?? CreateDefaultProfile();
            return p ?? new AvatarProfile();
        }

        /// <summary>Loads the default profile, applies a mutation, and saves it back.</summary>
        public static bool UpdateDefaultProfile(Action<AvatarProfile> mutate)
        {
            var p = LoadOrCreateDefault();
            mutate(p);
            p.ProfileName = "default";
            return !string.IsNullOrEmpty(SaveProfile(p));
        }

        /// <summary>Parses a color from a HEX string ("#RRGGBB" / "#AARRGGBB").</summary>
        public static bool TryParseColor(string? hex, out System.Windows.Media.Color color)
        {
            color = default;
            if (string.IsNullOrWhiteSpace(hex)) return false;
            try
            {
                color = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex.Trim());
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
