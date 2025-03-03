using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using PinDistance.Model;

namespace PinDistance.Services
{
    public class DistanceServiceV2 : IDistanceServiceV2
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<DistanceServiceV2> _logger;
        private const string ApiKey = "579b464db66ec23bdd000001cdd3946e44ce4aad7209ff7b23ac571b";
        private const string BaseUrl = "https://api.data.gov.in/resource/5c2f62fe-5afa-4119-a499-fec9d604d5bd";

        public DistanceServiceV2(HttpClient httpClient, ILogger<DistanceServiceV2> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
        }

        public async Task<(bool IsValid, double Latitude, double Longitude)> GetGeocodeAsync(string pincode)
        {
            _logger.LogInformation("Starting geocode retrieval for pincode: {Pincode}", pincode);

            var url = $"{BaseUrl}?api-key={ApiKey}&format=json&filters[pincode]={pincode}";
            try
            {
                _logger.LogInformation("Calling external API: {Url}", url);
                var response = await _httpClient.GetStringAsync(url);

                var json = JObject.Parse(response);
                var records = json["records"];
                if (records != null && records.HasValues)
                {
                    // Get first record where "officetype" is "PO"
                    // Find the first matching record with "PO", then "HO", otherwise take any available record
                    var firstRecord = records.FirstOrDefault(r =>
                        (r["officetype"]?.Value<string>()?.Equals("PO", StringComparison.OrdinalIgnoreCase) ?? false))
                        ?? records.FirstOrDefault(r =>
                        (r["officetype"]?.Value<string>()?.Equals("HO", StringComparison.OrdinalIgnoreCase) ?? false))
                        ?? records.FirstOrDefault();

                    double latitude = firstRecord.Value<double>("latitude");
                    double longitude = firstRecord.Value<double>("longitude");

                    _logger.LogInformation("Geocode found: Latitude = {Latitude}, Longitude = {Longitude}", latitude, longitude);
                    return (true, latitude, longitude);
                }

                _logger.LogWarning("No geocode data found for pincode: {Pincode}", pincode);
                return (false, 0, 0);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while fetching geocode for pincode: {Pincode}", pincode);
                return (false, 0, 0);
            }
        }

        private double CalculateHaversineDistance(double lat1, double lon1, double lat2, double lon2)
        {
            const double Radius = 6371;
            var dLat = ToRadians(lat2 - lat1);
            var dLon = ToRadians(lon2 - lon1);

            lat1 = ToRadians(lat1);
            lat2 = ToRadians(lat2);

            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                    Math.Cos(lat1) * Math.Cos(lat2) *
                    Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return Radius * c;
        }

        private double ToRadians(double angle) => Math.PI * angle / 180.0;

        public async Task<PincodeDistanceV2DTO> GetDistanceBetweenPincodesAsync(string originPincode, string destinationPincode)
        {
            _logger.LogInformation("Calculating distance between {Pincode1} and {Pincode2}", originPincode, destinationPincode);

            var (isValid1, lat1, lon1) = await GetGeocodeAsync(originPincode);
            if (!isValid1)
            {
                _logger.LogWarning("Invalid pincode: {Pincode1}", originPincode);
                return new PincodeDistanceV2DTO(false, 0, $"Invalid Pincode: {originPincode}");
            }

            var (isValid2, lat2, lon2) = await GetGeocodeAsync(destinationPincode);
            if (!isValid2)
            {
                _logger.LogWarning("Invalid pincode: {Pincode2}", destinationPincode);
                return new PincodeDistanceV2DTO(false, 0, $"Invalid Pincode: {destinationPincode}");
            }

            var distance = CalculateHaversineDistance(lat1, lon1, lat2, lon2);
            var roundedDistance = Math.Round(distance); // Removes decimal place
            _logger.LogInformation("Calculated distance: {Distance} km", distance);

            return new PincodeDistanceV2DTO(true, roundedDistance, string.Empty);
        }
    }
}
