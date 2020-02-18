using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using AutoMapper;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using DatingApp.API.Data.Abstraction;
using DatingApp.API.DTO;
using DatingApp.API.Helpers;
using DatingApp.API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace DatingApp.API.Controllers
{
    // [Authorize] // we now authenticate globally rather in each controller
    [Route("api/users/{userId}/photos")]
    [ApiController]
    public class PhotosController : ControllerBase
    {
        private IDatingRepository _repository;
        private IMapper _mapper;
        private IOptions<CloudinarySettings> _cloudinaryConfig;
        private Cloudinary _cloudinary;

        public PhotosController(IDatingRepository repository, IMapper mapper, IOptions<CloudinarySettings> cloudinaryConfig)
        {
            _repository = repository;
            _mapper = mapper;
            _cloudinaryConfig = cloudinaryConfig;

            Account account = new Account(     
                _cloudinaryConfig.Value.CloudName,
                _cloudinaryConfig.Value.ApiKey,
                _cloudinaryConfig.Value.ApiSecret
            );

            _cloudinary = new Cloudinary(account);
        }

        [HttpGet("{id}", Name = "GetPhoto")]
        public async Task<IActionResult> GetPhoto(int photoId)
        {
            var photo = await _repository.GetPhoto(photoId);

            var photoDTO = _mapper.Map<PhotoForReturnDTO>(photo);

            return Ok(photoDTO);
        }


        [HttpPost]
        public async Task<IActionResult> AddPhotoForUser(int userId, [FromForm]PhotoForCreationDTO photoForCreation)
        { 
            // Compare user id against root parameter, authorize the user
            var isCurrentUser = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value) == userId;
           
            if (!isCurrentUser)
                return Unauthorized();
            //Get the user from repository
            var user = await _repository.GetUser(userId, isCurrentUser);
            // Get the file that we're sending to this api call
            var file = photoForCreation.File;
            // Cloudinary class where we store upload result
            var uploadResult = new ImageUploadResult();

            if(file.Length > 0)
            {
                // Create new file stream by reading the contents of the file we're uploading
                using(var stream = file.OpenReadStream())
                {
                    // Create our upload parameters
                    var uploadParams = new ImageUploadParams()
                    {
                        File = new FileDescription(file.Name, stream),
                        // Cloudinary transformation, we're cropping photos and focusing on user's face
                        Transformation = new Transformation().Width(500).Height(500).Crop("fill").Gravity("face")
                    };
                    // Cloudinary upload method, this is where we upload the photo to Cloudinary
                    uploadResult = _cloudinary.Upload(uploadParams); // This also returns important info such as image URL and public key we need to store in our db for later accessing
                }
            }

            // Get Url and PublicId from Cloudinary upload result
            photoForCreation.Url = uploadResult.Uri.ToString();
            photoForCreation.PublicId = uploadResult.PublicId;

            var photo = _mapper.Map<Photo>(photoForCreation);
            // If the users hasn't got any photos, set this one to be main photo
            if (!user.Photos.Any(u => u.IsMain))
                photo.IsMain = true;
            // Store our photo object in our db
            user.Photos.Add(photo);

            if (await _repository.SaveAll()) 
            {
                // photoToReturn is information we're sending back to the client
                var photoToReturn = _mapper.Map<PhotoForReturnDTO>(photo);
                // CreatedAtRoute call our GetPhoto method to return photo information back to the client
                return CreatedAtRoute("GetPhoto", new { id = photo.Id }, photoToReturn);
            }

            return BadRequest("Could not add the photo");

        }

        [HttpPost("{id}/setMain")]
        public async Task<IActionResult> SetMainPhoto(int userId, int id)
        {
            // Compare user id against root parameter, authorize the user
            var isCurrentUser = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value) == userId;

            if (!isCurrentUser)
                return Unauthorized();

            var user = await _repository.GetUser(userId, isCurrentUser);

            if (!user.Photos.Any(p => p.Id == id))
                return Unauthorized();

            var photo = await _repository.GetPhoto(id);

            if (photo.IsMain)
                return BadRequest("This is already a main photo");

            var currentMainPhoto = await _repository.GetMainPhotoForUser(userId);

            currentMainPhoto.IsMain = false;

            photo.IsMain = true;

            if (await _repository.SaveAll())
                return NoContent();

            return BadRequest("Could not set photo to main");
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeletePhoto(int userId, int id)
        {
            // Compare user id against root parameter, authorize the user
            var isCurrentUser = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value) == userId;

            if (!isCurrentUser)
                return Unauthorized();

            var user = await _repository.GetUser(userId, isCurrentUser);

            if (!user.Photos.Any(p => p.Id == id))
                return Unauthorized();

            var photo = await _repository.GetPhoto(id);

            if (photo.IsMain)
                return BadRequest("You cannot delete your main photo");

            if(photo.PublicId != null)
            {
                var deleteParams = new DeletionParams(photo.PublicId);

                var result = _cloudinary.Destroy(deleteParams);

                if (result.Result == "ok")
                {
                    _repository.Delete(photo);
                }
            }

            if(photo.PublicId == null)
            {
                _repository.Delete(photo);
            }

            if(await _repository.SaveAll())
            {
                return Ok();
            }

            return BadRequest("Failed to delete the photo");
        }
    }
}