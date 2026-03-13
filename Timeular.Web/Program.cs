using System.Collections.Generic;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();

var app = builder.Build();

// simple config endpoint for service polling
app.MapGet("/config", (HttpContext ctx) =>
{
    // construct absolute url for whatever host the web app is running on
    var hostUrl = $"{ctx.Request.Scheme}://{ctx.Request.Host}";
    var sample = new Timeular.Core.TimeularConfig
    {
        WebInterfaceUrl = hostUrl,
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
