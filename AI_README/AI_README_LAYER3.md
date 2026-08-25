# LAYER 3: ROUTING & COMMAND HANDLERS

## ROUTING ARCHITECTURE (`CommandParser.cs`)
Coordinates text queries, weights match recommendations, maps modules to navigation categories, and handles suggestion execution.

- **Routing Registry**: Handlers are registered under `CommandParser` static constructor using:
  `RegisterHandler(CommandType.TYPE, "Display Name", () => new CommandHandler());`
- **Categorization**: Map the `CommandType` in `GetCategoryForType(CommandType type)` switch block. Available categories:
  - `"AI & Automation"`
  - `"System & Power"`
  - `"Files & Editing"`
  - `"Apps & Launcher"`
  - `"Audio & Media"`
  - `"Productivity"`
  - `"Utilities"`

---

## INTERFACE CONTRACT (`Modules/Layer1/ICommandHandler.cs`)
Any command handler must implement the three core interface methods:

```csharp
public interface ICommandHandler
{
    // Evaluates if the query is structurally close to command triggers
    bool CanHandle(string query);

    // Builds the collection of suggestions for autocomplete lists
    List<CommandResult> GetSuggestions(string query);

    // Declares metadata, alias commands, and usage definitions
    List<CommandDesc> GetCommandDescriptions();
}
```

---

## RECOMMENDATION & SIMILARITY RULES
Suggestions are mapped using `CommandResult` payloads:

- **Similarity Scoring**:
  - Direct exact matching should use `10.0`.
  - Fuzzy queries use `SearchUtil.GetSimilarity(query, "command_alias")`.
  - **Triggers Override**: Desktop apps match at a base score of `4.5`. If your command matches, enforce a floor similarity of `5.0` (or `4.0` for secondary functions) to ensure your launcher overlay overrides external desktop apps.
- **Result Schema**:
  ```csharp
  new CommandResult
  {
      TITLE = "🛠️ Visual Action Name",
      DESCRIPTION = "Summary of tool capabilities and side-effects",
      SIMILARITY = computedSimilarity,
      EXECUTE = () => MyOverlay.ShowOverlay() // Action callback
  }
  ```

---

## STEP-BY-STEP HANDLER CREATION GUIDE
1. **Define Enum**: Add `MY_NEW_FEATURE` key inside `CommandType` enum in `CommandParser.cs`.
2. **Implement Handler**: Create `MyNewFeatureCommandHandler.cs` in `Modules/Layer3/Handlers/`. Apply `ICommandHandler`.
3. **Register Handler**: Register the class in `CommandParser` constructor:
   `RegisterHandler(CommandType.MY_NEW_FEATURE, "My Feature Deck", () => new MyNewFeatureCommandHandler());`
4. **Map Category**: Add case mapping `CommandType.MY_NEW_FEATURE` to appropriate category string in `GetCategoryForType()`.
