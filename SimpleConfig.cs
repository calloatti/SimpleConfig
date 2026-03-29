using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using Timberborn.PlayerDataSystem;
using UnityEngine;

// 2026/02/25 08:42

namespace Calloatti.Config
{
  public class SimpleConfigSchema
  {
    public string ConfigFileName { get; set; } = "DefaultModConfig.txt";
    public List<SimpleConfigEntry> Settings { get; set; } = new List<SimpleConfigEntry>();
  }

  public class SimpleConfigEntry
  {
    public string Key { get; set; }
    public string Type { get; set; }
    public object DefaultValue { get; set; }
    public string Label { get; set; }
    public string Tooltip { get; set; }
    public string ControlType { get; set; }
    public float? MinValue { get; set; }
    public float? MaxValue { get; set; }
    public float? Step { get; set; }
    public List<string> Options { get; set; }

    public List<string> AvailableIn { get; set; } = new List<string> { "MainMenu" };

    public bool RequiresRestart { get; set; }
    public bool RequiresReload { get; set; }
  }

  public class SimpleConfig
  {
    private readonly string _txtFilePath;
    private readonly string _txtSchemaPath;
    private readonly Dictionary<string, string> _settings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _comments = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    public SimpleConfig(string modPath)
    {
      // modPath comes from modEnvironment.ModPath in your Starter class
      _txtSchemaPath = Path.Combine(modPath, "SimpleConfig.txt");

      if (!File.Exists(_txtSchemaPath))
      {
        Debug.LogError($"[SimpleConfig] CRITICAL ERROR: SimpleConfig.txt not found at: {_txtSchemaPath}");
        return;
      }

      SimpleConfigSchema schema = LoadSchema();
      _txtFilePath = Path.Combine(PlayerDataFileService.PlayerDataDirectory, schema.ConfigFileName);

      LoadTxt();
      SyncWithSchema(schema);
    }

    // A robust helper to strip surrounding quotes, spaces, and trailing commas from schema strings
    private string CleanSchemaValue(string val)
    {
      if (string.IsNullOrWhiteSpace(val)) return val;
      val = val.Trim();
      
      // Strip trailing commas common in loose formats
      if (val.EndsWith(",")) val = val.Substring(0, val.Length - 1).Trim();
      
      // Strip leading/trailing quotes (tolerates mismatched typo quotes)
      if (val.StartsWith("\"")) val = val.Substring(1);
      if (val.EndsWith("\"")) val = val.Substring(0, val.Length - 1);
      
      return val.Trim();
    }

    private SimpleConfigSchema LoadSchema()
    {
      SimpleConfigSchema schema = new SimpleConfigSchema();
      SimpleConfigEntry currentEntry = null;

      string[] lines = File.ReadAllLines(_txtSchemaPath);
      foreach (string rawLine in lines)
      {
        string line = rawLine.Trim();

        if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#") || line.StartsWith("//"))
          continue;

        int equalsIndex = line.IndexOf('=');
        if (equalsIndex > 0)
        {
          // Clean the property name right away to strip any quotes from the schema
          string prop = CleanSchemaValue(line.Substring(0, equalsIndex));
          string rawValue = line.Substring(equalsIndex + 1).Trim();

          // Strip inline comments marked by # in the schema
          int hashIndex = rawValue.IndexOf('#');
          if (hashIndex >= 0)
          {
            rawValue = rawValue.Substring(0, hashIndex).Trim();
          }

          if (prop.Equals("Key", StringComparison.OrdinalIgnoreCase))
          {
            if (currentEntry != null)
            {
              schema.Settings.Add(currentEntry);
            }
            currentEntry = new SimpleConfigEntry();
          }

          ApplySchemaProperty(schema, currentEntry, prop, rawValue);
        }
      }

      if (currentEntry != null)
      {
        schema.Settings.Add(currentEntry);
      }

      return schema;
    }

    private void ApplySchemaProperty(SimpleConfigSchema schema, SimpleConfigEntry entry, string prop, string rawValue)
    {
      // Clean the value side to strip any quotes
      string val = CleanSchemaValue(rawValue);

      if (prop.Equals("ConfigFileName", StringComparison.OrdinalIgnoreCase))
      {
        schema.ConfigFileName = val;
        return;
      }

      if (entry == null) return;

      switch (prop.ToLowerInvariant())
      {
        case "key": entry.Key = val; break;
        case "type": entry.Type = val; break;
        case "defaultvalue": entry.DefaultValue = val; break;
        case "label": entry.Label = val; break;
        case "tooltip": entry.Tooltip = val; break;
        case "controltype": entry.ControlType = val; break;
        case "minvalue": if (float.TryParse(val, NumberStyles.Any, CultureInfo.InvariantCulture, out float min)) entry.MinValue = min; break;
        case "maxvalue": if (float.TryParse(val, NumberStyles.Any, CultureInfo.InvariantCulture, out float max)) entry.MaxValue = max; break;
        case "step": if (float.TryParse(val, NumberStyles.Any, CultureInfo.InvariantCulture, out float step)) entry.Step = step; break;
        case "requiresrestart": if (bool.TryParse(val, out bool rr)) entry.RequiresRestart = rr; break;
        case "requiresreload": if (bool.TryParse(val, out bool rl)) entry.RequiresReload = rl; break;
        case "options": entry.Options = ParseSchemaArray(val); break;
        case "availablein": entry.AvailableIn = ParseSchemaArray(val); break;
      }
    }

    private List<string> ParseSchemaArray(string val)
    {
      var list = new List<string>();
      val = val.Replace("[", "").Replace("]", "");
      string[] parts = val.Split(',');
      foreach (var p in parts)
      {
        string clean = CleanSchemaValue(p);
        if (!string.IsNullOrEmpty(clean)) list.Add(clean);
      }
      return list;
    }

    private void SyncWithSchema(SimpleConfigSchema schema)
    {
      bool changesMade = false;

      if (schema?.Settings != null)
      {
        foreach (var entry in schema.Settings)
        {
          if (string.IsNullOrWhiteSpace(entry.Key)) continue;

          // 1. Get the string representation of the default value
          string strValue = "";
          if (entry.DefaultValue != null)
          {
            if (entry.DefaultValue is double d)
              strValue = d.ToString(CultureInfo.InvariantCulture);
            else if (entry.DefaultValue is float f)
              strValue = f.ToString(CultureInfo.InvariantCulture);
            else if (entry.DefaultValue is bool b)
              strValue = b.ToString();
            else
              strValue = entry.DefaultValue.ToString();
          }

          // 2. Build the new comment, including the default value
          string comment = "";
          if (!string.IsNullOrWhiteSpace(entry.Label)) comment += entry.Label;
          if (!string.IsNullOrWhiteSpace(entry.Tooltip))
          {
            if (comment.Length > 0) comment += " - ";
            comment += entry.Tooltip;
          }
          if (entry.DefaultValue != null)
          {
            if (comment.Length > 0) comment += " - ";
            comment += $"Default value: {strValue}";
          }

          // 3. Check and update the comment if it differs from what's currently stored
          if (!string.IsNullOrWhiteSpace(comment))
          {
            // SetComment automatically prepends "# " to comments. 
            // We format our target string exactly like that so we can accurately check for differences.
            string targetComment = "# " + comment;
            _comments.TryGetValue(entry.Key, out string existingComment);

            if (existingComment != targetComment)
            {
              SetComment(entry.Key, comment);
              changesMade = true;
            }
          }

          // 4. Add the setting value itself if it is completely missing
          if (!_settings.ContainsKey(entry.Key))
          {
            _settings[entry.Key] = strValue;
            changesMade = true;
          }
        }
      }

      if (changesMade)
      {
        Save();
      }
    }

    public void LoadTxt()
    {
      _settings.Clear();
      _comments.Clear();
      if (!File.Exists(_txtFilePath)) return;

      foreach (string line in File.ReadAllLines(_txtFilePath))
      {
        string trimmed = line.Trim();

        if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith("#") || trimmed.StartsWith("//"))
          continue;

        int equalsIndex = trimmed.IndexOf('=');
        if (equalsIndex > 0)
        {
          string key = trimmed.Substring(0, equalsIndex).Trim();
          string rawValue = trimmed.Substring(equalsIndex + 1);

          int hashIndex = rawValue.IndexOf('#');
          int slashIndex = rawValue.IndexOf("//");

          int commentIndex = -1;
          if (hashIndex >= 0 && slashIndex >= 0) commentIndex = Math.Min(hashIndex, slashIndex);
          else if (hashIndex >= 0) commentIndex = hashIndex;
          else if (slashIndex >= 0) commentIndex = slashIndex;

          if (commentIndex >= 0)
          {
            _settings[key] = rawValue.Substring(0, commentIndex).Trim();
            _comments[key] = rawValue.Substring(commentIndex).Trim();
          }
          else
          {
            _settings[key] = rawValue.Trim();
          }
        }
      }
    }

    public void Save()
    {
      Directory.CreateDirectory(PlayerDataFileService.PlayerDataDirectory);
      List<string> outputLines = new List<string>();
      HashSet<string> writtenKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

      if (File.Exists(_txtFilePath))
      {
        foreach (string line in File.ReadAllLines(_txtFilePath))
        {
          string trimmed = line.Trim();

          if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith("#") || trimmed.StartsWith("//"))
          {
            outputLines.Add(line);
            continue;
          }

          int equalsIndex = trimmed.IndexOf('=');
          if (equalsIndex > 0)
          {
            string key = trimmed.Substring(0, equalsIndex).Trim();

            if (_settings.TryGetValue(key, out string val))
            {
              string comment = _comments.TryGetValue(key, out string c) ? $" {c}" : "";
              outputLines.Add($"{key}={val}{comment}");
              writtenKeys.Add(key);
            }
          }
          else
          {
            outputLines.Add(line);
          }
        }
      }

      foreach (var kvp in _settings)
      {
        if (!writtenKeys.Contains(kvp.Key))
        {
          string comment = _comments.TryGetValue(kvp.Key, out string c) ? $" {c}" : "";
          outputLines.Add($"{kvp.Key}={kvp.Value}{comment}");
        }
      }

      File.WriteAllLines(_txtFilePath, outputLines);
    }

    public bool HasKey(string key) => _settings.ContainsKey(key);

    public void DeleteKey(string key)
    {
      _settings.Remove(key);
      _comments.Remove(key);
    }

    public string GetString(string key)
    {
      if (_settings.TryGetValue(key, out string val)) return val;
      Debug.LogError($"[SimpleConfig] ERROR: Missing string for key '{key}'.");
      return string.Empty;
    }

    public bool GetBool(string key)
    {
      if (_settings.TryGetValue(key, out string val) && bool.TryParse(val, out bool result)) return result;
      Debug.LogError($"[SimpleConfig] ERROR: Missing or invalid bool for key '{key}'.");
      return false;
    }

    public int GetInt(string key)
    {
      if (_settings.TryGetValue(key, out string val) && int.TryParse(val, out int result)) return result;
      Debug.LogError($"[SimpleConfig] ERROR: Missing or invalid int for key '{key}'.");
      return 0;
    }

    public float GetFloat(string key)
    {
      if (_settings.TryGetValue(key, out string val) && float.TryParse(val.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out float result)) return result;
      Debug.LogError($"[SimpleConfig] ERROR: Missing or invalid float for key '{key}'.");
      return 0f;
    }

    public T GetEnum<T>(string key) where T : struct, Enum
    {
      if (_settings.TryGetValue(key, out string val) && Enum.TryParse<T>(val, true, out T result)) return result;
      Debug.LogError($"[SimpleConfig] ERROR: Missing or invalid enum '{typeof(T).Name}' for key '{key}'.");
      return default;
    }

    public void Set(string key, object value)
    {
      if (value is float f)
        _settings[key] = f.ToString(CultureInfo.InvariantCulture);
      else if (value is double d)
        _settings[key] = d.ToString(CultureInfo.InvariantCulture);
      else
        _settings[key] = value.ToString();
    }

    public void SetComment(string key, string comment)
    {
      if (string.IsNullOrWhiteSpace(comment))
      {
        _comments.Remove(key);
        return;
      }

      string trimmed = comment.TrimStart();
      if (!trimmed.StartsWith("#") && !trimmed.StartsWith("//"))
      {
        comment = "# " + comment;
      }

      _comments[key] = comment;
    }
  }
}