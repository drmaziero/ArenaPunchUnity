using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Matchmaking
{
    [Serializable]
    public class MatchmakerPayload
    {
        [JsonProperty("matchId")]
        public string MatchId { get; set; }

        [JsonProperty("queueName")]
        public string QueueName { get; set; }

        [JsonProperty("region")]
        public string Region { get; set; }

        [JsonProperty("players")]
        public List<MatchmakerPlayer> Players { get; set; }
    }

    [Serializable]
    public class MatchmakerPlayer
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("attributes")]
        public Dictionary<string, object> Attributes { get; set; }
    }
}