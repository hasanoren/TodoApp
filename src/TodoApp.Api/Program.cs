using Microsoft.AspNetCore.Authentication.JwtBearer;

using Microsoft.EntityFrameworkCore;
using TodoApp.Infrastructure.Data;

using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();   // ---- YENİ: Swagger dokümanını üretir --



// ---- YENİ: DbContext'i DI container'a kaydet ----
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// ---- YENİ: Repository ve Service kayıtları ----
builder.Services.AddScoped<TodoApp.Application.Interfaces.IUserRepository, TodoApp.Infrastructure.Repositories.UserRepository>();
builder.Services.AddScoped<TodoApp.Application.Services.AuthService>();

// ---- YENİ: JWT Authentication yapılandırması ----
var jwtKey = builder.Configuration["Jwt:Key"]!;
var jwtIssuer = builder.Configuration["Jwt:Issuer"];
var jwtAudience = builder.Configuration["Jwt:Audience"];

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();      // ---- YENİ: /swagger/v1/swagger.json dokümanını sunar ----
    app.UseSwaggerUI();    // ---- YENİ: /swagger adresinde görsel arayüzü açar ----

}

app.UseHttpsRedirection();

// ---- YENİ: Authentication, Authorization'dan ÖNCE gelmeli ----
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();