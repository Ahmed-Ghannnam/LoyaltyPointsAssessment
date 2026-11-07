using LoyaltyPoints.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .ConfigureApiBehaviorOptions(options =>
    {
        options.InvalidModelStateResponseFactory = context =>
        {
            var errors = context.ModelState.Values
                .SelectMany(entry => entry.Errors)
                .Select(error => string.IsNullOrWhiteSpace(error.ErrorMessage)
                    ? "The request body is invalid."
                    : error.ErrorMessage)
                .ToArray();

            return new BadRequestObjectResult(new { errors });
        };
    });

builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Loyalty Points API",
        Version = "v1",
        Description = "API for awarding loyalty points and reading wallet balances."
    });
});
builder.Services.AddSingleton<InMemoryStorage>();
builder.Services.AddSingleton<AwardService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Loyalty Points API v1");
    });
}

app.MapControllers();

app.Run();

public partial class Program;
