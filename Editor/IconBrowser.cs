using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace UnityEditorIcons.Editor
{
      public sealed class IconBrowser : EditorWindow
      {
            private Vector2 scrollPosition;
            private List<Texture2D> allIcons;
            private List<Texture2D> filteredIcons;
            private string searchText = "";
            private bool isInitialized;

            private enum SortMode
            {
                  NameAsc,
                  NameDesc,
                  Width,
                  Height
            }

            private SortMode currentSortMode = SortMode.NameAsc;

            // UI Constants
            private const float CardTargetWidth = 150f;
            private const float CardHeight = 110f;
            private const float IconAreaHeight = 60f;
            private const float Padding = 10f;

            // Styles
            private GUIStyle dimensionTextStyle;
            private GUIStyle cardStyle;
            private GUIContent copyButtonContent;

            [MenuItem("Tools/Icon Browser")]
            public static void ShowWindow()
            {
                  GetWindow<IconBrowser>("Icon Browser");
            }

            private void OnEnable()
            {
                  var copyIcon = EditorGUIUtility.IconContent("d_UnityEditor.FindDependencies").image as Texture2D;

                  copyButtonContent = copyIcon != null ? new GUIContent(copyIcon, "Copy icon name to clipboard") : new GUIContent("C", "Copy icon name to clipboard");

                  cardStyle = new GUIStyle(EditorStyles.helpBox)
                  {
                              padding = new RectOffset(5, 5, 5, 5)
                  };

                  dimensionTextStyle = new GUIStyle(EditorStyles.label)
                  {
                              alignment = TextAnchor.MiddleCenter,
                              fontSize = 9,
                              normal = { textColor = Color.white }
                  };
            }

            private void OnGUI()
            {
                  if (!isInitialized)
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

                  string newSearchText = GUILayout.TextField(searchText, GUI.skin.FindStyle("ToolbarSearchTextField"), GUILayout.MaxWidth(this.position.width / 2));

                  if (GUILayout.Button("", GUI.skin.FindStyle("ToolbarSearchCancelButton")))
                  {
                        newSearchText = "";
                        GUI.FocusControl(null);
                  }

                  if (newSearchText != searchText)
                  {
                        searchText = newSearchText;
                        FilterIcons();
                  }

                  GUILayout.FlexibleSpace();
                  var newSortMode = (SortMode)EditorGUILayout.EnumPopup(currentSortMode, EditorStyles.toolbarDropDown, GUILayout.Width(100));

                  if (newSortMode != currentSortMode)
                  {
                        currentSortMode = newSortMode;
                        SortIcons();
                  }

                  EditorGUILayout.EndHorizontal();
            }

            private void DrawIconGrid()
            {
                  scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

                  int columnCount = Mathf.Max(1, Mathf.FloorToInt(this.position.width / CardTargetWidth));

                  if (filteredIcons is { Count: > 0 })
                  {
                        for (int i = 0; i < filteredIcons.Count; i += columnCount)
                        {
                              EditorGUILayout.BeginHorizontal();

                              for (int j = 0; j < columnCount; j++)
                              {
                                    if (i + j < filteredIcons.Count)
                                    {
                                          DrawIconCard(filteredIcons[i + j]);
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

                  EditorGUILayout.BeginVertical(cardStyle, GUILayout.Width(cardWidth), GUILayout.Height(CardHeight));

                  Rect iconAreaRect = GUILayoutUtility.GetRect(cardWidth, IconAreaHeight, GUILayout.ExpandWidth(false));

                  EditorGUI.DrawRect(iconAreaRect, new Color(0.2f, 0.2f, 0.2f, 1f));

                  float iconSize = Mathf.Min(icon.width, icon.height, IconAreaHeight - 10);

                  var iconRect = new Rect(iconAreaRect.x + (iconAreaRect.width - iconSize) / 2, iconAreaRect.y + (iconAreaRect.height - iconSize) / 2, iconSize,
                              iconSize);
                  GUI.DrawTexture(iconRect, icon);

                  string dimensions = $"{icon.width}x{icon.height}";
                  Vector2 dimTextSize = dimensionTextStyle.CalcSize(new GUIContent(dimensions));
                  var dimBgRect = new Rect(iconAreaRect.xMax - dimTextSize.x - 6, iconAreaRect.yMax - dimTextSize.y - 4, dimTextSize.x + 2, dimTextSize.y + 2);
                  var dimTextRect = new Rect(dimBgRect.x - 1, dimBgRect.y - 1, dimBgRect.width, dimBgRect.height);

                  EditorGUI.DrawRect(dimBgRect, new Color(0.1f, 0.1f, 0.1f, 0.6f));
                  GUI.Label(dimTextRect, dimensions, dimensionTextStyle);

                  GUILayout.FlexibleSpace();

                  EditorGUILayout.BeginHorizontal();
                  EditorGUILayout.SelectableLabel(icon.name, GUILayout.Height(20), GUILayout.ExpandWidth(true));

                  if (GUILayout.Button(copyButtonContent, GUIStyle.none, GUILayout.Width(20), GUILayout.Height(20)))
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
                  GUILayout.Label($"Displaying: {filteredIcons.Count} / {allIcons.Count} icons");
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
                  isInitialized = true;
            }

            private void InitializeIcons()
            {
                  Debug.unityLogger.logEnabled = false;

                  try
                  {
                        allIcons = Resources.FindObjectsOfTypeAll<Texture2D>()
                                            .Where(static t =>
                                                        t != null && !string.IsNullOrEmpty(t.name) && t.hideFlags == HideFlags.HideAndDontSave &&
                                                        EditorUtility.IsPersistent(t))
                                            .Where(static t => EditorGUIUtility.IconContent(t.name).image)
                                            .Distinct()
                                            .ToList();
                  }
                  finally
                  {
                        Debug.unityLogger.logEnabled = true;
                  }

                  filteredIcons = new List<Texture2D>(allIcons);
            }

            private void SortIcons()
            {
                  filteredIcons = currentSortMode switch
                  {
                              SortMode.NameAsc => filteredIcons.OrderBy(static t => t.name).ToList(),
                              SortMode.NameDesc => filteredIcons.OrderByDescending(static t => t.name).ToList(),
                              SortMode.Width => filteredIcons.OrderBy(static t => t.width).ThenBy(static t => t.height).ToList(),
                              SortMode.Height => filteredIcons.OrderBy(static t => t.height).ThenBy(static t => t.width).ToList(),
                              _ => filteredIcons
                  };
            }

            private void FilterIcons()
            {
                  filteredIcons = allIcons.Where(t => t.name.ToLower().Contains(searchText.ToLower(), StringComparison.Ordinal)).ToList();
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
                  int successCount = allIcons.Count - failedExports.Count;
                  GenerateMarkdown(rootPath, allIcons.Where(icon => !failedExports.Contains(icon.name)).ToList());
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

                  foreach (Texture2D icon in allIcons)
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

                  using var writer = new StreamWriter(savePath);

                  writer.WriteLine($"# Unity Editor Icons ({Application.unityVersion})");
                  writer.WriteLine();

                  writer.WriteLine($"![Unity Version](https://img.shields.io/badge/Unity-{Application.unityVersion}-purple.svg)");
                  writer.WriteLine($"![GitHub last commit](https://img.shields.io/github/last-commit/{user}/{repo})");
                  writer.WriteLine($"[![GitHub license](https://img.shields.io/github/license/{user}/{repo})](LICENSE)");
                  writer.WriteLine($"[![GitHub release (latest by date)](https://img.shields.io/github/v/release/{user}/{repo})](/{user}/{repo}/releases/latest)");
                  writer.WriteLine();

                  writer.WriteLine("This project provides two main features:");

                  writer.WriteLine(
                              "1.  **An Editor Window for Unity**: You can browse, search, and copy the names of all internal editor icons directly within Unity. This is very useful for creating custom editor tools with a native look and feel.");

                  writer.WriteLine(
                              "2.  **A `README.md` Generator**: The tool can also generate this `README` file, including previews of all the icons, to keep the repository up-to-date with new Unity versions.");
                  writer.WriteLine();

                  writer.WriteLine("| Preview | Dimensions | Name (for `IconContent`) |");
                  writer.WriteLine("|:---:|:---:|---|");

                  foreach (Texture2D icon in successfulIcons.OrderBy(static i => i.name))
                  {
                        string imageName = string.Concat(icon.name.Split(Path.GetInvalidFileNameChars()));
                        string preview = $"<img src=\"icons/{imageName}.png\" width=\"24\">";
                        string dims = $"`{icon.width}x{icon.height}`";
                        string code = $"```{icon.name}```";
                        writer.WriteLine($"| {preview} | {dims} | {code} |");
                  }
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