// Developer: heaplyn
// Date: 2026-08-19
// Summary: Command handler for the Dataset Harvester.
//          Allows manual triggering of autonomous dataset discovery and download.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace JarvisLauncher.Modules.Layer3.Handlers
{
    public class DatasetCommandHandler : ICommandHandler
    {
        public bool CanHandle(string query)
        {
            string q = query.ToLower();
            return q.Contains("dataset") || q.Contains("harvest") || q.Contains("scrape github datasets");
        }

        public List<CommandResult> GetSuggestions(string query)
        {
            return new List<CommandResult>
            {
                new CommandResult
                {
                    TITLE = "🚀 Harvest Datasets",
                    DESCRIPTION = "Scrape GitHub for LLM datasets and use AI to auto-download high-quality data.",
                    SIMILARITY = 1.0,
                    EXECUTE = () => Task.Run(async () => await DatasetHarvester.RunAutomaticHarvestAsync())
                },
                new CommandResult
                {
                    TITLE = "🔍 Search HF Datasets",
                    DESCRIPTION = "Use AI to discover new datasets on Hugging Face based on current trends.",
                    SIMILARITY = 0.8,
                    EXECUTE = () => {
                        string q = query.Replace("dataset", "").Replace("search", "").Trim();
                        if (string.IsNullOrEmpty(q)) q = "fine-tuning";
                        Task.Run(async () => await DatasetHarvester.RunAutomaticHarvestAsync()); // Trigger full harvest as it includes AI search
                    }
                }
            };
        }

        public List<CommandDesc> GetCommandDescriptions()
        {
            return new List<CommandDesc>
            {
                new CommandDesc
                {
                    COMMAND_NAME = "Harvest Datasets",
                    COMMAND_DESCRIPTION = "Autonomous scraping of GitHub and Hugging Face for LLM datasets.",
                    COMMAND_EXAMPLE = "harvest datasets"
                }
            };
        }
    }
}
