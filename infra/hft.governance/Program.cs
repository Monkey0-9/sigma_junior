using Hft.Infra;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddOpenApi();

// Register AppendOnlyLog singleton
builder.Services.AddSingleton<AppendOnlyLog>(sp => 
{
    // In a real scenario, these should be from configuration
    var path = @"c:\hft_platform\Hft.Runner\data\audit\governance.log"; 
    var key = Encoding.UTF8.GetBytes("super-secret-governance-key-12345678"); // 32 bytes for HMACSHA256 ideally
    // Make sure key is 32 bytes or more? HMACSHA256 accepts any length but 64 is block size.
    // Let's use a 32 byte key for safety if needed, or just let bytes be whatever.
    return new AppendOnlyLog(path, key);
});

// Register a singleton to hold active policies in memory (simple version)
builder.Services.AddSingleton<Hft.Governance.Services.PolicyState>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.MapControllers();

app.Run();
