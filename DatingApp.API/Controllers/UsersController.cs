using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using AutoMapper;
using DatingApp.API.Data.Abstraction;
using DatingApp.API.DTO;
using DatingApp.API.Helpers;
using DatingApp.API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DatingApp.API.Controllers
{
    // Any time any of the methods activate we should in turn update the last active property by calling the LogUserActivity service filter
    [ServiceFilter(typeof(LogUserActivity))]
    // [Authorize] // we now authenticate globally rather in each controller
    [Route("api/[controller]")]
    [ApiController]
    public class UsersController : ControllerBase
    {
        private readonly IDatingRepository _repository;
        private IMapper _mapper;

        public UsersController(IDatingRepository repository, IMapper mapper)
        {
            _repository = repository;
            _mapper = mapper;
        }

        [HttpGet]
        public async Task<IActionResult> GetUsers([FromQuery]UserParams userParams)
        {
            // Get the user id from the passed token from the client
            var currentUserId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);
            // Get the currently logged in user from the repo
            var userFromRepo = await _repository.GetUser(currentUserId, true);
            // Add current user id to users params to be excluded from search
            userParams.UserId = currentUserId;
            // Check if there's gender specified in user params
            if (string.IsNullOrEmpty(userParams.Gender))
            {
                // return users of oposite gender
                userParams.Gender = userFromRepo.Gender == "male" ? "female" : "male";
            }

            var users = await _repository.GetUsers(userParams);

            var usersToReturn = _mapper.Map<IEnumerable<UserForListDTO>>(users);

            // Return to client our pagination information through headers
            Response.AddPagination(users.CurrentPage, users.PageSize, users.TotalCount, users.TotalPages);

            return Ok(usersToReturn);
        }

        [HttpGet("{userId}", Name = "GetUser")]
        public async Task<IActionResult> GetUser(int userId)
        {
            var isCurrentUser = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value) == userId;

            var user = await _repository.GetUser(userId, isCurrentUser);

            // We use automapper to map our dto's and data class
            var userToReturn = _mapper.Map<UserForDetailedDTO>(user);

            return Ok(userToReturn);
        }

        [HttpPut("{userId}")]
        public async Task<IActionResult> UpdateUser(int userId, [FromBody]UserForUpdateDTO userForUpdate)
        {
            // Check if the user is the current user that is passing this token
            if (userId != int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value))
                return Unauthorized();

            var user = await _repository.GetUser(userId, true);

            _mapper.Map(userForUpdate, user);

            if (await _repository.SaveAll())
                return NoContent();

            throw new Exception($"Updating user {userId} failed on save");
        }

        [HttpPost("{userId}/like/{recipientId}")]
        public async Task<IActionResult> LikeUser(int userId, int recipientId)
        {
            // Check if the user is the current user that is passing this token
            if (userId != int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value))
                return Unauthorized();
            // Attempt to get a like
            var like = await _repository.GetLike(userId, recipientId);
            // Check if like exists
            if(like != null)
            {
                return BadRequest("You already like this user");
            }
            // Check if the recipitent exists
            if(await _repository.GetUser(recipientId, false) == null)
            {
                return NotFound("User not found");
            }

            like = new Like
            {
                LikerId = userId,
                LikeeId = recipientId
            };
            // Save the like
            _repository.Add<Like>(like);
            // Persist to db
            if (await _repository.SaveAll())
                return Ok();

            return BadRequest("Something went bad, failed to like user");
        }

    }
}