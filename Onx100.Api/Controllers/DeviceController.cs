using Microsoft.AspNetCore.Mvc;
using Onx100.Api.Models;
using Onx100.Api.Services;
using Onx100.Driver.Models;

namespace Onx100.Api.Controllers;

[ApiController]
[Route("api/device")]
public sealed class DeviceController : ControllerBase
{
    /******************** PRIVATE MEMBERS ********************/
    private readonly IOnx100DeviceService deviceService;

    
    /******************** CONSTRUCTOR ********************/
    public DeviceController(IOnx100DeviceService deviceService)
    {
        this.deviceService = deviceService;
    }

    
    /******************** PUBLIC API ENDPOINTS ********************/
    [HttpGet("state")]
    public ActionResult<DeviceStateResponse> GetState()
    {
        return Ok(CreateStateResponse());
    }

    [HttpPost("refresh")]
    public async Task<ActionResult<DeviceStateResponse>> RefreshStateAsync(CancellationToken cancellationToken)
    {
        await deviceService.RefreshStateAsync(cancellationToken);
        return Ok(CreateStateResponse());
    }

    [HttpPost("connect")]
    public async Task<ActionResult<DeviceStateResponse>> ConnectAsync(CancellationToken cancellationToken)
    {
        await deviceService.ConnectAsync(cancellationToken);
        return Ok(CreateStateResponse());
    }

    [HttpPost("disconnect")]
    public async Task<ActionResult<DeviceStateResponse>> DisconnectAsync(CancellationToken cancellationToken)
    {
        await deviceService.DisconnectAsync(cancellationToken);
        return Ok(CreateStateResponse());
    }

    [HttpPost("power/on")]
    public async Task<ActionResult<DeviceStateResponse>> PowerOnAsync(CancellationToken cancellationToken)
    {
        await deviceService.PowerOnAsync(cancellationToken);
        return Ok(CreateStateResponse());
    }

    [HttpPost("power/off")]
    public async Task<ActionResult<DeviceStateResponse>> PowerOffAsync(CancellationToken cancellationToken)
    {
        await deviceService.PowerOffAsync(cancellationToken);
        return Ok(CreateStateResponse());
    }

    [HttpPut("input/{input:int}")]
    public async Task<ActionResult<DeviceStateResponse>> SelectInputAsync(int input, CancellationToken cancellationToken)
    {
        if (input is < 1 or > 4)
        {
            return BadRequest(new ApiErrorResponse("invalid_argument", "Input must be between 1 and 4."));
        }

        await deviceService.SelectInputAsync(input, cancellationToken);
        return Ok(CreateStateResponse());
    }

    [HttpPut("volume/{volume:int}")]
    public async Task<ActionResult<DeviceStateResponse>> SetVolumeAsync(int volume, CancellationToken cancellationToken)
    {
        if (volume is < 0 or > 100)
        {
            return BadRequest(new ApiErrorResponse("invalid_argument", "Volume must be between 0 and 100."));
        }

        await deviceService.SetVolumeAsync(volume, cancellationToken);
        return Ok(CreateStateResponse());
    }

    [HttpPut("mute/{enabled:bool}")]
    public async Task<ActionResult<DeviceStateResponse>> SetMuteAsync(bool enabled, CancellationToken cancellationToken)
    {
        await deviceService.SetMuteAsync(enabled, cancellationToken);
        return Ok(CreateStateResponse());
    }

    
    /******************** PRIVATE METHODS ********************/
    private DeviceStateResponse CreateStateResponse()
    {
        Onx100DeviceState deviceState = deviceService.DeviceState;
        return DeviceStateResponse.From(deviceService.ConnectionState, deviceState);
    }
}