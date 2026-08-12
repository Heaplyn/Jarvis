// Developer: heaplyn
// Date: 2026-08-09
// Summary: Detects active network interfaces and displays local IPv4 addresses. Copy-pastes to clipboard on click.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net.Sockets;


namespace JarvisLauncher
{   
   
    public class PhoneControlCommandHandler : ICommandHandler
    {
         private static List<string> Aliases = new List<string>{
        "phone",
        "control",
        

    };
        public bool CanHandle(string query)
        {
            query = query.Trim().ToLower();
            foreach (var Alias in Aliases)
            {
                if (SearchUtil.IsClose(query,Alias))
                {
                    return true;
                }
            }
            return false;
        }

        public List<CommandResult> GetSuggestions(string query)
        {
            var suggestions = new List<CommandResult>();
            query = query.Trim().ToLower();
            double similarity = SearchUtil.GetSimilarity(query,Aliases[0]);
            foreach (var Alias in Aliases)
            {
                similarity = Math.Max(similarity,SearchUtil.GetSimilarity(query,Alias));

            }
            
           suggestions.Add(new CommandResult
                {
                    Title = "LLM Gui",
                    Description = "Opens LLM Gui",
                    Execute = () =>
                    {
                        //var PowerShell = JarvisLauncher.CommandParser;
                    },
                    Similarity = similarity
                });

            return suggestions;
        }


    }
}
