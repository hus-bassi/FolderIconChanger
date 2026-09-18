using System.IO;
using System.Text.Json;
using FolderIconChanger.Helpers;
using FolderIconChanger.Models;

namespace FolderIconChanger.Services;

/// <summary>Persists lightweight settings (last locations + customization records).</summary>
public sealed class SettingsService
{
    private static readonly Lazy<SettingsService> Lazy = new(() => new SettingsService());
    public static SettingsService Instance => Lazy.Value;

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly string _filePath;
    private readonly object _lock = new();
    private AppSettings _settings;

    private SettingsService()
    {
        string dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FolderIconChanger");
        _filePath = Path.Combine(dir, "settings.json");
        _settings = Load();
    }

    public AppSettings Current
    {
        get { lock (_lock) { return _settings; } }
    }

    public void RememberImageDirectory(string? directory)
    {
        if (string.IsNullOrWhiteSpace(directory)) return;
        lock (_lock)
        {
            _settings.LastImageDirectory = directory;
            Save();
        }
    }

    public void RememberFolder(string? folder)
    {
        if (string.IsNullOrWhiteSpace(folder)) return;
        lock (_lock)
        {
            _settings.LastFolder = folder;
            Save();
        }
    }

    public void SaveRecord(AppliedFolderRecord record)
    {
        lock (_lock)
        {
            var existing = _settings.FindRecord(record.FolderPath);
            if (existing is not null)
            {
                _settings.Records.Remove(existing);
            }

            _settings.Records.Add(record);
            Save();
        }
    }

    public AppliedFolderRecord? GetRecord(string folderPath)
    {
        lock (_lock)
        {
            return _settings.FindRecord(folderPath);
        }
    }

    public void RemoveRecord(string folderPath)
    {
        lock (_lock)
        {
            var existing = _settings.FindRecord(folderPath);
            if (existing is not null)
            {
                _settings.Records.Remove(existing);
                Save();
            }
        }
    }

    private AppSettings Load()
    {
        try
        {
            if (!File.Exists(_filePath))
            {
                return new AppSettings();
            }

            string json = File.ReadAllText(_filePath);
            return JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
        }
        catch (Exception ex)
        {
            AppLog.Error(ex, "Failed to load settings.");
            return new AppSettings();
        }
    }

    private void Save()
    {
        try
        {
            string dir = Path.GetDirectoryName(_filePath)!;
            Directory.CreateDirectory(dir);
            string json = JsonSerializer.Serialize(_settings, JsonOptions);
            File.WriteAllText(_filePath, json);
        }
        catch (Exception ex)
        {
            AppLog.Error(ex, "Failed to save settings.");
        }
    }
}