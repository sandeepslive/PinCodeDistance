namespace PinDistance.Model
{
    public class StatusDTO
    {
        public StatusDTO()
        {
            timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
        }
        public int Code { get; set; }
        public string text { get; set; }
        public string timestamp { get; set; }

    }
    public class ResposneBaseModel
    {
        public StatusDTO status { get; set; }
    }

}