namespace MyMvcApp.Models
{
    public class ApiResponse
    {
        public string Source { get; set; }
        public string[] Answers { get; set; } = Array.Empty<string>();
        public string Message { get; set; }
    }
}
