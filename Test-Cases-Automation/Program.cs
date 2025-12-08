using Test_Cases_Automation.Services;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, config) =>
{
    config.WriteTo.Console()
          .WriteTo.File("Logs/log.txt", rollingInterval: RollingInterval.Day);
});


// Add services to the container.

builder.Services.AddScoped<ApiTestRunnerService>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins("http://localhost:5173", "https://asset-hierarchy-management.vercel.app")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

builder.Services.AddControllers();

var apiKey = builder.Configuration.GetValue<string>("Gemini:ApiKey");

// 2. Register CopilotAIService as a Singleton
// The DI container will create one instance and pass the apiKey to its constructor.
if (!string.IsNullOrWhiteSpace(apiKey))
{
    builder.Services.AddSingleton(new CopilotAIService(apiKey));
}
else
{
    // Handle the case where the API key is missing (e.g., log an error)
    Console.WriteLine("Warning: Gemini API Key is missing from configuration.");
}


// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}


app.UseHttpsRedirection();


app.UseAuthorization();
app.UseCors("AllowFrontend");

app.MapControllers();

app.Run();
