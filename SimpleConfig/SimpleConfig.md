================================================================================
SimpleConfig.cs - Step-by-Step Explanation
================================================================================

1. HIGH-LEVEL PURPOSE
The SimpleConfig class is a robust, thread-safe configuration manager for a 
Timberborn mod. It bridges a mod-defined "schema" (SimpleConfig.txt) with a 
user-specific configuration file (e.g., DefaultModConfig.txt) stored in the 
game's PlayerDataDirectory.

2. SCHEMA PARSING (ParseSchemaFile & ApplySchemaProperty)
When instantiated, SimpleConfig looks for a SimpleConfig.txt file in the mod's 
directory. 
- It reads this file line-by-line, parsing key-value pairs separated by '='.
- When it hits a "Key=" line, it creates a new SimpleConfigEntry.
- Subsequent lines (Type, DefaultValue, Label, etc.) populate this entry's 
  properties. This builds a "cached schema" defining exactly what settings 
  the mod expects and how their UI should be generated.

3. LOADING USER CONFIG (LoadTxt)
Next, it attempts to load the actual user settings file.
- It reads the file line-by-line, ignoring pure comments (# or //).
- It splits lines by '=' to get the Key and the Raw Value.
- It looks for " #" in the Raw Value. This is a clever trick to separate the 
  actual setting value from "inline metadata comments" on the same line.
- It populates the _settings dictionary with the value and the _comments 
  dictionary with the inline string.
- If it detects "Inline:true" in the comment, it dynamically parses those 
  pipe-separated values (e.g., | Type:toggle | Label:Enable) to update the 
  runtime schema cache. This makes user-side text edits instantly reflect in UI.

4. HANDLING ORPHANS (EnrichSchemaWithOrphans)
An "orphan" is a setting found in the user's config file that was NOT defined 
in the mod's SimpleConfig.txt schema.
- The system iterates over loaded _settings and checks against known keys.
- If a key is unknown, it wraps it into a new SimpleConfigEntry flagged as 
  IsOrphan = true.
- It checks if the orphan has a modern "Inline:true" comment. If so, it parses 
  it. If not, it falls back to a "Legacy Parser" that tries to safely extract 
  labels, tooltips, and default values from older, unstructured comments without 
  destructively overwriting them.

5. SYNCHRONIZATION & SAVING (SyncWithSchema & Save)
- SyncWithSchema ensures every entry in the schema exists in the user config.
- If a setting is missing, it adds it using the schema's DefaultValue.
- It constructs a highly structured inline comment (e.g., # Inline:true | 
  Type:int | ...) for modern schema entries. Orphan entries retain their exact 
  RawComment to prevent data loss.
- Save() writes the configuration back to disk. It reads the existing file 
  first to preserve file structure, blank lines, and standalone comments, only 
  replacing the active key=value #comment lines.

6. FILE SYSTEM WATCHER (InitializeWatcher & OnConfigFileChanged)
- A FileSystemWatcher is hooked to the player data directory.
- If the user manually edits and saves the text file while the game is running, 
  the watcher triggers a reload (LoadTxt) after a tiny debounce delay (500ms).
- This allows for real-time "hot-reloading" of configuration changes.

7. RUNTIME DATA ACCESS (Getters & Setters)
- The class provides strict-typed getters (GetString, GetBool, GetInt, 
  GetFloat, GetEnum) to retrieve configuration values.
- All data access and mutations (Set, SetComment, SetInlineComment, DeleteKey) 
  are protected by a lock object (_lockObj) to ensure thread-safety, which is 
  critical in Unity/Timberborn since file watchers run on separate threads.
================================================================================

1. The constructor defines the expected file path for the mod's SimpleConfig.txt schema file.
2. It verifies that this schema file exists on disk, logging a critical error and aborting if it does not.
3. It executes ParseSchemaFile() to read the schema and build the internal _cachedSchema blueprint.
4. It defines the file path for the user's actual configuration file using the game's player data directory.
5. It calls LoadConfigTxt() to read the user's currently saved settings and comments into memory.
6. It runs EnrichSchemaWithOrphans() to safely inject any unknown or legacy user settings into the temporary schema cache.
7. It calls SyncWithSchema() to align the loaded data with the blueprint, writing missing defaults or formatted comments to the disk if needed.
8. It executes InitializeWatcher() to monitor the user's configuration file for external changes to enable hot-reloading.