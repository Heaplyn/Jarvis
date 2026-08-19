// Developer: heaplyn
// Date: 2026-08-18
// Summary: Command Handler for Godellian Intelligence monitoring and interaction.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace JarvisLauncher.Modules.Layer3.Handlers
{
    public class GodellianCommandHandler : ICommandHandler
    {
        public bool CanHandle(string query)
        {
            string q = query.ToLower();
            return q.Contains("godellian") || q.Contains("brain") || q.Contains("neural") || q == "gi";
        }

        public List<CommandResult> GetSuggestions(string query)
        {
            return new List<CommandResult> {
                new CommandResult {
                    TITLE = "🧠 Open Godellian Intelligence Core",
                    DESCRIPTION = "Monitor local brain accuracy, vocabulary, and evolutionary progress.",
                    SIMILARITY = 1.0,
                    EXECUTE = () => GodellianIntelligenceOverlay.ShowOverlay()
                },
                new CommandResult {
                    TITLE = "🧬 Trigger Neural Mutation",
                    DESCRIPTION = "Force a synaptic drift mutation in a random neural cluster.",
                    SIMILARITY = 0.8,
                    EXECUTE = () => { CoreRegistry.Intelligence.MainBrain.MutateTopology(); DebugConsoleOverlay.Log("Neural", "Manual mutation triggered, Sir."); }
                }
            };
        }

        public List<CommandDesc> GetCommandDescriptions()
        {
            return new List<CommandDesc> {
                new CommandDesc("Godellian Core", "Opens the meta-recursive intelligence dashboard.", "godellian"),
                new CommandDesc("Neural Mutate", "Triggers autonomous synaptic drift.", "brain mutate")
            };
        }
    }
}
