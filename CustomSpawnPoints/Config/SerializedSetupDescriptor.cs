using System.Collections.Generic;
using System.Linq;
using Character;
using Game.Progression;
using HarmonyLib;
using Model;
using StrangeCustoms.Tracks;
using Track;
using UnityEngine;

namespace CustomSpawnPoints.Config;

public class SerializedSetupDescriptor
{
    public string identifier; // used as setupid
    public string progressionId;
    public string name;
    public string[] enabledFeatures;
    public int initialMoney;
    public SerializableSpawnPoint spawnPoint;
    public List<SerializableCarPlacement> carPlacements = new();
    public bool showTutorial;
    
    public SerializedSetupDescriptor() {}

    public SerializedSetupDescriptor(SetupDescriptor setupDescriptor)
    {
        identifier = setupDescriptor.identifier;
        progressionId = setupDescriptor.identifier;
        name = setupDescriptor.name;
        enabledFeatures = [];
        initialMoney = setupDescriptor.initialMoney;
        spawnPoint = new SerializableSpawnPoint(setupDescriptor.spawnPoint);
        carPlacements = setupDescriptor.placements.Select((t, i) => new SerializableCarPlacement(t)).ToList();
        showTutorial = setupDescriptor.showTutorial;
    }

    public SetupDescriptor Build()
    {
        var go = new GameObject($"setup-{identifier}");
        var setupDescriptor = go.AddComponent<SetupDescriptor>();
        setupDescriptor.identifier = identifier;
        setupDescriptor.initialMoney = initialMoney;
        setupDescriptor.spawnPoint = spawnPoint.Build(go);
        setupDescriptor.placements = carPlacements.Select((t, i) => t.Build(go, $"{identifier}-{i}")).ToArray();
        setupDescriptor.showTutorial = showTutorial;
        return setupDescriptor;
    }
    
    public class SerializableSpawnPoint
    {
        public float[] location;
        public float[] rotation;
        public float range = 3f;
        public int priority = 1;
        
        public SerializableSpawnPoint() {}

        public SerializableSpawnPoint(SpawnPoint spawnPoint)
        {
            location = [spawnPoint.transform.position.x, spawnPoint.transform.position.y, spawnPoint.transform.position.z];
            rotation = [spawnPoint.transform.rotation.eulerAngles.x, spawnPoint.transform.rotation.eulerAngles.y, spawnPoint.transform.rotation.eulerAngles.z];
            range = spawnPoint.radius;
            priority = spawnPoint.priority;
        }
        
        public SpawnPoint Build(GameObject parent)
        {
            var spawnPoint = parent.AddComponent<SpawnPoint>();
            spawnPoint.transform.position = new Vector3(location[0], location[1], location[2]);
            spawnPoint.transform.rotation = Quaternion.Euler(rotation[0], rotation[1], rotation[2]);
            spawnPoint.priority = priority;
            spawnPoint.radius = range;
            return spawnPoint;
        }
    }

    public class SerializableCarPlacement
    {
        public string[] carIdentifier;
        public SerializedLocation location;
        public bool wreck;
        public float oiled = 1f;
        public float loadPercent = 0.0f;
        public string? loadId;

        public SerializableCarPlacement() {}

        private static readonly AccessTools.FieldRef<TrackMarker, SerializableLocation> _location = AccessTools.FieldRefAccess<TrackMarker, SerializableLocation>("_location");
        
        public SerializableCarPlacement(SetupDescriptor.CarPlacement carPlacement)
        {
            carIdentifier = carPlacement.carIdentifier;
            if (carPlacement.marker != null)
            {
                location = new SerializedLocation(_location(carPlacement.marker));    
            }
            wreck = carPlacement.wreck;
            oiled = carPlacement.oiled;
            loadPercent = carPlacement.loadPercent;
            loadId = carPlacement.load?.id;
        }

        public SetupDescriptor.CarPlacement Build(GameObject gameObject, string id)
        {
            SetupDescriptor.CarPlacement value = new();
            value.carIdentifier = carIdentifier;
            value.marker = gameObject.AddComponent<TrackMarker>();
            value.marker.Location = Graph.Shared.MakeLocation(location);
            value.marker.type = TrackMarkerType.Generic;
            value.marker.id = $"car-placement-{id}";
            value.wreck = wreck;
            value.oiled = oiled;
            value.loadPercent = loadPercent;
            value.load = CarPrototypeLibrary.instance.LoadForId(loadId);
            return value;
        }
    }
}