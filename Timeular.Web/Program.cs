using System.Collections.Generic;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();

var app = builder.Build();

// simple config endpoint for service polling
app.MapGet("/config", () =>
{
    var sample = new Timeular.Core.TimeularConfig
    {
        WebInterfaceUrl = "https://example.com/", // replace with actual URL
        SideLabels = new Dictionary<int, string> { {1, "Work"}, {2, "Break"} }
    };
    return Results.Json(sample);
});

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseRouting();

app.UseAuthorization();

app.MapRazorPages();

app.Run();
