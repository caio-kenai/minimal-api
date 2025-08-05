using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Config JWT
var jwtKey = "EstaChaveDeveSerMaisLongaESecreta!123"; // Coloque em appsettings ou secret manager
var issuer = "MinhaAPI";
var audience = "MinhaAPIUsuarios";

// Configuração da autenticação JWT
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters()
        {
            ValidateIssuer = true,
            ValidIssuer = issuer,

            ValidateAudience = true,
            ValidAudience = audience,

            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),

            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };
    });

// Para autorizar endpoints
builder.Services.AddAuthorization();

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

#region Models & Storage (simples, em memória)

record Veiculo(int Id, string Marca, string Modelo, int Ano);

record Administrador(string Username, string Password); // Senha em texto plano apenas para demo (NÃO FAÇA ISSO EM PRODUÇÃO)

var veiculos = new List<Veiculo>();
var administradores = new List<Administrador>();

// Seed admin demo
administradores.Add(new Administrador("admin", "123456"));

int nextVeiculoId = 1;

#endregion

#region Endpoints públicos

// Cadastro administrador (sem autenticação para demo, mas ideal seria restringir)
app.MapPost("/admin/register", (Administrador adm) =>
{
    if (administradores.Any(a => a.Username == adm.Username))
        return Results.BadRequest("Administrador já existe.");

    administradores.Add(adm);
    return Results.Ok("Administrador registrado.");
});

// Login para obter token JWT
app.MapPost("/admin/login", (Administrador login) =>
{
    var adm = administradores.FirstOrDefault(a => a.Username == login.Username && a.Password == login.Password);
    if (adm == null)
        return Results.Unauthorized();

    var tokenHandler = new JwtSecurityTokenHandler();
    var key = Encoding.UTF8.GetBytes(jwtKey);

    var tokenDescriptor = new SecurityTokenDescriptor
    {
        Subject = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.Name, adm.Username),
            new Claim(ClaimTypes.Role, "Administrador")
        }),
        Expires = DateTime.UtcNow.AddHours(2),
        Issuer = issuer,
        Audience = audience,
        SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
    };

    var token = tokenHandler.CreateToken(tokenDescriptor);
    var jwt = tokenHandler.WriteToken(token);

    return Results.Ok(new { token = jwt });
});

// Listar veículos - público
app.MapGet("/veiculos", () =>
{
    return Results.Ok(veiculos);
});

#endregion

#region Endpoints protegidos (Admin)

// Criar veículo
app.MapPost("/veiculos", (Veiculo veiculo) =>
{
    var novoVeiculo = veiculo with { Id = nextVeiculoId++ };
    veiculos.Add(novoVeiculo);
    return Results.Created($"/veiculos/{novoVeiculo.Id}", novoVeiculo);
}).RequireAuthorization();

// Atualizar veículo
app.MapPut("/veiculos/{id:int}", (int id, Veiculo veiculoAtualizado) =>
{
    var veiculo = veiculos.FirstOrDefault(v => v.Id == id);
    if (veiculo == null)
        return Results.NotFound();

    var updated = veiculoAtualizado with { Id = id };
    veiculos.Remove(veiculo);
    veiculos.Add(updated);

    return Results.Ok(updated);
}).RequireAuthorization();

// Deletar veículo
app.MapDelete("/veiculos/{id:int}", (int id) =>
{
    var veiculo = veiculos.FirstOrDefault(v => v.Id == id);
    if (veiculo == null)
        return Results.NotFound();

    veiculos.Remove(veiculo);
    return Results.NoContent();
}).RequireAuthorization();

#endregion

app.Run();
