// Регистрация сервиса телеграм-бота
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using MyMvcApp.Models;
using MyMvcApp.Services;
using System.Configuration; // Replace "YourNamespace" with the actual namespace of the TelegramBotHostedService class


var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddHostedService<TypeWeekDownloadService>();

builder.Services.AddHostedService(provider =>
{
    var botToken = "6715640503:AAEyix5YebsK3FOJHK9G76fBMXjyVM4uHus"; // Получить токен бота из конфигурации
    var configuration = provider.GetRequiredService<IConfiguration>();
    return new TelegramBotHostedService(botToken, configuration);
});

builder.Services.AddHostedService<DocumentCleanupService>();
builder.Services.AddHostedService<NewsEventsDownloadService>();
builder.Services.AddHostedService<ContactDownloadService>();
builder.Services.AddHostedService<ScheduleDownloadService>();
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

app.Run();
