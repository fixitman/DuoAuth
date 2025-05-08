// Root myDeserializedClass = JsonConvert.DeserializeObject<Root>(myJsonResponse);
    public class FailResponse
    {
        public int code { get; set; }
        public string? message { get; set; }
        public string? message_detail { get; set; }
        public string? stat { get; set; }
    }

