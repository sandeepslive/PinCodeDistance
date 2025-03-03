namespace PinDistance.Model
{
    public class DistanceResponseModel : ResposneBaseModel
    {
        public string duration { get; set; }    

        public double distance { get; set; } // In kilometers

        public string distanceUnit { get; set; }
    }
}
