// Developer: heaplyn
// Date: 2026-08-13
// Summary: Command handler for animation preferences, motion speeds, and visual options overlay.

using System.Collections.Generic;

namespace JarvisLauncher.Modules.Layer3.Handlers
{
    public class AnimationCommandHandler : ICommandHandler
    {
        public bool CanHandle(string query)
        {
            query = query.ToLower().Trim();
            return query == "anim" || query == "animation" || query == "animations" ||
                   query == "fx" || query == "visuals" || query == "motion" ||
                   query.StartsWith("anim ") || query.StartsWith("animation ");
        }

        public List<CommandResult> GetSuggestions(string query)
        {
            var results = new List<CommandResult>();

            results.Add(new CommandResult
            {
                Title = "✨ Configure Animations & Visual Effects Options",
                Description = "Adjust transition speeds, motion effects, window fill opacity, & text transparency",
                Similarity = 5.5,
                Execute = () => AnimationOptionsOverlay.ShowOverlay()
            });

            return results;
        }

        public List<CommandDesc> GetCommandDescriptions()
        {
            return new List<CommandDesc>
            {
                new CommandDesc("anim", "Configure HUD animations & motion presets", "anim"),
                new CommandDesc("visuals", "Adjust visual effects, glow, & opacity options", "visuals")
            };
        }

        public void OnStart() { }
    }
}
