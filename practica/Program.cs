using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

string key = "hel_me";

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme()
    {
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});
builder.Services.AddCors();
builder.Services.AddAuthorization();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
            ValidateIssuerSigningKey = true,
        };
    });

builder.Services.AddDbContext<ApplicationContext>(options =>
{
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"));
});
var app = builder.Build();
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.UseCors(policy => policy
    .AllowAnyOrigin()
    .AllowAnyHeader()
    .AllowAnyMethod()
);
app.UseSwagger();
app.UseSwaggerUI();
//user
app.MapGet("/User", async (ApplicationContext db) =>
{
    return await db.List_User.ToListAsync();
});
app.MapPost("/User/register", async (RegisterUserDTO dto, ApplicationContext db) =>
{
    if (!EmailValidator.ValidateEmail(dto.Email))
        return Results.BadRequest(new { error = "Некорректный email" });
    if (await db.List_User.AnyAsync(u => u.Email == dto.Email))
        return Results.BadRequest(new { error = "Участник с таким email уже существует" });
    PasswordValidator.Validate(dto.Password);
    var user = new User
    {
        Id = Guid.NewGuid().ToString(),
        Email = dto.Email!,
        Password = BCrypt.Net.BCrypt.HashPassword(dto.Password!, 12),
        NickName = dto.NickName!,
        Role = "User",
        PositiveFeedback = 0,
        NegativeFeedback = 0
    };

    await db.List_User.AddAsync(user);
    await db.SaveChangesAsync();
    return Results.Ok(new { message = "Пользователь успешно зарегистрирован" });
});
app.MapPut("/update/User/{Id}", async (string Id, UserDTO dto, ApplicationContext db) =>
{
    User buff = await db.List_User.FindAsync(Id);
    if (buff == null) return Results.NotFound(new { error = "Пользователь не найден" });
    if (!string.IsNullOrWhiteSpace(dto.Password))
    {
        PasswordValidator.Validate(dto.Password);
        buff.Password = BCrypt.Net.BCrypt.HashPassword(dto.Password, 12);
    }
    else
        return Results.BadRequest(new { error = "Пароль обязателен для обновления" });

    if (!string.IsNullOrWhiteSpace(dto.NickName))
    {
        buff.NickName = dto.NickName;
    }
    await db.SaveChangesAsync();
    return Results.Ok(new { message = "Данные пользователя обновлены" });
});
app.MapDelete("/delete/User/{Id}", async (string Id, ApplicationContext db) =>
{
    var del = await db.List_User.FindAsync(Id);
    if (del == null) return Results.NotFound(new { error = "Участник не найден" });

    db.List_User.Remove(del);
    await db.SaveChangesAsync();
    return Results.Ok(new { message = "Пользователь удалён" });
});
// JWT token
app.Map("/login/{username}", async (UserRequest reg, ApplicationContext db) =>
{
    var user = await db.List_User.FirstOrDefaultAsync(s => s.Email == reg.Email);
    var claims = new List<Claim>
    {
        new Claim("Id", user.Id.ToString()),
        new Claim("Role", user.Role),
        new Claim("Email", user.Email)
    };
    // создаем JWT-токен
    var jwt = new JwtSecurityToken(
            claims: claims,
            expires: DateTime.UtcNow.Add(TimeSpan.FromMinutes(20)),
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
                SecurityAlgorithms.HmacSha256));

    return new JwtSecurityTokenHandler().WriteToken(jwt);
});
app.MapPost("/login/{username}", async (UserRequest reg, ApplicationContext db) =>
{
    var user = await db.List_User.FirstOrDefaultAsync(s => s.Email == reg.Email);
    if (user == null)
        return Results.Unauthorized();

    var claims = new List<Claim>
    {
        new Claim("Id", user.Id.ToString()),
        new Claim("Role", user.Role),
        new Claim("Email", user.Email)
    };

    var jwt = new JwtSecurityToken(
        claims: claims,
        expires: DateTime.UtcNow.Add(TimeSpan.FromMinutes(20)),
        signingCredentials: new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
            SecurityAlgorithms.HmacSha256));

    var encodedJwt = new JwtSecurityTokenHandler().WriteToken(jwt);

    var response = new
    {
        access_token = encodedJwt,
        username = user.Email
    };

    return Results.Json(response);
});
app.MapGet("/data", [Authorize] () => new { message = "Hello World!" });
app.Run();