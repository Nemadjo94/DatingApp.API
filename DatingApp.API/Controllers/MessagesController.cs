using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using AutoMapper;
using DatingApp.API.Data.Abstraction;
using DatingApp.API.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using DatingApp.API.DTO;
using DatingApp.API.Models;

namespace DatingApp.API.Controllers
{
    // Any time any of the methods activate we should in turn update the last active property by calling the LogUserActivity service filter
    [ServiceFilter(typeof(LogUserActivity))]
    // [Authorize] // we now authenticate globally rather in each controller
    [Route("api/users/{userId}/[controller]")]
    [ApiController]
    public class MessagesController : ControllerBase
    {
        private readonly IDatingRepository _repository;
        private readonly IMapper _mapper;

        public MessagesController(IDatingRepository repository, IMapper mapper)
        {
            _repository = repository;
            _mapper = mapper;
        }

        [HttpGet("{messageId}", Name = "GetMessage")]
        public async Task<IActionResult> GetMessage(int userId, int messageId)
        {
            //// Check if the user is the current user that is passing this token
            //if (userId != int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value))
            //    return Unauthorized();

            // Compare user id against root parameter, authorize the user
            var isCurrentUser = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value) == userId;

            if (!isCurrentUser)
                return Unauthorized();

            var message = await _repository.GetMessage(messageId);

            if (message != null)
            {
                return Ok(message);
            }
            else
            {
                return NotFound("Message not found");
            }

        }

        [HttpGet]
        public async Task<IActionResult> GetMessagesForUser(int userId, [FromQuery]MessageParams messageParams)
        {
            // Compare user id against root parameter, authorize the user
            var isCurrentUser = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value) == userId;

            if (!isCurrentUser)
                return Unauthorized();

            messageParams.UserId = userId;

            var messagesFromRepo = await _repository.GetMessagesForUser(messageParams);

            var messages = _mapper.Map<IEnumerable<MessageToReturnDTO>>(messagesFromRepo);

            Response.AddPagination(messagesFromRepo.CurrentPage, messagesFromRepo.PageSize, messagesFromRepo.TotalCount, messagesFromRepo.TotalPages);

            return Ok(messages);
        }

        [HttpGet("thread/{recipientId}")]
        public async Task<IActionResult> GetMessageThread(int userId, int recipientId)
        {
            // Compare user id against root parameter, authorize the user
            var isCurrentUser = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value) == userId;

            if (!isCurrentUser)
                return Unauthorized();

            //TODO: we could make messages as read inside this method

            var messagesFromRepo = await _repository.GetMessageThread(userId, recipientId);

            //foreach(var message in messagesFromRepo)
            //{
            //    message.IsRead = true;
            //}

            var messageThread = _mapper.Map<IEnumerable<MessageToReturnDTO>>(messagesFromRepo);

            return Ok(messageThread);
        }


        [HttpPost]
        public async Task<IActionResult> CreateMessage(int userId, [FromBody]MessageForCreationDTO messageForCreationDTO)
        {
            var sender = await _repository.GetUser(userId, false);

            // Compare user id against root parameter, authorize the user
            var isCurrentUser = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value) == userId;

            if (sender == null || !isCurrentUser)
                return Unauthorized();

            if (messageForCreationDTO == null || messageForCreationDTO.Content == null)
                return BadRequest("Message cannot be empty");

            messageForCreationDTO.SenderId = userId;

            // Get the recipient and check if exists
            var recipient = await _repository.GetUser(messageForCreationDTO.RecipientId, false);

            if (recipient == null)
            {
                return BadRequest("Could not find user");
            }

            var message = _mapper.Map<Message>(messageForCreationDTO);
            // Add message to be saved
            _repository.Add(message);
            // If the message is saved then we get correct sender id from db
            if (await _repository.SaveAll())
            {   // we return message info tohether with senders and recipients photos to display on the client
                var messageToReturn = _mapper.Map<MessageToReturnDTO>(message);
                return CreatedAtRoute("GetMessage", new { messageId = message.Id }, messageToReturn);
            }

            return BadRequest("Something went wrong");
        }

        /// <summary>
        /// We are using http post method because message is deleted from db if two sides delete it.
        /// Otherwise its a one sided deletion.
        /// </summary>
        /// <param name="messageId"></param>
        /// <param name="userId"></param>
        /// <returns></returns>
        [HttpPost("{messageId}")]
        public async Task<IActionResult> DeleteMessage(int messageId, int userId)
        {
            // Compare user id against root parameter, authorize the user
            var isCurrentUser = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value) == userId;

            if (!isCurrentUser)
                return Unauthorized();

            // Get message from repo
            var messageFromRepo = await _repository.GetMessage(messageId);

            // Check to see who sent or received the message and change the delete prop to true
            if(messageFromRepo.SenderId == userId)
            {
                messageFromRepo.SenderDeleted = true;
            }

            if(messageFromRepo.RecipientId == userId)
            {
                messageFromRepo.RecipientDeleted = true;
            }
            // If both users deleted the message, remove the message from db
            if(messageFromRepo.SenderDeleted && messageFromRepo.RecipientDeleted)
            {
                _repository.Delete<Message>(messageFromRepo);
            }

            if (await _repository.SaveAll())
                return NoContent();

            return BadRequest("Something went wrong, message could not be deleted");
        }

        [HttpPost("{messageId}/read")]
        public async Task<IActionResult> MarkMessageAsRead(int messageId, int userId)
        {
            //This only work for one message, we have to call api for each message, find better solution

            // Compare user id against root parameter, authorize the user
            var isCurrentUser = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value) == userId;

            if (!isCurrentUser)
                return Unauthorized();

            var message = await _repository.GetMessage(messageId);

            if (message.RecipientId != userId)
                return Unauthorized();

            message.IsRead = true;
            message.DateRead = DateTime.Now;

            if (await _repository.SaveAll())
                return NoContent();

            return BadRequest("Something went wrong, message could not be read");
        }

    }
}