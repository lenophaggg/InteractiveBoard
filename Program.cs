// Регистрация сервиса телеграм-бота
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using MyMvcApp.Models;
using MyMvcApp.Services;
using MyMvcApp.Hubs;
using Microsoft.AspNetCore.SignalR;



var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddHttpClient("UaiApi", c =>
{
    c.BaseAddress = new Uri("http://192.168.0.101:8001/");
});

builder.Services.AddHttpClient("Smtu", c =>
{
    c.BaseAddress = new Uri("https://www.smtu.ru/");
});

builder.Services.AddHostedService<TypeWeekDownloadService>();

builder.Services.AddSignalR();
builder.Services.AddHostedService(provider =>
{
    var botToken = "6715640503:AAGpLQ4y3oneUL5dAqPBfzKzbgFvIwccS-Q";
    var configuration = provider.GetRequiredService<IConfiguration>();
    var hub = provider.GetRequiredService<IHubContext<ChatHub>>();
    var httpClientFactory = provider.GetRequiredService<IHttpClientFactory>(); // Внедряем фабрику
    return new TelegramBotHostedService(botToken, configuration, hub, httpClientFactory);
});

//builder.Services.AddHostedService<DocumentCleanupService>();
//builder.Services.AddHostedService<NewsEventsDownloadService>();

builder.Services.AddHostedService<ScheduleDownloadService>();
builder.Services.AddHostedService<ContactForNLPDownloadService>();

builder.Services.AddScoped<ChatService>(sp =>
{
    var http = sp.GetRequiredService<IHttpClientFactory>().CreateClient("UaiApi");
    return new ChatService(http);
});

builder.Services.AddScoped<ChatFormatter>();

// ...


// Add services to the container.
builder.Services.AddControllersWithViews();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapHub<ChatHub>("/chathub");

app.Run();
