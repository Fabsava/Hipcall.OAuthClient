using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpClient();

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/callback", async (string? code, IHttpClientFactory httpClientFactory, IConfiguration configuration) =>
{
    if (string.IsNullOrEmpty(code))
    {
        return Results.Content("<h3>Hata: Yetkilendirme kodu bulunamadı.</h3>", "text/html; charset=utf-8");
    }

    var tokenEndpoint = "https://use.hipcall.com.tr/oauth/token";
    var clientId = configuration["OAuth:ClientId"] ?? throw new InvalidOperationException("ClientId is missing in appsettings.json");
    var clientSecret = configuration["OAuth:ClientSecret"] ?? throw new InvalidOperationException("ClientSecret is missing in appsettings.json");
    var redirectUri = configuration["OAuth:RedirectUri"] ?? throw new InvalidOperationException("RedirectUri is missing in appsettings.json");

    var httpClient = httpClientFactory.CreateClient();
    
    var requestContent = new FormUrlEncodedContent(new Dictionary<string, string>
    {
        { "client_id", clientId },
        { "client_secret", clientSecret },
        { "redirect_uri", redirectUri },
        { "grant_type", "authorization_code" },
        { "code", code }
    });

    try
    {
        var response = await httpClient.PostAsync(tokenEndpoint, requestContent);
        var jsonResponse = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            return Results.Content($"<h3 style='color:red;'>Giriş Başarısız (Token alınamadı)</h3><p>{jsonResponse}</p>", "text/html; charset=utf-8");
        }

        using var jsonDoc = JsonDocument.Parse(jsonResponse);
        var accessToken = jsonDoc.RootElement.GetProperty("access_token").GetString();
        
        var profileEndpoint = "https://use.hipcall.com.tr/api/v3/profile";
        
        var profileRequest = new HttpRequestMessage(HttpMethod.Get, profileEndpoint);
        profileRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
        
        var profileResponse = await httpClient.SendAsync(profileRequest);
        var profileJsonResponse = await profileResponse.Content.ReadAsStringAsync();

        // Sadece müşteriye gösterilecek şık bir HTML sayfası döndürüyoruz.
        // Token bilgilerini backend'de gizli tutuyoruz, sadece profil verilerini ekrana basıyoruz.
        var html = $@"
        <!DOCTYPE html>
        <html lang='tr'>
        <head>
            <meta charset='UTF-8'>
            <meta name='viewport' content='width=device-width, initial-scale=1.0'>
            <title>Müşteri Paneli</title>
            <style>
                body {{ font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; background-color: #f3f4f6; color: #1f2937; display: flex; justify-content: center; padding: 40px 20px; margin: 0; }}
                .card {{ background-color: white; border-radius: 12px; box-shadow: 0 4px 15px rgba(0,0,0,0.05); max-width: 700px; width: 100%; padding: 30px; }}
                .header {{ border-bottom: 2px solid #e5e7eb; padding-bottom: 15px; margin-bottom: 25px; text-align: center; }}
                .header-icon {{ font-size: 48px; margin-bottom: 10px; color: #10b981; }}
                h2 {{ margin: 0; color: #111827; font-size: 26px; }}
                p.subtitle {{ color: #6b7280; margin-top: 8px; font-size: 15px; }}
                .data-container {{ background-color: #1f2937; border-radius: 8px; padding: 0; overflow: hidden; }}
                .data-header {{ background-color: #374151; padding: 10px 15px; color: #e5e7eb; font-size: 13px; font-weight: 600; display: flex; justify-content: space-between; }}
                pre {{ color: #a7f3d0; padding: 20px; margin: 0; overflow-x: auto; font-size: 14px; line-height: 1.6; font-family: 'Consolas', 'Monaco', monospace; }}
            </style>
        </head>
        <body>
            <div class='card'>
                <div class='header'>
                    <div class='header-icon'>✓</div>
                    <h2>Hesabınız Bağlandı</h2>
                    <p class='subtitle'>Hipcall üzerinden başarılı bir şekilde giriş yaptınız. Aşağıda hesabınıza ait profil bilgileri yer almaktadır.</p>
                </div>
                <div class='content'>
                    <div class='data-container'>
                        <div class='data-header'>
                            <span>Profil Verileri (JSON)</span>
                            <span>Salt Okunur</span>
                        </div>
                        <pre id='jsonDisplay'></pre>
                    </div>
                </div>
            </div>
            
            <script>
                // API'den gelen Profil JSON verisini buraya enjekte ediyoruz
                const profileData = {profileJsonResponse};
                
                // HTML içerisindeki pre etiketine güzelce (girintili) yazdırıyoruz
                document.getElementById('jsonDisplay').textContent = JSON.stringify(profileData, null, 4);
            </script>
        </body>
        </html>";
        
        return Results.Content(html, "text/html; charset=utf-8");
    }
    catch (Exception ex)
    {
        return Results.Content($"<h3>Bir sistem hatası oluştu:</h3><p>{ex.Message}</p>", "text/html; charset=utf-8");
    }
});

app.Run();
