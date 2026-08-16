// Developer: heaplyn
// Date: 2026-08-16
// Summary: High-performance syntax highlighter for WPF RichTextBox.
//          Applies formatting to the EXISTING document to preserve the Undo/Redo stack.

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Linq;

namespace JarvisLauncher
{
    public static class SyntaxHighlighter
    {
        public static void Highlight(RichTextBox rtb, string extension)
        {
            if (!EditorIntelligenceManager.SyntaxHighlightingRules.TryGetValue(extension, out var rules)) return;

            // Use TextRange to get and set properties without wiping the document
            var totalRange = new TextRange(rtb.Document.ContentStart, rtb.Document.ContentEnd);
            string text = totalRange.Text.Replace("\r\n", "\n");

            if (text.Length > 100000) return;

            // 1. Clear all formatting FIRST (reset to default)
            totalRange.ClearAllProperties();
            totalRange.ApplyPropertyValue(TextElement.ForegroundProperty, Brushes.White);
            totalRange.ApplyPropertyValue(TextElement.FontWeightProperty, FontWeights.Normal);

            // 2. Find all matches
            var matches = new List<(int Index, int Length, SyntaxRule Rule)>();
            foreach (var rule in rules)
            {
                foreach (Match m in Regex.Matches(text, rule.Pattern, RegexOptions.Compiled | RegexOptions.Multiline))
                {
                    matches.Add((m.Index, m.Length, rule));
                }
            }

            // 3. Sort by position and apply forward-only
            var sorted = matches.OrderBy(m => m.Index).ToList();

            // To avoid quadratic performance, we use a single forward-moving pointer
            TextPointer currentPointer = rtb.Document.ContentStart;
            int lastOffset = 0;

            foreach (var m in sorted)
            {
                // Move to start of match
                TextPointer start = GetPointAtOffset(currentPointer, m.Index - lastOffset);
                if (start == null) break;

                // Move to end of match
                TextPointer end = GetPointAtOffset(start, m.Length);
                if (end == null) break;

                var matchRange = new TextRange(start, end);
                try {
                    var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(m.Rule.ColorHex));
                    matchRange.ApplyPropertyValue(TextElement.ForegroundProperty, brush);
                    if (m.Rule.IsBold) matchRange.ApplyPropertyValue(TextElement.FontWeightProperty, FontWeights.Bold);
                } catch { }

                // Sync for next iteration
                currentPointer = start;
                lastOffset = m.Index;
            }
        }

        private static TextPointer GetPointAtOffset(TextPointer start, int offset)
        {
            TextPointer p = start;
            int count = 0;
            while (p != null && count < offset)
            {
                var context = p.GetPointerContext(LogicalDirection.Forward);
                if (context == TextPointerContext.Text)
                {
                    int runLength = p.GetTextInRun(LogicalDirection.Forward).Length;
                    if (count + runLength >= offset)
                    {
                        return p.GetPositionAtOffset(offset - count);
                    }
                    count += runLength;
                }
                p = p.GetNextContextPosition(LogicalDirection.Forward);
            }
            return (count == offset) ? p : null;
        }
    }
}
