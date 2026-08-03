using Onx100.Driver.Configuration;
using Onx100.Api.Services;
using Onx100.Api.Middleware;
using Onx100.Api.Hubs;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddSignalR();
builder.Services.AddOpenApi();

builder.Services.Configure<Onx100Options>(builder.Configuration.GetSection("Onx100")); 
builder.Services.AddSingleton<IOnx100DeviceService, Onx100DeviceService>();

WebApplication app = builder.Build();
app.UseMiddleware<ApiExceptionMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.MapControllers();
app.MapHub<DeviceHub>("/hubs/device");
app.Run();