// Developer: heaplyn
// Date: 2026-08-09
// Summary: Handles CLI commands to manage a local tasks/todo list saved persistently in a JSON database file.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows;

namespace JarvisLauncher
{
    public class TodoItem
    {
        public string Task { get; set; } = string.Empty;
        public bool IsCompleted { get; set; } = false;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }

    public class TodoCommandHandler : ICommandHandler
    {
        public bool CanHandle(string query)
        {
            query = query.Trim().ToLower();
            return query == "todo" || query.StartsWith("todo ") || query == "tasks" || query.StartsWith("tasks ");
        }

        public List<CommandResult> GetSuggestions(string query)
        {
            var suggestions = new List<CommandResult>();
            query = query.Trim();
            var parts = query.Split(' ', 3, StringSplitOptions.RemoveEmptyEntries);

            string cmd = parts[0].ToLower();
            double similarity = 2.0; // High priority match

            if (parts.Length > 1)
            {
                string action = parts[1].ToLower();

                if (action == "add" && parts.Length > 2)
                {
                    string task = parts[2].Trim();
                    suggestions.Add(new CommandResult
                    {
                        Title       = $"Add Task: \"{task}\"",
                        Description = "Add a new active task to your Todo list database",
                        Similarity  = similarity,
                        Execute     = () => AddTask(task)
                    });
                }
                else if (action == "done" && parts.Length > 2)
                {
                    if (int.TryParse(parts[2], out int idx))
                    {
                        suggestions.Add(new CommandResult
                        {
                            Title       = $"Complete Task #{idx}",
                            Description = "Mark the selected task as completed",
                            Similarity  = similarity,
                            Execute     = () => CompleteTask(idx)
                        });
                    }
                }
                else if ((action == "delete" || action == "remove") && parts.Length > 2)
                {
                    if (int.TryParse(parts[2], out int idx))
                    {
                        suggestions.Add(new CommandResult
                        {
                            Title       = $"Delete Task #{idx}",
                            Description = "Remove the selected task from your list permanently",
                            Similarity  = similarity,
                            Execute     = () => DeleteTask(idx)
                        });
                    }
                }
                else if (action == "clear")
                {
                    suggestions.Add(new CommandResult
                    {
                        Title       = "Clear Completed Tasks",
                        Description = "Purge all completed items from the list database",
                        Similarity  = similarity,
                        Execute     = () => ClearCompletedTasks()
                    });
                }
                else if (action == "list")
                {
                    suggestions.Add(new CommandResult
                    {
                        Title       = "Display Tasks List",
                        Description = "Print all active and completed tasks in the terminal",
                        Similarity  = similarity,
                        Execute     = () => ListTasks()
                    });
                }
            }
            else
            {
                // No action specified, default suggestions
                suggestions.Add(new CommandResult
                {
                    Title       = "List Todo Tasks",
                    Description = "Display all currently tracked tasks in the system terminal",
                    Similarity  = similarity,
                    Execute     = () => ListTasks()
                });

                suggestions.Add(new CommandResult
                {
                    Title       = "Add Task...",
                    Description = "Type task content (e.g. todo add buy groceries)",
                    Similarity  = similarity - 0.5,
                    Execute     = null
                });
            }

            return suggestions;
        }

        private static string GetTodoPath()
        {
            string dataDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data");
            if (!Directory.Exists(dataDir))
            {
                string devPath = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..\Data"));
                if (Directory.Exists(devPath))
                {
                    dataDir = devPath;
                }
                else
                {
                    Directory.CreateDirectory(dataDir);
                }
            }
            return Path.Combine(dataDir, "TodoList.json");
        }

        private static List<TodoItem> LoadTasks()
        {
            try
            {
                string path = GetTodoPath();
                if (File.Exists(path))
                {
                    string json = File.ReadAllText(path);
                    return JsonSerializer.Deserialize<List<TodoItem>>(json) ?? new List<TodoItem>();
                }
            }
            catch { }
            return new List<TodoItem>();
        }

        private static void SaveTasks(List<TodoItem> tasks)
        {
            try
            {
                string path = GetTodoPath();
                string json = JsonSerializer.Serialize(tasks, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(path, json);
            }
            catch (Exception ex)
            {
                TextOverlay.Show($"⚠️ Failed to save Todo DB: {ex.Message}", 3000);
            }
        }

        private static void AddTask(string task)
        {
            var tasks = LoadTasks();
            tasks.Add(new TodoItem { Task = task });
            SaveTasks(tasks);
            TextOverlay.Show($"✅ Task Added:\n\"{task}\"", 2500);
        }

        private static void CompleteTask(int userIndex)
        {
            var tasks = LoadTasks();
            int idx = userIndex - 1;

            if (idx >= 0 && idx < tasks.Count)
            {
                tasks[idx].IsCompleted = true;
                SaveTasks(tasks);
                TextOverlay.Show($"✓ Completed: \"{tasks[idx].Task}\"", 2500);
            }
            else
            {
                TextOverlay.Show($"⚠️ Invalid task index: {userIndex}", 3000);
            }
        }

        private static void DeleteTask(int userIndex)
        {
            var tasks = LoadTasks();
            int idx = userIndex - 1;

            if (idx >= 0 && idx < tasks.Count)
            {
                string name = tasks[idx].Task;
                tasks.RemoveAt(idx);
                SaveTasks(tasks);
                TextOverlay.Show($"🗑️ Deleted: \"{name}\"", 2500);
            }
            else
            {
                TextOverlay.Show($"⚠️ Invalid task index: {userIndex}", 3000);
            }
        }

        private static void ClearCompletedTasks()
        {
            var tasks = LoadTasks();
            int countBefore = tasks.Count;
            tasks.RemoveAll(t => t.IsCompleted);
            int deleted = countBefore - tasks.Count;
            SaveTasks(tasks);
            TextOverlay.Show($"🧹 Purged {deleted} completed tasks!", 2500);
        }

        private static void ListTasks()
        {
            var tasks = LoadTasks();
            var sb = new StringBuilder();

            sb.AppendLine("===================================================");
            sb.AppendLine("                 JARVIS TODO SYSTEM                ");
            sb.AppendLine("===================================================");
            sb.AppendLine();

            if (tasks.Count == 0)
            {
                sb.AppendLine("[No tasks currently in your list. Type 'todo add <task>' to create one.]");
            }
            else
            {
                for (int i = 0; i < tasks.Count; i++)
                {
                    var item = tasks[i];
                    string status = item.IsCompleted ? "[✓] DONE" : "[ ] TODO";
                    sb.AppendLine($"{i + 1}. {status,-8} - {item.Task}  (added {item.CreatedAt:yyyy-MM-dd HH:mm})");
                }
            }
            sb.AppendLine();
            sb.AppendLine("---------------------------------------------------");
            sb.AppendLine("Commands:");
            sb.AppendLine("- todo add <content>  : Add a task");
            sb.AppendLine("- todo done <index>   : Complete a task");
            sb.AppendLine("- todo delete <index> : Delete a task");
            sb.AppendLine("- todo clear          : Purge completed");

            CliOutputOverlay.Show("Tasks List", sb.ToString());
        }
    }
}
