using UnityEngine;
using System.Collections.Generic;
using System.IO;
using CustomSpawnPoints.Config;
using Game.Progression;
using Helpers;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using UI.Console;

namespace CustomSpawnPoints;

[ConsoleCommand("/spawnpoints", "Debugging spawn points")]
public class SpawnPointsCommand : IConsoleCommand
{

    public string Execute(string[] components)
    {
        JsonSerializerSettings serializerSettings = new JsonSerializerSettings();
        DefaultContractResolver contractResolver = new DefaultContractResolver();
        CamelCaseNamingStrategy caseNamingStrategy = new CamelCaseNamingStrategy();
        caseNamingStrategy.ProcessDictionaryKeys = false;
        contractResolver.NamingStrategy = caseNamingStrategy;
        serializerSettings.ContractResolver = contractResolver;
        serializerSettings.Converters = new List<JsonConverter>(2)
        {
            new Vector3Converter(),
            new StringEnumConverter()
        };
        JsonSerializer Serializer = JsonSerializer.CreateDefault(serializerSettings);
        foreach (var des in Object.FindObjectsOfType<SetupDescriptor>(true))
        {
            var path = Path.Combine(CustomSpawnPointsMod.Shared.ModDirectory, $"spawnpoint-{des.identifier}.json");
            using (StreamWriter streamWriter = new StreamWriter(path))
                using (var jsonWriter = new JsonTextWriter(streamWriter) { Formatting = Formatting.Indented })
                    Serializer.Serialize(jsonWriter, new SerializedSetupDescriptor(des));
        }
        return "Spawn points dumped";
    }
    
}