using UnityEditor;
using UnityEngine;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Linq;

namespace UnityAgents.Editor
{
    public static class MarkdownRenderer
    {
        private static GUIStyle normalStyle;
        private static GUIStyle boldStyle;
        private static GUIStyle italicStyle;
        private static GUIStyle codeStyle;
        private static GUIStyle headingStyle;
        private static GUIStyle linkStyle;
        private static GUIStyle tableHeaderStyle;
        private static GUIStyle tableCellStyle;
        private static bool stylesInitialized = false;

        private static void InitializeStyles()
        {
            if (stylesInitialized) return;

            normalStyle = new GUIStyle(EditorStyles.label)
            {
                wordWrap = true,
                richText = true
            };

            boldStyle = new GUIStyle(normalStyle)
            {
                fontStyle = FontStyle.Bold
            };

            italicStyle = new GUIStyle(normalStyle)
            {
                fontStyle = FontStyle.Italic
            };

            codeStyle = new GUIStyle(EditorStyles.textArea)
            {
                wordWrap = true,
                richText = false,
                fontSize = 11,
                padding = new RectOffset(5, 5, 5, 5)
            };

            headingStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 16,
                wordWrap = true
            };

            linkStyle = new GUIStyle(normalStyle)
            {
                normal = { textColor = new Color(0.3f, 0.5f, 1f) }
            };

            tableHeaderStyle = new GUIStyle(EditorStyles.label)
            {
                fontStyle = FontStyle.Bold,
                wordWrap = true,
                richText = true,
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(5, 5, 2, 2)
            };

            tableCellStyle = new GUIStyle(EditorStyles.label)
            {
                wordWrap = true,
                richText = true,
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(5, 5, 2, 2)
            };

            stylesInitialized = true;
        }

        public static void RenderMarkdown(string markdown)
        {
            InitializeStyles();

            if (string.IsNullOrEmpty(markdown))
            {
                EditorGUILayout.LabelField("(empty)", italicStyle);
                return;
            }

            var lines = markdown.Split('\n');
            bool inCodeBlock = false;
            string codeBlockContent = "";
            bool inTable = false;
            List<string> tableLines = new List<string>();

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];

                // Code block detection
                if (line.TrimStart().StartsWith("```"))
                {
                    if (inCodeBlock)
                    {
                        // End of code block
                        EditorGUILayout.TextArea(codeBlockContent.TrimEnd(), codeStyle);
                        codeBlockContent = "";
                        inCodeBlock = false;
                    }
                    else
                    {
                        // Start of code block
                        inCodeBlock = true;
                    }
                    continue;
                }

                if (inCodeBlock)
                {
                    codeBlockContent += line + "\n";
                    continue;
                }

                // Table detection
                bool isTableLine = line.Contains("|") && line.Trim().Length > 0;
                
                if (isTableLine)
                {
                    if (!inTable)
                    {
                        inTable = true;
                        tableLines.Clear();
                    }
                    tableLines.Add(line);
                    continue;
                }
                else if (inTable)
                {
                    // End of table, render it
                    RenderTable(tableLines);
                    inTable = false;
                    tableLines.Clear();
                }

                // Headings
                if (line.StartsWith("# "))
                {
                    var heading = new GUIStyle(headingStyle) { fontSize = 18 };
                    EditorGUILayout.LabelField(line.Substring(2), heading);
                    continue;
                }
                if (line.StartsWith("## "))
                {
                    var heading = new GUIStyle(headingStyle) { fontSize = 16 };
                    EditorGUILayout.LabelField(line.Substring(3), heading);
                    continue;
                }
                if (line.StartsWith("### "))
                {
                    var heading = new GUIStyle(headingStyle) { fontSize = 14 };
                    EditorGUILayout.LabelField(line.Substring(4), heading);
                    continue;
                }

                // Horizontal rule
                if (line.Trim() == "---" || line.Trim() == "***")
                {
                    EditorGUILayout.Space(5);
                    EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
                    EditorGUILayout.Space(5);
                    continue;
                }

                // Process inline markdown
                string processedLine = ProcessInlineMarkdown(line);
                
                // Lists
                if (line.TrimStart().StartsWith("- ") || line.TrimStart().StartsWith("* "))
                {
                    int indentLevel = line.Length - line.TrimStart().Length;
                    string bullet = new string(' ', indentLevel) + "• " + processedLine.TrimStart().Substring(2);
                    EditorGUILayout.LabelField(bullet, normalStyle);
                }
                else if (Regex.IsMatch(line.TrimStart(), @"^\d+\.\s"))
                {
                    EditorGUILayout.LabelField(processedLine, normalStyle);
                }
                else if (!string.IsNullOrWhiteSpace(line))
                {
                    EditorGUILayout.LabelField(processedLine, normalStyle);
                }
                else
                {
                    EditorGUILayout.Space(3);
                }
            }

            // Handle unclosed code block
            if (inCodeBlock)
            {
                EditorGUILayout.TextArea(codeBlockContent.TrimEnd(), codeStyle);
            }

            // Handle unclosed table
            if (inTable && tableLines.Count > 0)
            {
                RenderTable(tableLines);
            }
        }

        private static void RenderTable(List<string> tableLines)
        {
            if (tableLines.Count < 2) return; // Need at least header and separator

            // Parse table rows
            List<List<string>> rows = new List<List<string>>();
            int separatorIndex = -1;

            for (int i = 0; i < tableLines.Count; i++)
            {
                string line = tableLines[i].Trim();
                
                // Check if this is a separator line (e.g., |---|---|)
                if (Regex.IsMatch(line, @"^\|?\s*[-:]+\s*(\|\s*[-:]+\s*)+\|?\s*$"))
                {
                    separatorIndex = i;
                    continue;
                }

                // Parse cells
                var cells = ParseTableRow(line);
                if (cells.Count > 0)
                {
                    rows.Add(cells);
                }
            }

            if (rows.Count == 0) return;

            // Determine column count
            int columnCount = rows.Max(r => r.Count);

            EditorGUILayout.Space(5);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // Render header row (first row before separator, or just first row if no separator)
            int headerRowCount = separatorIndex >= 0 ? Mathf.Min(separatorIndex, rows.Count) : 1;
            
            for (int rowIdx = 0; rowIdx < headerRowCount; rowIdx++)
            {
                EditorGUILayout.BeginHorizontal();
                var row = rows[rowIdx];
                
                for (int i = 0; i < columnCount; i++)
                {
                    string cellContent = i < row.Count ? ProcessInlineMarkdown(row[i]) : "";
                    EditorGUILayout.LabelField(cellContent, tableHeaderStyle, GUILayout.ExpandWidth(true));
                }
                
                EditorGUILayout.EndHorizontal();
            }

            // Separator line
            if (headerRowCount > 0)
            {
                EditorGUILayout.Space(2);
                var separatorRect = EditorGUILayout.GetControlRect(false, 1);
                EditorGUI.DrawRect(separatorRect, new Color(0.5f, 0.5f, 0.5f, 0.5f));
                EditorGUILayout.Space(2);
            }

            // Render data rows
            for (int rowIdx = headerRowCount; rowIdx < rows.Count; rowIdx++)
            {
                EditorGUILayout.BeginHorizontal();
                var row = rows[rowIdx];
                
                for (int i = 0; i < columnCount; i++)
                {
                    string cellContent = i < row.Count ? ProcessInlineMarkdown(row[i]) : "";
                    EditorGUILayout.LabelField(cellContent, tableCellStyle, GUILayout.ExpandWidth(true));
                }
                
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(5);
        }

        private static List<string> ParseTableRow(string line)
        {
            var cells = new List<string>();
            
            // Remove leading and trailing pipes
            line = line.Trim();
            if (line.StartsWith("|")) line = line.Substring(1);
            if (line.EndsWith("|")) line = line.Substring(0, line.Length - 1);

            // Split by pipe, respecting escaped pipes
            var parts = line.Split('|');
            
            foreach (var part in parts)
            {
                cells.Add(part.Trim());
            }

            return cells;
        }

        private static string ProcessInlineMarkdown(string text)
        {
            // Bold **text** or __text__
            text = Regex.Replace(text, @"\*\*(.+?)\*\*", "<b>$1</b>");
            text = Regex.Replace(text, @"__(.+?)__", "<b>$1</b>");

            // Italic *text* or _text_
            text = Regex.Replace(text, @"\*(.+?)\*", "<i>$1</i>");
            text = Regex.Replace(text, @"(?<!_)_([^_]+?)_(?!_)", "<i>$1</i>");

            // Inline code `code`
            text = Regex.Replace(text, @"`(.+?)`", "<color=#D4D4D4><b>$1</b></color>");

            // Links [text](url) - just show the text for now
            text = Regex.Replace(text, @"\[(.+?)\]\(.+?\)", "<color=#6495ED>$1</color>");

            return text;
        }
    }
}
