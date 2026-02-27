using Blake3;

using Microsoft.AspNetCore.Mvc;

using System.Diagnostics;
using System.IO.Hashing;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateSlimBuilder(args);

var services = builder.Services;

services.AddOpenApi();
services.AddHealthChecks();

services.ConfigureHttpJsonOptions(options =>
{
	options.SerializerOptions.TypeInfoResolverChain.Insert(default, CustomJsonSerializerContext.Default);
	options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
});

var app = builder.Build();

app.MapOpenApi();
app.MapHealthChecks("/health");

#if DEBUG

app.UseSwaggerUI(options =>
{
	options.SwaggerEndpoint("/openapi/v1.json", "Api");
});

app.MapGet("/hash", (string? text, Algorithms algorithm = Algorithms.XXHASH128) => ProcessRequest(text, algorithm))
.Produces<ResponseDto>(StatusCodes.Status200OK)
.Produces(StatusCodes.Status500InternalServerError);

#endif

app.MapPost("/hash", ([FromBody] RequestDto request) => ProcessRequest(request.Text, request.Algorithm))
.Produces<ResponseDto>(StatusCodes.Status200OK)
.Produces(StatusCodes.Status500InternalServerError);

await app.RunAsync();

static string? ComputeHash(string? text, Algorithms algorithm)
{
	if (text is null)
		return default;

	string? result = default;

	var textBytes = Encoding.UTF8.GetBytes(text);

	switch (algorithm)
	{
		case Algorithms.XXHASH128:
			{
				var hashBytes = XxHash128.Hash(textBytes);

				result = Convert.ToHexString(hashBytes);
			}
			break;
		case Algorithms.BLAKE3:
			{
				var hashBytes = Hasher.Hash(textBytes);

				result = hashBytes.ToString().ToUpperInvariant();
			}
			break;
	}

	return result;
}

static ResponseDto Process(string? text, Algorithms algorithm)
{
	var start = Stopwatch.GetTimestamp();

	var hash = ComputeHash(text, algorithm);

	var textLength = text?.Length;

	var elapsed = Stopwatch.GetElapsedTime(start);
	var elapsedMs = (long)elapsed.TotalMilliseconds;
	var elapsedTicks = Stopwatch.GetTimestamp() - start;

	var result = new ResponseDto(hash, algorithm, textLength, elapsedMs, elapsedTicks);

	return result;
}

static IResult ProcessRequest(string? text, Algorithms algorithm)
{
	try
	{
		var response = Process(text, algorithm);

		return Results.Ok(response);
	}
	catch (Exception ex)
	{
		return Results.Problem(title: ex.Message, statusCode: StatusCodes.Status500InternalServerError);
	}
}

[JsonConverter(typeof(JsonStringEnumConverter<Algorithms>))]
public enum Algorithms
{
	XXHASH128,
	BLAKE3
}

public sealed record RequestDto(string? Text, Algorithms Algorithm);

public sealed record ResponseDto(
	string? Hash,
	Algorithms Algorithm,
	int? TextLength,
	long ElapsedMs,
	long ElapsedTicks
);

[JsonSerializable(typeof(Algorithms))]
[JsonSerializable(typeof(RequestDto))]
[JsonSerializable(typeof(ResponseDto))]
internal partial class CustomJsonSerializerContext : JsonSerializerContext
{

}
