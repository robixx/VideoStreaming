using Microsoft.EntityFrameworkCore;
using Streaming.Application.Interface;
using Streaming.Infrastructure.Data;
using Streaming.Infrastructure.Service;

var builder = WebApplication.CreateBuilder(args);
// ===== Add CORS policy =====
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy
            .AllowAnyOrigin()   // allow requests from any domain
            .AllowAnyHeader()   // allow all headers
            .AllowAnyMethod();  // allow GET, POST, PUT, DELETE etc.
    });
});

// Add services to the container.
builder.Services.AddDbContext<DatabaseConnection>
    (options => options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddScoped<IVideoStream, VideoStreamingService>();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors("AllowAll");

app.UseAuthorization();

app.MapControllers();

app.Run();
