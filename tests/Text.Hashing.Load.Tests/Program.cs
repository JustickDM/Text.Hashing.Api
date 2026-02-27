using Microsoft.Extensions.Configuration;

using NBomber.CSharp;
using NBomber.Http.CSharp;

using System.Net.Mime;
using System.Text;
using System.Text.Json;

using static System.TimeSpan;

var configuration = new ConfigurationBuilder()
.SetBasePath(Directory.GetCurrentDirectory())
.AddJsonFile("appsettings.json", true)
.Build();

var config = configuration.GetSection(nameof(Config)).Get<Config>() ?? new Config();

Console.WriteLine(config.ToString());

using var httpClient = new HttpClient();
httpClient.BaseAddress = new Uri(config.BaseUrl);

var texts = new[]
{
	new string('C', 10),
	new string('#', 100),
	new string('A', 1000),
	new string('O', 10000),
	new string('T', 100000),
};

var scenario = Scenario.Create("post_hash_scenario", async context =>
{
	var text = texts[Random.Shared.Next(texts.Length)];
	var request = new RequestDto(text, config.Algorithm);
	var requestJson = JsonSerializer.Serialize(request);
	var requestBody = new StringContent(requestJson, Encoding.UTF8, MediaTypeNames.Application.Json);
	var httpRequest = Http.CreateRequest("POST", $"{config.BaseUrl}/hash").WithBody(requestBody);
	var httpResponse = await Http.Send(httpClient, httpRequest);

	return httpResponse;
})
.WithWarmUpDuration(FromMilliseconds(config.WarmUpDurationMs))
.WithLoadSimulations(
	Simulation.Inject(config.Rate, FromMilliseconds(config.IntervalMs), FromMilliseconds(config.DuringMs))
);

NBomberRunner
	.RegisterScenarios(scenario)
	.Run();

enum Algorithms
{
	XXHASH128,
	BLAKE3
}

sealed record RequestDto(string? Text, Algorithms Algorithm);

sealed record Config(
	string BaseUrl = "http://localhost:5000",
	Algorithms Algorithm = Algorithms.XXHASH128,
	int Rate = 10000,
	long WarmUpDurationMs = 5000,
	long IntervalMs = 1000,
	long DuringMs = 30000
);
