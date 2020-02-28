using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace ReplyVision
{
    public class PersonDb
    {
        [JsonProperty]
        private Dictionary<string, Guid> Persons { get; set; } = new Dictionary<string, Guid>();

        public void Add(string nameSurname, Guid guid)
        {
            Persons.Add(nameSurname, guid);
        }

        public bool TryGetValue(string nameSurname, out Guid guid)
        {
            return Persons.TryGetValue(nameSurname, out guid);
        }

        public void Save(string path)
        {
            File.WriteAllText(path, JsonConvert.SerializeObject(this));
        }

        public static PersonDb Load(string path)
        {
            if (!File.Exists(path))
                return new PersonDb();

            string content = File.ReadAllText(path);
            return JsonConvert.DeserializeObject<PersonDb>(content);
        }
    }
}
