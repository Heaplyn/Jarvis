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
                        Task.Run(async () => {
                            await DatasetHarvester.RunAutomaticHarvestAsync();
                            await GodellianHuggingFaceEngine.RunAutoGrabCycleAsync();
                        });
                    }
                },
                new CommandResult
                {
                    TITLE = "🧠 Godellian HF Ingest",
                    DESCRIPTION = "Force autonomic knowledge ingestion from Hugging Face for the Godellian brain.",
                    SIMILARITY = 0.9,
                    EXECUTE = () => Task.Run(async () => await GodellianHuggingFaceEngine.RunAutoGrabCycleAsync())
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
