using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

string key = "hel_me_hel_me_hel_me_hel_me_hel_me_hel_me_hel_me_hel_me_hel_me_hel_me_hel_me_hel_me";

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
app.MapGet("/User", async (ApplicationContext db) =>
{
    return await db.List_User.ToListAsync();
});
app.MapGet("/User/nickname/{nickname}", [Authorize] async (string nickname, ApplicationContext db) =>
{
    var user = await db.List_User.FirstOrDefaultAsync(u => u.NickName.ToLower() == nickname.ToLower());
    if (user == null) return Results.NotFound(new { error = "������������ �� ������" });

    return Results.Ok(new
    {
        user.Id,
        user.Email,
        user.NickName,
        user.Role,
        user.PositiveFeedback,
        user.NegativeFeedback
    });
});
app.MapPost("/User/register", async (RegisterUserDTO dto, ApplicationContext db) =>
{
    if (!EmailValidator.ValidateEmail(dto.Email))
        return Results.BadRequest(new { error = "������������ email" });
    if (await db.List_User.AnyAsync(u => u.Email == dto.Email))
        return Results.BadRequest(new { error = "�������� � ����� email ��� ����������" });
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
    return Results.Ok(new { message = "������������ ������� ���������������" });
});
app.MapPost("/User/{id}/feedback", [Authorize] async (string id, FeedbackDTO feedbackDto, HttpContext context, ApplicationContext db) =>
{
    var currentUserId = context.User.Claims.FirstOrDefault(c => c.Type == "Id")?.Value;
    if (currentUserId == null)
        return Results.Unauthorized();

    if (currentUserId == id)
        return Results.BadRequest(new { error = "������ ������� ����� ������ ����" });

    var user = await db.List_User.FindAsync(id);
    if (user == null)
        return Results.NotFound(new { error = "������������ �� ������" });

    if (feedbackDto.TypeFeedback == "positive")
    {
        user.PositiveFeedback++;
    }
    else if (feedbackDto.TypeFeedback == "negative")
    {
        user.NegativeFeedback++;
    }
    else
    {
        return Results.BadRequest(new { error = "�������� ��� ������" });
    }

    await db.SaveChangesAsync();
    return Results.Ok( new { message = "����� ������� ��������" });
});
app.MapPut("/update/User/{Id}", async (string Id, UserDTO dto, ApplicationContext db) =>
{
    User buff = await db.List_User.FindAsync(Id);
    if (buff == null) return Results.NotFound(new { error = "������������ �� ������" });
    if (!string.IsNullOrWhiteSpace(dto.Password))
    {
        PasswordValidator.Validate(dto.Password);
        buff.Password = BCrypt.Net.BCrypt.HashPassword(dto.Password, 12);
    }
    else
        return Results.BadRequest(new { error = "������ ���������� ��� ����������" });

    if (!string.IsNullOrWhiteSpace(dto.NickName))
    {
        buff.NickName = dto.NickName;
    }
    await db.SaveChangesAsync();
    return Results.Ok(new { message = "������ ������������ ���������" });
});
app.MapDelete("/delete/User/{Id}", async (string Id, ApplicationContext db) =>
{
    var del = await db.List_User.FindAsync(Id);
    if (del == null) return Results.NotFound(new { error = "�������� �� ������" });

    db.List_User.Remove(del);
    await db.SaveChangesAsync();
    return Results.Ok(new { message = "������������ �����" });
});
// JWT token
app.MapPost("/login", async (UserRequest reg, ApplicationContext db) =>
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
app.MapGet("/User/me", [Authorize] async (HttpContext context, ApplicationContext db) =>
{
    var userId = context.User.Claims.FirstOrDefault(c => c.Type == "Id")?.Value;
    if (userId == null)
        return Results.Unauthorized();

    var user = await db.List_User.FindAsync(userId);
    if (user == null)
        return Results.NotFound();

    return Results.Ok(new
    {
        user.Email,
        user.NickName,
        user.Role,
        user.PositiveFeedback,
        user.NegativeFeedback
    });
});

app.Run();
