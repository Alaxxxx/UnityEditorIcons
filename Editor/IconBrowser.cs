using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace UnityEditorIcons.Editor
{
      public sealed class IconBrowser : EditorWindow
      {
            private Vector2 _scrollPosition;
            private List<Texture2D> _allIcons;
            private List<Texture2D> _filteredIcons;
            private string _searchText = "";
            private bool _isInitialized;

            private enum SortMode
            {
                  NameAsc,
                  NameDesc,
                  Width,
                  Height
            }

            private SortMode _currentSortMode = SortMode.NameAsc;

            // UI Constants
            private const float CardTargetWidth = 150f;
            private const float CardHeight = 110f;
            private const float IconAreaHeight = 60f;
            private const float Padding = 10f;

            // Styles
            private GUIStyle _dimensionTextStyle;
            private GUIStyle _cardStyle;
            private GUIContent _copyButtonContent;

            [MenuItem("Tools/Icon Browser")]
            public static void ShowWindow()
            {
                  GetWindow<IconBrowser>("Icon Browser");
            }

            private void OnEnable()
            {
                  var copyIcon = EditorGUIUtility.IconContent("d_UnityEditor.FindDependencies").image as Texture2D;

                  _copyButtonContent = copyIcon != null ? new GUIContent(copyIcon, "Copy icon name to clipboard") : new GUIContent("C", "Copy icon name to clipboard");

                  _cardStyle = new GUIStyle(EditorStyles.helpBox)
                  {
                              padding = new RectOffset(5, 5, 5, 5)
                  };

                  _dimensionTextStyle = new GUIStyle(EditorStyles.label)
                  {
                              alignment = TextAnchor.MiddleCenter,
                              fontSize = 9,
                              normal = { textColor = Color.white }
                  };
            }

            private void OnGUI()
            {
                  if (!_isInitialized)
                  {
                        EditorGUILayout.LabelField("Loading icons, please wait...", EditorStyles.centeredGreyMiniLabel);

                        if (Event.current.type == EventType.Repaint)
                        {
                              InitializeAndSortIcons();
                        }

                        return;
                  }

                  DrawToolbar();
                  DrawIconGrid();
                  DrawBottomBar();
            }

            private void DrawToolbar()
            {
                  EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

                  string newSearchText = GUILayout.TextField(_searchText, GUI.skin.FindStyle("ToolbarSearchTextField"), GUILayout.MaxWidth(this.position.width / 2));

                  if (GUILayout.Button("", GUI.skin.FindStyle("ToolbarSearchCancelButton")))
                  {
                        newSearchText = "";
                        GUI.FocusControl(null);
                  }

                  if (newSearchText != _searchText)
                  {
                        _searchText = newSearchText;
                        FilterIcons();
                  }

                  GUILayout.FlexibleSpace();
                  var newSortMode = (SortMode)EditorGUILayout.EnumPopup(_currentSortMode, EditorStyles.toolbarDropDown, GUILayout.Width(100));

                  if (newSortMode != _currentSortMode)
                  {
                        _currentSortMode = newSortMode;
                        SortIcons();
                  }

                  EditorGUILayout.EndHorizontal();
            }

            private void DrawIconGrid()
            {
                  _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

                  int columnCount = Mathf.Max(1, Mathf.FloorToInt(this.position.width / CardTargetWidth));

                  if (_filteredIcons is { Count: > 0 })
                  {
                        for (int i = 0; i < _filteredIcons.Count; i += columnCount)
                        {
                              EditorGUILayout.BeginHorizontal();

                              for (int j = 0; j < columnCount; j++)
                              {
                                    if (i + j < _filteredIcons.Count)
                                    {
                                          DrawIconCard(_filteredIcons[i + j]);
                                    }
                              }

                              EditorGUILayout.EndHorizontal();
                        }
                  }
                  else
                  {
                        EditorGUILayout.LabelField("No icons match your search.", EditorStyles.centeredGreyMiniLabel);
                  }

                  EditorGUILayout.EndScrollView();
            }

            private void DrawIconCard(Texture2D icon)
            {
                  float cardWidth = (this.position.width / Mathf.Max(1, Mathf.FloorToInt(this.position.width / CardTargetWidth))) - Padding;

                  EditorGUILayout.BeginVertical(_cardStyle, GUILayout.Width(cardWidth), GUILayout.Height(CardHeight));

                  Rect iconAreaRect = GUILayoutUtility.GetRect(cardWidth, IconAreaHeight, GUILayout.ExpandWidth(false));

                  EditorGUI.DrawRect(iconAreaRect, new Color(0.2f, 0.2f, 0.2f, 1f));

                  float iconSize = Mathf.Min(icon.width, icon.height, IconAreaHeight - 10);

                  var iconRect = new Rect(iconAreaRect.x + (iconAreaRect.width - iconSize) / 2, iconAreaRect.y + (iconAreaRect.height - iconSize) / 2, iconSize,
                              iconSize);
                  GUI.DrawTexture(iconRect, icon);

                  string dimensions = $"{icon.width}x{icon.height}";
                  Vector2 dimTextSize = _dimensionTextStyle.CalcSize(new GUIContent(dimensions));
                  var dimBgRect = new Rect(iconAreaRect.xMax - dimTextSize.x - 6, iconAreaRect.yMax - dimTextSize.y - 4, dimTextSize.x + 2, dimTextSize.y + 2);
                  var dimTextRect = new Rect(dimBgRect.x - 1, dimBgRect.y - 1, dimBgRect.width, dimBgRect.height);

                  EditorGUI.DrawRect(dimBgRect, new Color(0.1f, 0.1f, 0.1f, 0.6f));
                  GUI.Label(dimTextRect, dimensions, _dimensionTextStyle);

                  GUILayout.FlexibleSpace();

                  EditorGUILayout.BeginHorizontal();
                  EditorGUILayout.SelectableLabel(icon.name, GUILayout.Height(20), GUILayout.ExpandWidth(true));

                  if (GUILayout.Button(_copyButtonContent, GUIStyle.none, GUILayout.Width(20), GUILayout.Height(20)))
                  {
                        EditorGUIUtility.systemCopyBuffer = icon.name;
                        Debug.Log($"Copied to clipboard: {icon.name}");
                  }

                  EditorGUILayout.EndHorizontal();

                  EditorGUILayout.EndVertical();
            }

            private void DrawBottomBar()
            {
                  EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
                  GUILayout.Label($"Displaying: {_filteredIcons.Count} / {_allIcons.Count} icons");
                  GUILayout.FlexibleSpace();

                  if (GUILayout.Button(new GUIContent("Generate Repo Files", "Generates the README.md and icons/ folder.")))
                  {
                        GenerateRepoFiles();
                  }

                  EditorGUILayout.EndHorizontal();
            }

            private void InitializeAndSortIcons()
            {
                  InitializeIcons();
                  SortIcons();
                  _isInitialized = true;
            }

            private void InitializeIcons()
            {
                  Debug.unityLogger.logEnabled = false;

                  try
                  {
                        _allIcons = Resources.FindObjectsOfTypeAll<Texture2D>()
                                             .Where(static t => t != null && !string.IsNullOrEmpty(t.name))
                                             .Where(static t => t.hideFlags == HideFlags.HideAndDontSave)
                                             .Where(static t => EditorUtility.IsPersistent(t))
                                             .Where(static t => EditorGUIUtility.IconContent(t.name).image)
                                             .Where(IsValidEditorIcon)
                                             .Distinct()
                                             .ToList();
                  }
                  finally
                  {
                        Debug.unityLogger.logEnabled = true;
                  }

                  _filteredIcons = new List<Texture2D>(_allIcons);

                  Debug.Log($"Found {_allIcons.Count} editor icons");
            }

            [SuppressMessage("ReSharper", "StringLiteralTypo")]
            private static bool IsValidEditorIcon(Texture2D texture)
            {
                  string name = texture.name.ToLower();

                  string[] excludePatterns =
                  {
                              "AnimationRow",
                              "btn",
                              "cmd",
                              "box",
                              "bg_",
                              "button",
                              "cn ",
                              "cnentry",
                              "ColorField",
                              "d_rol",
                              "darkview",
                              "dockarea",
                              "dropwell",
                              "gameviewbackground",
                              "grey_border",
                              "iconselector",
                              "inlined ",
                              "IN Title",
                              "mini",
                              "ObjectPicker",
                              "OL Highlight",
                              "PB-",
                              "Pre ",
                              "ProfilerLeft",
                              "ProfilerRight",
                              "progress ",
                              "ProjectBrowser",
                              "pulldown",
                              "ro_",
                              "scroll ",
                              "selected",
                              "slider ",
                              "tabbar",
                              "TE ",
                              "textfield",
                              "toolbar",
                              "transparent",
                              "unselected",
                              "window ",
                  };

                  foreach (string pattern in excludePatterns)
                  {
                        if (name.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                        {
                              return false;
                        }
                  }

                  return true;
            }

            private void SortIcons()
            {
                  _filteredIcons = _currentSortMode switch
                  {
                              SortMode.NameAsc => _filteredIcons.OrderBy(static t => t.name).ToList(),
                              SortMode.NameDesc => _filteredIcons.OrderByDescending(static t => t.name).ToList(),
                              SortMode.Width => _filteredIcons.OrderBy(static t => t.width).ThenBy(static t => t.height).ToList(),
                              SortMode.Height => _filteredIcons.OrderBy(static t => t.height).ThenBy(static t => t.width).ToList(),
                              _ => _filteredIcons
                  };
            }

            private void FilterIcons()
            {
                  _filteredIcons = _allIcons.Where(t => t.name.ToLower().Contains(_searchText.ToLower(), StringComparison.Ordinal)).ToList();
                  SortIcons();
            }

            private string GetRepoRootPath()
            {
                  MonoScript script = MonoScript.FromScriptableObject(this);
                  string scriptPath = AssetDatabase.GetAssetPath(script);
                  string editorFolderPath = Path.GetDirectoryName(scriptPath);

                  return Path.GetDirectoryName(editorFolderPath);
            }

            private void GenerateRepoFiles()
            {
                  string rootPath = GetRepoRootPath();

                  if (string.IsNullOrEmpty(rootPath) || !Directory.Exists(rootPath))
                  {
                        Debug.LogError("Could not determine the repository root path.");

                        return;
                  }

                  string iconsPath = Path.Combine(rootPath, "icons");
                  Directory.CreateDirectory(iconsPath);
                  List<string> failedExports = ExportAllIcons(iconsPath);
                  int successCount = _allIcons.Count - failedExports.Count;
                  GenerateMarkdown(rootPath, _allIcons.Where(icon => !failedExports.Contains(icon.name)).ToList());
                  string report = $"{successCount} icons successfully exported.\n";

                  if (failedExports.Count > 0)
                  {
                        report += $"{failedExports.Count} icons failed to export. Check console for details.";
                  }

                  EditorUtility.DisplayDialog("Repository Files Generated", report, "OK");
            }

            private List<string> ExportAllIcons(string exportPath)
            {
                  var failedExports = new List<string>();

                  foreach (Texture2D icon in _allIcons)
                  {
                        try
                        {
                              Texture2D readableTexture = DuplicateTexture(icon);

                              if (!readableTexture)
                              {
                                    throw new Exception("Duplicated texture was null.");
                              }

                              byte[] bytes = readableTexture.EncodeToPNG();
                              string fileName = string.Concat(icon.name.Split(Path.GetInvalidFileNameChars()));
                              File.WriteAllBytes(Path.Combine(exportPath, fileName + ".png"), bytes);
                              DestroyImmediate(readableTexture);
                        }
                        catch (Exception ex)
                        {
                              Debug.LogWarning($"Failed to export icon '{icon.name}': {ex.Message}");
                              failedExports.Add(icon.name);
                        }
                  }

                  return failedExports;
            }

            private static void GenerateMarkdown(string rootPath, IEnumerable<Texture2D> successfulIcons)
            {
                  string savePath = Path.Combine(rootPath, "README.md");
                  const string user = "alaxxxx";
                  const string repo = "unityeditoricons";

                  List<Texture2D> iconsList = successfulIcons.ToList();
                  int totalIcons = iconsList.Count;

                  using var writer = new StreamWriter(savePath);

                  writer.WriteLine($"# Unity Editor Icons ({Application.unityVersion})");
                  writer.WriteLine();
                  writer.WriteLine($"**{totalIcons} icons** available for Unity {Application.unityVersion}");
                  writer.WriteLine();

                  // Badges
                  writer.WriteLine($"![Unity Version](https://img.shields.io/badge/Unity-{Application.unityVersion}-purple.svg)");
                  writer.WriteLine($"![Icons Count](https://img.shields.io/badge/Icons-{totalIcons}-blue.svg)");
                  writer.WriteLine($"![GitHub last commit](https://img.shields.io/github/last-commit/{user}/{repo})");
                  writer.WriteLine($"[![GitHub license](https://img.shields.io/github/license/{user}/{repo})](LICENSE)");
                  writer.WriteLine($"[![GitHub release (latest by date)](https://img.shields.io/github/v/release/{user}/{repo})](/{user}/{repo}/releases/latest)");
                  writer.WriteLine();

                  // Description
                  writer.WriteLine("This project provides two main features:");
                  writer.WriteLine();

                  writer.WriteLine(
                              "1. **An Editor Window for Unity**: Browse, search, and copy the names of all internal editor icons directly within Unity. Perfect for creating custom editor tools with a native look and feel.");
                  writer.WriteLine();

                  writer.WriteLine(
                              "2. **A README Generator**: Automatically generates this documentation with previews of all icons, keeping the repository up-to-date with new Unity versions.");
                  writer.WriteLine();

                  // Installation
                  writer.WriteLine("## 🚀 Installation");
                  writer.WriteLine();
                  writer.WriteLine("1. Download the latest release or clone this repository");
                  writer.WriteLine("2. Open the Icon Browser via **Tools > Icon Browser**");
                  writer.WriteLine();

                  // Usage
                  writer.WriteLine("## 📖 Usage");
                  writer.WriteLine();
                  writer.WriteLine("```csharp");
                  writer.WriteLine("// Basic usage");
                  writer.WriteLine("GUIContent icon = EditorGUIUtility.IconContent(\"d_Toolbar Plus\");");
                  writer.WriteLine();
                  writer.WriteLine("// In buttons");
                  writer.WriteLine("if (GUILayout.Button(icon, GUILayout.Width(30)))");
                  writer.WriteLine("    Debug.Log(\"Clicked!\");");
                  writer.WriteLine();
                  writer.WriteLine("// In toolbars");
                  writer.WriteLine("if (GUILayout.Button(EditorGUIUtility.IconContent(\"d_Refresh\"), EditorStyles.toolbarButton))");
                  writer.WriteLine("    RefreshData();");
                  writer.WriteLine();
                  writer.WriteLine("```");
                  writer.WriteLine();

                  // Quick stats
                  IconStats stats = GetIconStats(iconsList);
                  writer.WriteLine("## 📊 Icon Statistics");
                  writer.WriteLine();
                  writer.WriteLine($"- **Total Icons**: {totalIcons}");
                  writer.WriteLine($"- **Most Common Size**: {stats.MostCommonSize}");
                  writer.WriteLine($"- **Size Range**: {stats.MinSize}×{stats.MinSize} to {stats.MaxSize}×{stats.MaxSize}");
                  writer.WriteLine();

                  // Icon table
                  writer.WriteLine($"## 🎨 All Icons ({totalIcons})");
                  writer.WriteLine();
                  writer.WriteLine("| Preview | Dimensions | Name (for `EditorGUIUtility.IconContent`) |");
                  writer.WriteLine("|:---:|:---:|---|");

                  foreach (Texture2D icon in iconsList.OrderBy(static i => i.name))
                  {
                        string imageName = string.Concat(icon.name.Split(Path.GetInvalidFileNameChars()));
                        string preview = $"<img src=\"icons/{imageName}.png\" width=\"24\" alt=\"{icon.name}\">";
                        string dims = $"`{icon.width}×{icon.height}`";
                        string code = $"```\n{icon.name}\n```";
                        writer.WriteLine($"| {preview} | {dims} | {code} |");
                  }

                  // Footer
                  writer.WriteLine();
                  writer.WriteLine("---");
                  writer.WriteLine();
                  writer.WriteLine($"*Generated automatically on {DateTimeOffset.Now:yyyy-MM-dd} for Unity {Application.unityVersion}*");
            }

            private static IconStats GetIconStats(List<Texture2D> icons)
            {
                  var stats = new IconStats();

                  IEnumerable<int> sizes = icons.Select(static i => i.width).Where(w => w == icons.First(ic => ic.width == w).height);
                  IOrderedEnumerable<IGrouping<int, int>> sizeGroups = sizes.GroupBy(static s => s).OrderByDescending(static g => g.Count());
                  stats.MostCommonSize = sizeGroups.FirstOrDefault()?.Key ?? 16;

                  stats.MinSize = icons.Min(static i => Math.Min(i.width, i.height));
                  stats.MaxSize = icons.Max(static i => Math.Max(i.width, i.height));

                  return stats;
            }

            private struct IconStats
            {
                  public int DarkIcons;
                  public int MostCommonSize;
                  public int MinSize;
                  public int MaxSize;
            }

            private static Texture2D DuplicateTexture(Texture2D source)
            {
                  if (!source)
                  {
                        return null;
                  }

                  RenderTexture renderTex = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.Default, RenderTextureReadWrite.Default);
                  Graphics.Blit(source, renderTex);
                  RenderTexture previous = RenderTexture.active;
                  RenderTexture.active = renderTex;
                  var readableText = new Texture2D(source.width, source.height);
                  readableText.ReadPixels(new Rect(0, 0, renderTex.width, renderTex.height), 0, 0);
                  readableText.Apply();
                  RenderTexture.active = previous;
                  RenderTexture.ReleaseTemporary(renderTex);

                  return readableText;
            }
      }
}