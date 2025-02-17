using AtlusScriptLibrary.FlowScriptLanguage.Compiler;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace AtlusScriptLibrary.Common.Libraries;

public class MessageScriptLibrary : ICloneable
{
    public int Index { get; set; }

    public string Name { get; set; }

    public string Description { get; set; }

    [JsonObjectCreationHandling(JsonObjectCreationHandling.Populate)]
    public List<MessageScriptLibraryFunction> Functions { get; set; } = new();

    public object Clone()
    {
        var clone = new MessageScriptLibrary();
        clone.Index = Index;
        clone.Name = Name;
        clone.Description = Description;
        clone.Functions = Functions.Clone()?.ToList();
        return clone;
    }
}