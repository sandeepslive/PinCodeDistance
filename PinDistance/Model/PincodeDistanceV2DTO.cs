namespace PinDistance.Model
{
    public class PincodeDistanceV2DTO
    {

        public bool Success { get; set; }
        public double Distance { get; set; }
        public string ErrorMessage { get; set; }

        public PincodeDistanceV2DTO(bool success, double distance, string errorMessage)
        {
            Success = success;
            Distance = distance;
            ErrorMessage = errorMessage;
        }
    }
}
