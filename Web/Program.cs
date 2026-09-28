using DotNetEnv;
using GoogleIntegrationService.Infrastructure.Data;
using GoogleIntegrationService.Infrastructure.Google;
using GoogleIntegrationService.Services;
using Microsoft.EntityFrameworkCore;
using GoogleIntegrationService.Web.Extensions;

Env.TraversePath().Load();

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddControllers();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("AppDb")));

builder.Services.AddSingleton<IYouTubeUserService, YouTubeUserService>();
builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(Program).Assembly);
});



builder.Services.AddScoped<TokenService>();
builder.Services.AddScoped<IUserAccountService, UserAccountService>();
builder.Services.AddJwtAuthentication(builder.Configuration);
builder.Services.AddGoogleAuthentication(builder.Configuration);
var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.Migrate();
}

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapControllers();

app.Run();
