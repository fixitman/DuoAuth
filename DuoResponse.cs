
 public class Response
    {
        public string? result { get; set; }
        public string? status { get; set; }
        public string? status_msg { get; set; }
    }

    public class DuoResponse
    {
        public Response? response { get; set; }
        public string? stat { get; set; }
    }