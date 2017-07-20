using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Cognitive.Capabilities
{
    /// <summary>
    /// Named Entity Extractor interface using an Azure ML endpoint
    /// </summary>
    public class EntityExtractor
    {
        private HttpClient client;

        public EntityExtractor(string uri, string apiKey)
        {
            client = new HttpClient();
            client.BaseAddress = new Uri(uri);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        }
        public async Task<IEnumerable<NamedEntity>> Extract(string text)
        {
            var input = new {
                  Inputs = new {
                    input1 = new {
                      Values = new [] {
                        new [] { text }
                      }
                    }
                  }
                };
            var response = await client.PostAsJsonAsync("", input);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();

            dynamic result = JObject.Parse(json);
            var values = result.Results.output1.value.Values as JArray;

            return values.Select(value => {
                var row = value as JArray;
                return new NamedEntity() {
                    Name = row[1].Value<string>(),
                    EntityType = GetEntityType(row[4].Value<string>()),
                };
            }).ToArray();
        }


        private EntityType GetEntityType(string labelName)
        {
            switch (labelName)
            {
                case "PER": return EntityType.Person;
                case "ORG": return EntityType.Orginization;
                case "LOC": return EntityType.Location;
                default: return EntityType.Other;
            }
        }
    }

    public class NamedEntity
    {
        public string Name { get; set; }
        public EntityType EntityType { get; set; }
    }

    public enum EntityType
    {
        Other,
        Person,
        Orginization,
        Location
    }

}
