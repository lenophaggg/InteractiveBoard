// Регистрация сервиса телеграм-бота
using MyMvcApp.Services; // Replace "YourNamespace" with the actual namespace of the TelegramBotHostedService class


var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

builder.Services.AddHostedService(provider =>
{
    var botToken = "6715640503:AAH0j3XDbuWuAo2mSIULSrNct_8mj5Y41wY"; // Получить токен бота из конфигурации
    var configuration = provider.GetRequiredService<IConfiguration>();
    return new TelegramBotHostedService(botToken, configuration);
});

builder.Services.AddHostedService<DocumentCleanupService>();
builder.Services.AddHostedService<NewsEventsDownloadService>();
builder.Services.AddHostedService<ScheduleDownloadService>();
builder.Services.AddHostedService<ContactDownloadService>();
// ...
builder.Services.AddHostedService<TypeWeekDownloadService>();


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
