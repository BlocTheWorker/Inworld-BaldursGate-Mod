using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Resources;

namespace BaldursGateInworld.Manager
{
    internal struct Location
    {
        internal float x, y, z;
    }

    internal struct Character
    {
        internal string id, name;
        internal string head, breast, melee, ranged;
    }

    public class POICoordinate
    {
        [JsonProperty("x")]
        public int X { get; set; }
        [JsonProperty("y")]
        public int Y { get; set; }
        [JsonProperty("height")]
        public int Height { get; set; }

        [JsonProperty("scene")]
        public string Scene { get; set; }
    }


    public class POILocation
    {
        [JsonProperty("id")]
        public string Id { get; set; }
        [JsonProperty("coordinate")]
        public POICoordinate Coordinate { get; set; }
        [JsonProperty("distance")]
        public int Distance { get; set; }
        [JsonProperty("place")]
        public string Place { get; set; }
    }

    public class LocationDatabase
    {
        public List<POILocation> Locations { get; set; }
    }

    public class WorldManager
    {
        // The single instance of the class
        private static WorldManager _instance;
        private static readonly object _lock = new object();
        private string appData, currentScene;
        private List<POILocation> _locations;
        private HashSet<string> _returnedLocations;
        // The public variable to store the dynamic object
        public dynamic? WorldData { get; private set; }

        private Location location;
        private List<Character> party;

        // Private constructor
        private WorldManager()
        {
            appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            appData = appData.Replace("Roaming", "");
            SetupLocationTracker();
        }

        // Get the single instance of the class
        public static WorldManager Instance
        {
            get
            {
                lock (_lock)
                {
                    if (_instance == null)
                    {
                        _instance = new WorldManager();
                    }
                    return _instance;
                }
            }
        }


        private void populateData()
        {
            location = new Location
            {
                x = WorldData[0][0],
                y = WorldData[0][1],
                z = WorldData[0][2]
            };
            party = new List<Character>();

            var partyArr = WorldData[1];
            foreach (var singleCompanion in partyArr)
            {
                Character character = new Character();
                character.id = singleCompanion[0];
                character.name = singleCompanion[1];
                character.head = singleCompanion[2][0];
                character.breast = singleCompanion[2][1];
                character.melee = singleCompanion[2][2];
                character.ranged = singleCompanion[2][3];
                party.Add(character);
            }

            currentScene = WorldData[2];
        }

        // The ReadWorld method
        public void ReadWorld()
        {
            try
            {
                string path = Path.Combine(appData, @"Local\Larian Studios\Baldur's Gate 3\Script Extender\BG3InworldData\data.json");
                string json = File.ReadAllText(path);
                WorldData = JsonConvert.DeserializeObject<dynamic>(json);
                populateData();
            }
            catch
            {

            }
        }

        public void SetupLocationTracker()
        {
            try
            {
                _locations = new List<POILocation>();
                _returnedLocations = new HashSet<string>();

                Uri uri = new Uri("/Resource/LocationDatabase.json", UriKind.Relative);
                StreamResourceInfo info = Application.GetResourceStream(uri);
                using (StreamReader reader = new StreamReader(info.Stream))
                {
                    string result = reader.ReadToEnd();
                    var rootObject = JsonConvert.DeserializeObject<LocationDatabase>(result);
                    _locations = rootObject.Locations;
                }
            }
            catch
            {

            }
        }

        public string IsCloseToAnything()
        {
            float x = this.location.x;
            float y = this.location.z;
            foreach (var location in _locations)
            {
                double distance = Math.Sqrt(Math.Pow(x - location.Coordinate.X, 2) + Math.Pow(y - location.Coordinate.Y, 2));
                if (distance <= location.Distance && !_returnedLocations.Contains(location.Id) && currentScene.ToLower() == location.Coordinate.Scene.ToLower())
                {
                    _returnedLocations.Add(location.Id);
                    return location.Place;
                }
            }
            return string.Empty;
        }
    }
}
