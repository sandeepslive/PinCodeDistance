using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PinDistance.Model;
using PinDistance.Services;

namespace PinDistance.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class PinCodeController : ControllerBase
    {
        private readonly IDistanceService _distanceService;
        private readonly IDistanceServiceV2 _distanceServiceV2;
        private readonly ILogger<PinCodeController> _logger;

        public PinCodeController(IDistanceService distanceService, IDistanceServiceV2 distanceServiceV2, ILogger<PinCodeController> logger)
        {
            _distanceService = distanceService;
            _distanceServiceV2 = distanceServiceV2;
            _logger = logger;
        }
        [Authorize]
        [HttpPost("distance")]
        public async Task<IActionResult> GetDistance([FromBody] DistanceRequestModel request)
        {
            if (string.IsNullOrEmpty(request.OriginPincode) || string.IsNullOrEmpty(request.DestinationPincode))
            {
                return BadRequest(new BadResponseModel
                {
                    message = "Origin and destination pincodes are required.",
                    status = new StatusDTO() { Code = 400, text = "BadRequest" }
                });
            }

            try
            {
                PincodeDistanceDTO distance = await _distanceService.GetDistanceAsync(request.OriginPincode, request.DestinationPincode);

                if (distance == null) // Indicate an error from the service
                {
                    return BadRequest(new BadResponseModel
                    {
                        message = "Could not calculate distance. Check pincodes or API availability.",
                        status = new StatusDTO() { Code = 400, text = "BadRequest" }
                    });
                }
                return Ok(new DistanceResponseModel
                {
                    distance = distance.distance,
                    duration = distance.duration,
                    distanceUnit = "km",
                    status = new StatusDTO() { Code = 200, text = "success" }
                });
            }
            catch (Exception ex)
            {
                // Log the exception using a proper logging framework
                Console.WriteLine($"Distance Calculation Error: {ex.Message}");
                return StatusCode(500, new BadResponseModel
                {
                    message = "An severe error occurred during distance calculation.",
                    status = new StatusDTO() { Code = 500, text = "Internal Server Error" }
                });
            }
        }

        [Authorize]
        [HttpPost("distancev2")]
        public async Task<IActionResult> GetDistanceV2([FromBody] DistanceRequestModel request)
        {
            if (string.IsNullOrWhiteSpace(request.OriginPincode) || string.IsNullOrWhiteSpace(request.DestinationPincode))
            {
                return BadRequest(new BadResponseModel
                {
                    message = "Origin and destination pincodes are required.",
                    status = new StatusDTO { Code = 400, text = "BadRequest" }
                });
            }

            try
            {
                var response = await _distanceServiceV2.GetDistanceBetweenPincodesAsync(request.OriginPincode, request.DestinationPincode);

                if (!response.Success)
                {
                    return BadRequest(new BadResponseModel
                    {
                        message = response.ErrorMessage,
                        status = new StatusDTO { Code = 400, text = "BadRequest" }
                    });
                }

                return Ok(new DistanceResponseModel
                {
                    distance = response.Distance,
                    distanceUnit = "km",
                    status = new StatusDTO { Code = 200, text = "success" }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Distance calculation failed for {OriginPincode} to {DestinationPincode}",
                    request.OriginPincode, request.DestinationPincode);

                return StatusCode(500, new BadResponseModel
                {
                    message = "A severe error occurred during distance calculation.",
                    status = new StatusDTO { Code = 500, text = "Internal Server Error" }
                });
            }
        }
        }
}
