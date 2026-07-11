using Bindito.Core;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using Timberborn.CoreUI;
using Timberborn.DropdownSystem;
using Timberborn.Modding;
using Timberborn.ModManagerScene;
using Timberborn.SingletonSystem;
using UnityEngine;
using UnityEngine.UIElements;

namespace Calloatti.Config
{
  #region 1. EARLY BOOT INJECTION
  public class SimpleConfigUIStarter : IModStarter
  {
    public void StartMod(IModEnvironment modEnvironment)
    {
      lock (AppDomain.CurrentDomain)
      {
        if (AppDomain.CurrentDomain.GetData("SimpleConfigUiPatched") is bool completelyPatched && completelyPatched)
        {
          return;
        }

        try
        {
          var harmony = new Harmony("com.calloatti.simpleconfig.sharedui");

          Type targetType = AccessTools.TypeByName("Timberborn.SettingsSystemUI.SettingsBox");
          if (targetType == null)
          {
            Debug.LogWarning("[SimpleConfigUI] Could not find internal type SettingsBox.");
            return;
          }

          var targetMethod = targetType.GetMethod("Load", BindingFlags.Instance | BindingFlags.Public);
          var postfixMethod = typeof(SimpleConfigModsMenuPatch).GetMethod("Postfix", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);

          if (targetMethod != null && postfixMethod != null)
          {
            harmony.Patch(targetMethod, postfix: new HarmonyMethod(postfixMethod));
            AppDomain.CurrentDomain.SetData("SimpleConfigUiPatched", true);
            Debug.Log("[SimpleConfigUI] Successfully injected unified Mod Settings button into SettingsBox.");
          }
        }
        catch (Exception ex)
        {
          Debug.LogError($"[SimpleConfigUI] Failed to apply runtime mod menu hook: {ex}");
        }
      }
    }
  }
  #endregion

  #region 2. TIMBERBORN DI CONFIGURATOR
  [Context("MainMenu")]
  public class SimpleConfigUIConfigurator : Configurator
  {
    protected override void Configure()
    {
      Bind<SimpleConfigUIRegistry>().AsSingleton();
      Bind<SimpleConfigUIDependencies>().AsSingleton();
    }
  }
  #endregion

  #region 3. MOD REPOSITORY REGISTRY & DEPENDENCY HOLDER
  public class SimpleConfigUIRegistry : ILoadableSingleton
  {
    public static ModRepository ActiveModRepository { get; private set; }
    private readonly ModRepository _modRepository;

    public SimpleConfigUIRegistry(ModRepository modRepository)
    {
      _modRepository = modRepository;
    }

    public void Load()
    {
      ActiveModRepository = _modRepository;
    }
  }

  public class SimpleConfigUIDependencies : ILoadableSingleton
  {
    public static SimpleConfigUIDependencies Instance { get; private set; }
    public DropdownItemsSetter DropdownItemsSetter { get; }
    public DropdownListDrawer DropdownListDrawer { get; }

    public SimpleConfigUIDependencies(DropdownItemsSetter dropdownItemsSetter, DropdownListDrawer dropdownListDrawer)
    {
      DropdownItemsSetter = dropdownItemsSetter;
      DropdownListDrawer = dropdownListDrawer;
    }

    public void Load()
    {
      Instance = this;
    }
  }
  #endregion

  #region 4. TIMBERBORN PANEL CONTROLLER
  public class ModSettingsPanelController : IPanelController
  {
    private readonly PanelStack _panelStack;
    private readonly VisualElement _root;

    public ModSettingsPanelController(PanelStack panelStack)
    {
      _panelStack = panelStack;
      _root = ModSettingsUIBuilder.BuildConfigurationOverlay(OnUICancelled);
    }

    public VisualElement GetPanel() => _root;

    public bool OnUIConfirmed() => false;

    public void OnUICancelled()
    {
      _panelStack.Pop(this);
    }
  }
  #endregion

  #region 5. MOD MANAGER HARMONY PATCH
  public static class SimpleConfigModsMenuPatch
  {
    public static void Postfix(object __instance)
    {
      var rootField = AccessTools.Field(__instance.GetType(), "_root");
      if (rootField == null) return;
      var root = rootField.GetValue(__instance) as VisualElement;

      var panelStackField = AccessTools.Field(__instance.GetType(), "_panelStack");
      if (panelStackField == null) return;
      var panelStack = panelStackField.GetValue(__instance) as PanelStack;

      if (root == null || panelStack == null) return;
      if (root.Q("ModSettingsButton") != null) return;

      VisualElement scrollWrapper = root.Q<VisualElement>("ScrollViewWrapper");
      if (scrollWrapper == null || scrollWrapper.parent == null) return;

      Button bindingsButton = root.Q<Button>("BindingsButton");
      if (bindingsButton == null) return;

      Button settingsButton = (Button)Activator.CreateInstance(bindingsButton.GetType());
      settingsButton.name = "ModSettingsButton";
      settingsButton.text = "Mod Settings";

      int sheetCount = bindingsButton.styleSheets.count;
      for (int i = 0; i < sheetCount; i++)
      {
        settingsButton.styleSheets.Add(bindingsButton.styleSheets[i]);
      }

      foreach (var className in bindingsButton.GetClasses())
      {
        settingsButton.AddToClassList(className);
      }

      settingsButton.RegisterCallback<ClickEvent>(evt =>
      {
        var controller = new ModSettingsPanelController(panelStack);
        panelStack.HideAndPushOverlay(controller);
      });

      VisualElement buttonRow = new VisualElement();
      buttonRow.style.flexDirection = FlexDirection.Row;
      buttonRow.style.justifyContent = Justify.Center;
      buttonRow.style.marginTop = 15;
      buttonRow.style.marginBottom = 15;

      bindingsButton.style.marginTop = 0;
      bindingsButton.style.marginBottom = 0;
      bindingsButton.style.marginRight = 10;
      bindingsButton.style.alignSelf = Align.Auto;

      settingsButton.style.marginTop = 0;
      settingsButton.style.marginBottom = 0;
      settingsButton.style.marginLeft = 10;
      settingsButton.style.alignSelf = Align.Auto;

      buttonRow.Add(bindingsButton);
      buttonRow.Add(settingsButton);

      scrollWrapper.parent.Add(buttonRow);
    }
  }
  #endregion

  #region 6. MOD SETTINGS UI BUILDER
  public static class ModSettingsUIBuilder
  {
    public static VisualElement BuildConfigurationOverlay(Action onClose)
    {
      // Core layout size properties
      float textColWidth = 450f;
      float controlColWidth = 320f;
      float buttonColWidth = 130f;

      // Mirroring the calculation setup from SyncModsPro table structure
      float calculatedTotalWidth = textColWidth + controlColWidth + buttonColWidth; // 900f
      float panelWidth = calculatedTotalWidth + 100f;                              // 1000f

      var modalBackground = new VisualElement();
      modalBackground.name = "SimpleConfigModalOverlay";
      modalBackground.style.position = Position.Absolute;
      modalBackground.style.top = 0;
      modalBackground.style.bottom = 0;
      modalBackground.style.left = 0;
      modalBackground.style.right = 0;
      modalBackground.style.backgroundColor = new StyleColor(new Color(0f, 0f, 0f, 0.90f));
      modalBackground.style.alignItems = Align.Center;
      modalBackground.style.justifyContent = Justify.Center;

      var mainWindow = new NineSliceVisualElement();
      mainWindow.AddToClassList("content-centered");
      mainWindow.AddToClassList("sliced-border");
      mainWindow.AddToClassList("sliced-border--nontransparent");

      mainWindow.style.width = panelWidth;
      mainWindow.style.maxWidth = StyleKeyword.None;

      var headerBackground = new NineSliceVisualElement();
      headerBackground.AddToClassList("capsule-header");
      headerBackground.style.justifyContent = Justify.Center;
      headerBackground.style.alignItems = Align.Center;
      headerBackground.style.top = -10;

      var title = new Label("Mod Settings");
      title.AddToClassList("capsule-header__text");
      title.style.unityTextAlign = TextAnchor.MiddleCenter;
      title.style.top = -2;
      headerBackground.Add(title);
      mainWindow.Add(headerBackground);

      var windowBox = new VisualElement();
      windowBox.AddToClassList("box");
      windowBox.style.width = panelWidth;
      windowBox.style.maxWidth = StyleKeyword.None;
      windowBox.style.height = 750;
      windowBox.style.paddingTop = 45f;
      windowBox.style.paddingBottom = 45f;
      windowBox.style.paddingLeft = 65f;
      windowBox.style.paddingRight = 45f;

      var scrollView = CreateScrollView();
      windowBox.Add(scrollView);
      mainWindow.Add(windowBox);

      var closeButton = new Button();
      closeButton.AddToClassList("close-button");
      closeButton.RegisterCallback<ClickEvent>(evt =>
      {
        onClose?.Invoke();
      });
      mainWindow.Add(closeButton);

      if (SimpleConfigUIRegistry.ActiveModRepository != null)
      {
        var sortedMods = SimpleConfigUIRegistry.ActiveModRepository.EnabledMods
                            .OrderBy(m => m.DisplayName)
                            .ToList();

        foreach (var mod in sortedMods)
        {
          string path = mod.ModDirectory.Path;
          string schemaPath = Path.Combine(path, "SimpleConfig.txt");

          if (!File.Exists(schemaPath)) continue;

          var localConfig = new SimpleConfig(path);
          SimpleConfigSchema schema = localConfig.LoadSchema();

          if (schema == null || !schema.Settings.Any()) continue;

          var pendingChanges = new Dictionary<string, object>();

          // Native Unity Foldout control to wrap the settings
          var foldout = new Foldout();
          foldout.text = mod.DisplayName;
          foldout.value = false; // Default to collapsed
          foldout.style.marginBottom = 15;

          // Apply Timberborn's font styling to the foldout label so it isn't tiny
          var foldoutToggle = foldout.Q<Toggle>();
          if (foldoutToggle != null)
          {
            var foldoutLabel = foldoutToggle.Q<Label>();
            if (foldoutLabel != null)
            {
              foldoutLabel.AddToClassList("text--big");
            }
          }

          int rowIndex = 0;

          foreach (var entry in schema.Settings)
          {
            if (string.IsNullOrWhiteSpace(entry.Key)) continue;

            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            // Using FlexStart visual progression identical to SyncModsPro row elements
            row.style.justifyContent = Justify.FlexStart;
            row.style.alignItems = Align.Center;
            row.style.paddingTop = 8;
            row.style.paddingBottom = 8;
            row.style.paddingLeft = 10;
            row.style.paddingRight = 10;
            row.style.borderBottomWidth = 1;
            row.style.borderBottomColor = new StyleColor(new Color(0, 0, 0, 0.3f));

            if (rowIndex % 2 == 0)
            {
              row.style.backgroundColor = new StyleColor(new Color(0f, 0f, 0f, 0.15f));
            }

            // --- 1. TEXT STACK (Left) ---
            var textContainer = new VisualElement();
            textContainer.style.flexDirection = FlexDirection.Column;
            textContainer.style.justifyContent = Justify.Center;
            textContainer.style.width = textColWidth;
            textContainer.style.minWidth = textColWidth;
            textContainer.style.maxWidth = textColWidth;
            textContainer.style.flexShrink = 0;
            textContainer.style.paddingRight = 15;

            string cleanLabel = string.IsNullOrWhiteSpace(entry.Label) ? entry.Key : entry.Label;

            var keyLabel = new Label(cleanLabel);
            keyLabel.AddToClassList("text--default");
            keyLabel.style.unityFontStyleAndWeight = FontStyle.Normal;

            var tooltipLabel = new Label(entry.Tooltip ?? "");
            tooltipLabel.style.color = new StyleColor(new Color(0.8f, 0.8f, 0.8f));
            tooltipLabel.style.fontSize = 12;
            tooltipLabel.style.whiteSpace = WhiteSpace.Normal;

            var defaultLabel = new Label($"Default: {entry.DefaultValue ?? "None"}");
            defaultLabel.style.color = new StyleColor(new Color(0.6f, 0.6f, 0.6f));
            defaultLabel.style.fontSize = 12;
            defaultLabel.style.marginTop = 2;

            textContainer.Add(keyLabel);

            if (!string.IsNullOrWhiteSpace(entry.Tooltip))
            {
              textContainer.Add(tooltipLabel);
            }

            textContainer.Add(defaultLabel);

            if (entry.RequiresRestart || entry.RequiresReload)
            {
              var indicatorLabel = new Label(entry.RequiresRestart ? "[Requires Restart]" : "[Requires Reload]");
              indicatorLabel.style.fontSize = 12;
              indicatorLabel.style.unityFontStyleAndWeight = FontStyle.Normal;
              indicatorLabel.style.marginTop = 2;
              indicatorLabel.style.color = new StyleColor(entry.RequiresRestart ? new Color(0.85f, 0.35f, 0.35f) : new Color(0.85f, 0.55f, 0.25f));
              textContainer.Add(indicatorLabel);
            }

            // --- 2. CONTROL (Middle) ---
            var controlContainer = new VisualElement
            {
              style = {
                width = controlColWidth,
                minWidth = controlColWidth,
                maxWidth = controlColWidth,
                flexShrink = 0,
                justifyContent = Justify.Center,
                paddingRight = 15
              }
            };
            VisualElement control = null;
            Action resetToDefaultAction = null;

            string controlType = entry.ControlType ?? "TextField";
            switch (controlType.ToLowerInvariant())
            {
              case "toggle":
                var toggle = new Toggle { value = localConfig.GetBool(entry.Key) };
                toggle.AddToClassList("game-toggle");
                toggle.RegisterValueChangedCallback(evt => pendingChanges[entry.Key] = evt.newValue);
                resetToDefaultAction = () => toggle.value = bool.TryParse(entry.DefaultValue?.ToString(), out bool b) && b;
                control = toggle;
                break;

              case "slider":
                float min = entry.MinValue ?? 0f;
                float max = entry.MaxValue ?? 100f;
                float step = entry.Step ?? 1f;

                var sliderWrapper = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, width = Length.Percent(100) } };

                var valueLabel = new Label();
                valueLabel.style.width = 45;
                valueLabel.style.unityTextAlign = TextAnchor.MiddleRight;
                valueLabel.AddToClassList("game-text-normal");
                valueLabel.style.color = new StyleColor(new Color(0.8f, 0.8f, 0.8f));
                valueLabel.style.marginLeft = 5;

                if (entry.Type != null && entry.Type.Equals("int", StringComparison.OrdinalIgnoreCase))
                {
                  int intMin = (int)min;
                  int intMax = (int)max;
                  int intStep = Mathf.Max(1, (int)step);
                  int initialValue = localConfig.GetInt(entry.Key);

                  var sliderInt = new SliderInt(intMin, intMax) { value = initialValue };
                  sliderInt.style.flexGrow = 1;
                  sliderInt.AddToClassList("slider");
                  sliderInt.AddToClassList("integer-slider__slider");

                  valueLabel.text = initialValue.ToString();

                  sliderInt.RegisterValueChangedCallback(evt =>
                  {
                    int val = evt.newValue;
                    if (intStep > 1)
                    {
                      val = Mathf.RoundToInt((float)val / intStep) * intStep;
                      sliderInt.SetValueWithoutNotify(val);
                    }
                    pendingChanges[entry.Key] = val;
                    valueLabel.text = val.ToString();
                  });

                  resetToDefaultAction = () => {
                    int def = int.TryParse(entry.DefaultValue?.ToString(), out int i) ? i : intMin;
                    sliderInt.value = def;
                  };

                  sliderWrapper.Add(sliderInt);
                }
                else
                {
                  float initialValue = localConfig.GetFloat(entry.Key);
                  var sliderFloat = new Slider(min, max) { value = initialValue };
                  sliderFloat.style.flexGrow = 1;
                  sliderFloat.AddToClassList("slider");
                  sliderFloat.AddToClassList("precise-slider__slider");

                  valueLabel.text = initialValue.ToString("0.##");

                  sliderFloat.RegisterValueChangedCallback(evt =>
                  {
                    float val = evt.newValue;
                    if (step > 0f)
                    {
                      val = Mathf.Round(val / step) * step;
                      sliderFloat.SetValueWithoutNotify(val);
                    }
                    pendingChanges[entry.Key] = val;
                    valueLabel.text = val.ToString("0.##");
                  });

                  resetToDefaultAction = () => {
                    float def = float.TryParse(entry.DefaultValue?.ToString(), out float f) ? f : min;
                    sliderFloat.value = def;
                  };

                  sliderWrapper.Add(sliderFloat);
                }

                sliderWrapper.Add(valueLabel);
                control = sliderWrapper;
                break;

              case "dropdown":
                var options = entry.Options ?? new List<string>();
                var dependencies = SimpleConfigUIDependencies.Instance;
                if (dependencies == null) break;

                var dropdown = new Timberborn.DropdownSystem.Dropdown();
                dropdown.Initialize(dependencies.DropdownListDrawer);

                var provider = new SimpleConfigDropdownProvider(
                  options,
                  getter: () => localConfig.GetString(entry.Key),
                  setter: (val) =>
                  {
                    pendingChanges[entry.Key] = val;
                    localConfig.Set(entry.Key, val);
                  }
                );

                dependencies.DropdownItemsSetter.SetItems(dropdown, provider);

                resetToDefaultAction = () =>
                {
                  string def = entry.DefaultValue?.ToString();
                  if (def != null && options.Contains(def))
                  {
                    provider.SetValue(def);
                    dropdown.UpdateSelectedValue();
                  }
                };

                dropdown.style.width = Length.Percent(100);

                var labelElement = dropdown.Q<Label>("Label");
                if (labelElement != null) labelElement.style.display = DisplayStyle.None;

                var selectionButton = dropdown.Q<Button>("Selection");
                if (selectionButton != null)
                {
                  selectionButton.style.backgroundImage = new StyleBackground(StyleKeyword.None);
                  selectionButton.style.backgroundColor = new StyleColor(new Color(0.15f, 0.15f, 0.15f, 1f));
                  selectionButton.style.borderTopWidth = 1;
                  selectionButton.style.borderBottomWidth = 1;
                  selectionButton.style.borderLeftWidth = 1;
                  selectionButton.style.borderRightWidth = 1;
                  selectionButton.style.borderTopColor = new StyleColor(new Color(0.4f, 0.4f, 0.4f, 1f));
                  selectionButton.style.borderBottomColor = new StyleColor(new Color(0.4f, 0.4f, 0.4f, 1f));
                  selectionButton.style.borderLeftColor = new StyleColor(new Color(0.4f, 0.4f, 0.4f, 1f));
                  selectionButton.style.borderRightColor = new StyleColor(new Color(0.4f, 0.4f, 0.4f, 1f));
                  selectionButton.style.borderTopLeftRadius = 3;
                  selectionButton.style.borderTopRightRadius = 3;
                  selectionButton.style.borderBottomLeftRadius = 3;
                  selectionButton.style.borderBottomRightRadius = 3;
                  selectionButton.style.minHeight = 28;
                }

                control = dropdown;
                break;

              case "textfield":
              default:
                var field = new NineSliceTextField { value = localConfig.GetString(entry.Key) };
                field.AddToClassList("text-field");
                field.RegisterValueChangedCallback(evt => pendingChanges[entry.Key] = evt.newValue);
                resetToDefaultAction = () => field.value = entry.DefaultValue?.ToString();
                control = field;
                break;
            }

            if (control != null)
            {
              controlContainer.Add(control);
            }

            // --- 3. DEFAULT BUTTON (Far Right) ---
            var buttonContainer = new VisualElement
            {
              style = {
                width = buttonColWidth,
                minWidth = buttonColWidth,
                maxWidth = buttonColWidth,
                flexShrink = 0,
                alignItems = Align.FlexStart, // Left align within the cell boundary
                justifyContent = Justify.Center
              }
            };

            var defaultButton = new Button();
            defaultButton.text = "Default";
            defaultButton.style.paddingTop = 4;
            defaultButton.style.paddingBottom = 4;
            defaultButton.style.paddingLeft = 8;
            defaultButton.style.paddingRight = 8;
            defaultButton.style.height = 26;
            defaultButton.style.backgroundColor = new StyleColor(new Color(0.15f, 0.15f, 0.15f, 1f));
            defaultButton.style.borderTopWidth = 1;
            defaultButton.style.borderBottomWidth = 1;
            defaultButton.style.borderLeftWidth = 1;
            defaultButton.style.borderRightWidth = 1;
            defaultButton.style.borderTopColor = new StyleColor(new Color(0.4f, 0.4f, 0.4f, 1f));
            defaultButton.style.borderBottomColor = new StyleColor(new Color(0.4f, 0.4f, 0.4f, 1f));
            defaultButton.style.borderLeftColor = new StyleColor(new Color(0.4f, 0.4f, 0.4f, 1f));
            defaultButton.style.borderRightColor = new StyleColor(new Color(0.4f, 0.4f, 0.4f, 1f));
            defaultButton.style.borderTopLeftRadius = 3;
            defaultButton.style.borderTopRightRadius = 3;
            defaultButton.style.borderBottomLeftRadius = 3;
            defaultButton.style.borderBottomRightRadius = 3;
            defaultButton.style.color = new StyleColor(Color.white);

            defaultButton.RegisterCallback<ClickEvent>(evt =>
            {
              resetToDefaultAction?.Invoke();
            });

            defaultButton.RegisterCallback<MouseEnterEvent>(evt => defaultButton.style.backgroundColor = new StyleColor(new Color(0.25f, 0.25f, 0.25f, 1f)));
            defaultButton.RegisterCallback<MouseLeaveEvent>(evt => defaultButton.style.backgroundColor = new StyleColor(new Color(0.15f, 0.15f, 0.15f, 1f)));

            buttonContainer.Add(defaultButton);

            row.Add(textContainer);
            row.Add(controlContainer);
            row.Add(buttonContainer);

            foldout.Add(row);
            rowIndex++;
          }

          var saveWrapper = new VisualElement();
          saveWrapper.style.alignItems = Align.Center;
          saveWrapper.style.paddingTop = 15;
          saveWrapper.style.paddingBottom = 10;
          // Centers cleanly relative to the visible content columns boundaries
          saveWrapper.style.width = calculatedTotalWidth;

          var saveButton = new Button();
          saveButton.text = "Save Changes";

          saveButton.style.paddingTop = 8;
          saveButton.style.paddingBottom = 8;
          saveButton.style.paddingLeft = 40;
          saveButton.style.paddingRight = 40;
          saveButton.style.fontSize = 14;
          saveButton.style.unityFontStyleAndWeight = FontStyle.Bold;
          saveButton.style.backgroundColor = new StyleColor(new Color(0.15f, 0.35f, 0.15f, 1f));
          saveButton.style.borderTopWidth = 1;
          saveButton.style.borderBottomWidth = 1;
          saveButton.style.borderLeftWidth = 1;
          saveButton.style.borderRightWidth = 1;
          saveButton.style.borderTopColor = new StyleColor(new Color(0.3f, 0.5f, 0.3f, 1f));
          saveButton.style.borderBottomColor = new StyleColor(new Color(0.3f, 0.5f, 0.3f, 1f));
          saveButton.style.borderLeftColor = new StyleColor(new Color(0.3f, 0.5f, 0.3f, 1f));
          saveButton.style.borderRightColor = new StyleColor(new Color(0.3f, 0.5f, 0.3f, 1f));
          saveButton.style.borderTopLeftRadius = 4;
          saveButton.style.borderTopRightRadius = 4;
          saveButton.style.borderBottomLeftRadius = 4;
          saveButton.style.borderBottomRightRadius = 4;
          saveButton.style.color = new StyleColor(Color.white);

          saveButton.RegisterCallback<MouseEnterEvent>(evt => saveButton.style.backgroundColor = new StyleColor(new Color(0.2f, 0.45f, 0.2f, 1f)));
          saveButton.RegisterCallback<MouseLeaveEvent>(evt => saveButton.style.backgroundColor = new StyleColor(new Color(0.15f, 0.35f, 0.15f, 1f)));

          saveButton.RegisterCallback<ClickEvent>(evt =>
          {
            foreach (var kvp in pendingChanges)
            {
              if (kvp.Value is bool b) localConfig.Set(kvp.Key, b);
              else if (kvp.Value is int i) localConfig.Set(kvp.Key, i);
              else if (kvp.Value is float f) localConfig.Set(kvp.Key, f);
              else localConfig.Set(kvp.Key, kvp.Value?.ToString());
            }
            localConfig.Save();
            pendingChanges.Clear();

            saveButton.text = "Saved!";
            saveButton.schedule.Execute(() => saveButton.text = "Save Changes").StartingIn(1500);
          });

          saveWrapper.Add(saveButton);

          foldout.Add(saveWrapper);
          scrollView.Add(foldout);
        }
      }

      modalBackground.Add(mainWindow);
      return modalBackground;
    }

    private static ScrollView CreateScrollView()
    {
      ScrollView scrollView = new ScrollView();
      scrollView.style.flexGrow = 1;
      scrollView.verticalScrollerVisibility = ScrollerVisibility.AlwaysVisible;
      scrollView.horizontalScrollerVisibility = ScrollerVisibility.Hidden;

      var dragger = scrollView.Q<VisualElement>(className: "unity-base-slider__dragger");
      if (dragger != null)
      {
        dragger.style.width = 20;
        dragger.style.minHeight = 58;
        dragger.style.backgroundColor = Color.clear;
        dragger.style.borderTopWidth = dragger.style.borderBottomWidth = dragger.style.borderLeftWidth = dragger.style.borderRightWidth = 0;
        var tex = Resources.Load<Texture2D>("UI/Images/Core/vertical-scroll-button-nine-slice");
        if (tex != null)
        {
          dragger.style.backgroundImage = new StyleBackground(tex);
          dragger.style.unitySliceTop = dragger.style.unitySliceBottom = dragger.style.unitySliceLeft = dragger.style.unitySliceRight = 14;
        }
      }

      var tracker = scrollView.Q<VisualElement>(className: "unity-base-slider__tracker");
      if (tracker != null)
      {
        tracker.style.width = 20;
        tracker.style.backgroundColor = Color.clear;
        tracker.style.borderTopWidth = tracker.style.borderBottomWidth = tracker.style.borderLeftWidth = tracker.style.borderRightWidth = 0;
        var tex = Resources.Load<Texture2D>("UI/Images/Core/vertical-scroll-bar-nine-slice");
        if (tex != null)
        {
          tracker.style.backgroundImage = new StyleBackground(tex);
          tracker.style.unitySliceTop = tracker.style.unitySliceBottom = 16;
        }
      }

      return scrollView;
    }
  }
  #endregion

  #region 7. TIMBERBORN DROPDOWN ADAPTER
  public class SimpleConfigDropdownProvider : IExtendedDropdownProvider
  {
    private readonly Action<string> _setter;
    private readonly Func<string> _getter;
    private readonly List<string> _options;

    public IReadOnlyList<string> Items => _options;

    public SimpleConfigDropdownProvider(List<string> options, Func<string> getter, Action<string> setter)
    {
      _options = options;
      _getter = getter;
      _setter = setter;
    }

    public string GetValue() => _getter();
    public void SetValue(string value) => _setter(value);
    public string FormatDisplayText(string value, bool selected) => value;
    public Sprite GetIcon(string value) => null;
    public ImmutableArray<string> GetItemClasses(string value) => ImmutableArray<string>.Empty;
  }
  #endregion
}