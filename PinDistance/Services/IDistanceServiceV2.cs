using PinDistance.Model;

namespace PinDistance.Services
{
    public interface IDistanceServiceV2
    {
        Task<PincodeDistanceV2DTO> GetDistanceBetweenPincodesAsync(string originPincode, string destinationPincode);
    }
}
