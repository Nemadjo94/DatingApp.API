using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using AutoMapper;
using DatingApp.API.Data.Abstraction;
using DatingApp.API.DTO;
using DatingApp.API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace DatingApp.API.Controllers
{
    [AllowAnonymous]
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        // private readonly IAuthRepository _repository;
        private readonly IConfiguration _configuration;
        private readonly IMapper _mapper;
        private readonly UserManager<User> _userManager;
        private readonly SignInManager<User> _signInManager;

        public AuthController(/*IAuthRepository repository*/  UserManager<User> userManager, SignInManager<User> signInManager, IConfiguration configuration, IMapper mapper)
        {
            // _repository = repository;
            _configuration = configuration;
            _mapper = mapper;
            _userManager = userManager;
            _signInManager = signInManager;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody]UserForRegisterDTO userDTO)
        {
            // validate request

            //if (!ModelState.IsValid)
            //{
            //    return BadRequest(ModelState);
            //}

            //userDTO.Username = userDTO.Username.ToLower(); // Da nemamo imena sa lower casom u bazi

            //if (await _repository.UserExists(userDTO.Username))
            //{
            //    return BadRequest("Username already exists");
            //}

            var userToCreate = _mapper.Map<User>(userDTO);

            var result = await _userManager.CreateAsync(userToCreate, userDTO.Password);

            // var createdUser = await _repository.Register(userToCreate, userDTO.Password);

            var userToReturn = _mapper.Map<UserForDetailedDTO>(userToCreate);

            if (result.Succeeded)
            {
                return CreatedAtRoute("GetUser", new { controller = "Users", userId = userToCreate.Id }, userToReturn);
            }

            return BadRequest(result.Errors);
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody]UserForLoginDTO userDTO)
        {
            // Proveravamo da li korisnik postoji
            //var userFromRepo = await _repository.Login(userDTO.Username.ToLower(), userDTO.Password);

            //if (userFromRepo == null)  // Znaci ako nema korisnika vrati unauthorized. Ne obavestavamo da li je username dobar, jer hakeri ce probati brute forcom da udju onda
            //    return Unauthorized();

            // Get identity user
            var user = await _userManager.FindByNameAsync(userDTO.UserName);
            // Attempt to sign in a user with sign in manager
            var result = await _signInManager.CheckPasswordSignInAsync(user, userDTO.Password, false);

            if (user == null)
            {
                return NotFound("User not found"); // remove later
            }

            if (result.Succeeded)
            {
                var appUser = await _userManager.Users
                    .Include(p => p.Photos)
                    .FirstOrDefaultAsync(u => u.NormalizedUserName == userDTO.UserName.ToUpper());

                // we return user because we want to store users main photo in browsers local storage, for nav bar to access it
                var userToReturn = _mapper.Map<UserForListDTO>(appUser); // Maybe just make a dto that returns that photo url

                // return token back to client with user object 
                return Ok(new
                {
                    token = GenerateJwtToken(appUser).Result,
                    user = userToReturn // or user?
                });
            }

            return Unauthorized();
          
        }

        private async Task<string> GenerateJwtToken(User user)
        {
            var claims = new List<Claim> // Nas korisnik ce imati dva claim-a 
           {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.UserName)
            };

            var roles = await _userManager.GetRolesAsync(user);

            foreach (var role in roles)
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }

            // Pravimo kljuc za jwt koji iz appsettings.json fajla uzima secret key vrednost
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration.GetSection("AppSettings:Token").Value));
            // Hasujemo kljuc
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha512Signature);
            // Kreiramo token
            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims), // Claimovi usera su subject
                Expires = DateTime.Now.AddDays(1), // Vreme trajanja tokena
                SigningCredentials = creds // 
            };
            // Kreiramo JWT token
            var tokenHandler = new JwtSecurityTokenHandler();
            // Pravimo token
            var token = tokenHandler.CreateToken(tokenDescriptor);
            // Write back our jwt token
            return tokenHandler.WriteToken(token);
        }

    }
}