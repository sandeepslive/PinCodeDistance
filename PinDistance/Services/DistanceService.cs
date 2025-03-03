using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using PinDistance.Model;

namespace PinDistance.Services
{

    public class DistanceService : IDistanceService
    {
        private readonly HttpClient _httpClient;
        private readonly string _googleMapsApiKey; // Store API key securely
        private readonly ILogger<DistanceService> _logger;

        public DistanceService(HttpClient httpClient, IConfiguration configuration, ILogger<DistanceService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
            _googleMapsApiKey = configuration["GoogleMaps:ApiKey"]; // Get from configuration
            if (string.IsNullOrEmpty(_googleMapsApiKey))
            {
                _logger.LogError("Google Maps API key is missing. Configure it in appsettings.json.");
                throw new ArgumentNullException("Google Maps API key is missing. Configure it in appsettings.json.");
            }

        }

        public async Task<PincodeDistanceDTO> GetDistanceAsync(string originPincode, string destinationPincode)
        {
            _logger.LogInformation("Fetching distance between {Origin} and {Destination}", originPincode, destinationPincode);

            var originLatLng = await GetLatLngFromPincode(originPincode);
            var destinationLatLng = await GetLatLngFromPincode(destinationPincode);

            if (originLatLng == null || destinationLatLng == null)
            {
                _logger.LogWarning("Geocoding failed for one or both pincodes: {Origin}, {Destination}", originPincode, destinationPincode);
                return null;
            }

            string apiUrl = $"https://maps.googleapis.com/maps/api/distancematrix/json?origins={originLatLng}&destinations={destinationLatLng}&key={_googleMapsApiKey}";

            _logger.LogInformation("Sending request to Google Distance Matrix API: {Url}", apiUrl);

            try
            {
                HttpResponseMessage response = await _httpClient.GetAsync(apiUrl);
                string jsonResponse = await response.Content.ReadAsStringAsync();

                _logger.LogInformation("Received response from Google Distance Matrix API: {Response}", jsonResponse);

                response.EnsureSuccessStatusCode();

                JObject result = JObject.Parse(jsonResponse);
                string status = result["status"].ToString();

                if (status != "OK")
                {
                    _logger.LogError("Google Maps API returned an error: {Status}, Response: {Response}", status, jsonResponse);
                    return null;
                }

                var distanceElement = result["rows"][0]["elements"][0]["distance"]["value"];
                var durationElement = result["rows"][0]["elements"][0]["duration"]["text"];

                if (distanceElement != null)
                {
                    double distanceMeters = distanceElement.ToObject<double>();
                    double distanceKilometers = distanceMeters / 1000;

                    _logger.LogInformation("Distance calculated: {DistanceKm} km, Duration: {Duration}", distanceKilometers, durationElement.ToString());

                    return new PincodeDistanceDTO()
                    {
                        distance = (long)Math.Round(distanceKilometers),
                        duration = durationElement.ToString()
                    };
                }
                else
                {
                    _logger.LogWarning("Could not parse distance from Google Maps API response: {Response}", jsonResponse);
                    return null;
                }
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP request failed while fetching distance from {Url}", apiUrl);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred while fetching distance from {Url}", apiUrl);
                return null;
            }
        }
        private async Task<string> GetLatLngFromPincode(string pincode)
        {
            string apiKey = _googleMapsApiKey; // From configuration (as before)
            string geocodingApiUrl = $"https://maps.googleapis.com/maps/api/geocode/json?components=postal_code:{pincode}|country:IN&key={apiKey}";

            try
            {
                HttpResponseMessage response = await _httpClient.GetAsync(geocodingApiUrl);
                response.EnsureSuccessStatusCode();

                string json = await response.Content.ReadAsStringAsync();
                JObject result = JObject.Parse(json);

                string status = result["status"].ToString();
                if (status == "OK")
                {
                    // Extract latitude and longitude
                    var location = result["results"][0]["geometry"]["location"];
                    double lat = location["lat"].ToObject<double>();
                    double lng = location["lng"].ToObject<double>();
                    return $"{lat},{lng}";
                }
                else
                {
                    Console.WriteLine($"Geocoding API Error: {status} for pincode: {pincode}");
                    return null; // Geocoding failed
                }
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"HTTP Request Error during geocoding: {ex.Message}");
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during geocoding: {ex.Message}");
                return null;
            }
        }
    }
}
