using EventProcessing.AggregationService.Workers;
using EventProcessing.Infrastructure;
using EventProcessing.Infrastructure.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddHostedService<EventConsumer>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    // Her iki servis aynı anda DB oluşturmaya çalışabilir, race condition'a karşı retry
    for (int retry = 0; retry < 5; retry++)
    {
        try
        {
            db.Database.Migrate();
            break;
        }
        catch (Microsoft.Data.SqlClient.SqlException ex) when (ex.Number == 1801)
        {
            // Database 'EventProcessingDb' already exists — diğer servis önce davrandı
            if (retry == 4) throw;
            Thread.Sleep(1000 * (retry + 1));
        }
    }
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthorization();
app.MapControllers();

app.Run();