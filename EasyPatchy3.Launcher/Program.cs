using MudBlazor.Services;
using EasyPatchy3.Launcher.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
builder.Services.AddMudServices();

// Configure HttpClient for API communication with main app
builder.Services.AddHttpClient("EasyPatchyApi", client =>
{
    var baseUrl = builder.Configuration.GetValue<string>("EasyPatchyApiBaseUrl") ?? "http://easypatch3-app:8080";
    client.BaseAddress = new Uri(baseUrl);
});

// Add launcher services
builder.Services.AddScoped<ApiService>();
builder.Services.AddScoped<LocalAppService>();
builder.Services.AddScoped<PatchApplicationService>();
builder.Services.AddScoped<UpdateService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Run();