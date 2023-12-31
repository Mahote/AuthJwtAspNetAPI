using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using AuthJwtAspNetAPI.Core.Utils;
using AuthJwtAspNetAPI.Core.Dtos;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.IdentityModel.Tokens.Jwt;

namespace AuthJwtAspNetAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly IConfiguration _configuration;

        public AuthController(UserManager<IdentityUser> userManager, RoleManager<IdentityRole> roleManager, IConfiguration configuration)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _configuration = configuration;
        }

        [HttpPost]
        [Route("seed-roles")]
        public async Task<IActionResult> SeedRoles() {

            bool isOwnerRoleExists = await _roleManager.RoleExistsAsync(StaticUserRoles.OWNER);
            bool isAdminRoleExists = await _roleManager.RoleExistsAsync(StaticUserRoles.ADMIN);
            bool isUserRoleExists = await _roleManager.RoleExistsAsync(StaticUserRoles.USER);

            if( isOwnerRoleExists && isAdminRoleExists && isUserRoleExists) {
                return Ok("Roles Seeding is Already Done");
            }
            await _roleManager.CreateAsync(new IdentityRole(StaticUserRoles.USER));
            await _roleManager.CreateAsync(new IdentityRole(StaticUserRoles.ADMIN));
            await _roleManager.CreateAsync(new IdentityRole(StaticUserRoles.OWNER));

            return Ok("role Seeding Done Succesfully");
        }

        [HttpPost]
        [Route("register")]
        public async Task<IActionResult> Register([FromBody] RegisterDto registerDto)
        {
            var isExistingUser = await _userManager.FindByNameAsync(registerDto.UserName);

            if(isExistingUser != null)
            {
                return BadRequest("UserName Already Exists");
            }

            IdentityUser newUser = new IdentityUser () 
            {
                Email = registerDto.Email,
                UserName = registerDto.UserName,
                SecurityStamp = Guid.NewGuid().ToString(),
            };

            var createUserResult = await _userManager.CreateAsync(newUser, registerDto.Password);

            if(!createUserResult.Succeeded) 
            {
                var errorString = "User Creation Failed Because: ";
                foreach(var error in createUserResult.Errors)
                {
                    errorString += " # " + error.Description;
                }
                return BadRequest(errorString);  
            }

            // add default User Role to all users
            await _userManager.AddToRoleAsync(newUser, StaticUserRoles.USER);

            return Ok("User Created Succesfully");

        }

        [HttpPost]
        [Route("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto loginDto)
        {
            var user = await _userManager.FindByNameAsync(loginDto.UserName);
            if( user is null)
            {
                return Unauthorized("Invalid Credentials");
            }
            var isPasswordCorrect = await _userManager.CheckPasswordAsync(user, loginDto.Password);

            if(!isPasswordCorrect)
            {
                return Unauthorized("Invalid Credentials");
            }

            var userRoles = await _userManager.GetRolesAsync(user);

            var authClaims = new List<Claim> 
            {
                new Claim(ClaimTypes.Name, user.UserName),
                new Claim(ClaimTypes.NameIdentifier, user.Id),
                new Claim("JWTID", Guid.NewGuid().ToString()),
            };

            foreach(var userRole in userRoles) {
                authClaims.Add(new Claim(ClaimTypes.Role, userRole));
            }

            var token = GenerateNewJsonWebToken(authClaims);

            return Ok(token);

        }

        private string GenerateNewJsonWebToken(List<Claim> claims) 
        {
            var authSecret = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["JWT:Secret"]));

            var tokenObject = new JwtSecurityToken(
                issuer: _configuration["JWT:ValidIssuer"],
                audience: _configuration["JWT:ValidAudience"],
                expires: DateTime.Now.AddHours(1),
                claims: claims,
                signingCredentials: new SigningCredentials(authSecret, SecurityAlgorithms.HmacSha256)
            );

            string token = new JwtSecurityTokenHandler().WriteToken(tokenObject);

            return token;
        }
    }
}