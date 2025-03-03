using PinDistance.Model;

namespace PinDistance.Services
{
    public interface IDistanceService
    {
       public Task<PincodeDistanceDTO> GetDistanceAsync(string originPincode, string destinationPincode);
    }

}
