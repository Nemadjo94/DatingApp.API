﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DatingApp.API.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DatingApp.API.DTO;
using Microsoft.AspNetCore.Identity;
using DatingApp.API.Models;
using CloudinaryDotNet;
using DatingApp.API.Helpers;
using Microsoft.Extensions.Options;
using CloudinaryDotNet.Actions;

namespace DatingApp.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AdminController : ControllerBase
    {
        private readonly DataContext _context;
        private readonly UserManager<User> _userManager;
        private readonly IOptions<CloudinarySettings> _cloudinaryConfig;
        private readonly Cloudinary _cloudinary;

        public AdminController(DataContext context, UserManager<User> userManager, IOptions<CloudinarySettings> cloudinaryConfig)
        {
            _context = context;
            _userManager = userManager;
            _cloudinaryConfig = cloudinaryConfig;

            Account account = new Account(
                    cloud: _cloudinaryConfig.Value.CloudName,
                    apiKey: _cloudinaryConfig.Value.ApiKey,
                    apiSecret: _cloudinaryConfig.Value.ApiSecret
                );

            _cloudinary = new Cloudinary(account);
        }

        [Authorize(Policy = "RequireAdminRole")]
        [HttpGet("usersWithRoles")]
        public async Task<IActionResult> GetUsersWithRoles()
        {
            var userList = await (from user in _context.Users
                                  orderby user.UserName
                                  select new
                                  {
                                      Id = user.Id,
                                      UserName = user.UserName,
                                      Roles = (from userRole in user.UserRoles
                                               join role in _context.Roles
                                               on userRole.RoleId
                                               equals role.Id
                                               select role.Name).ToList()
                                  }).ToListAsync();

            return Ok(userList);
        }

        [Authorize(Policy = "RequireAdminRole")]
        [HttpPost("editRoles/{userName}")]
        public async Task<IActionResult> EditRoles(string userName, RoleEditDTO roleEditDTO)
        {
            // TODO: add model state validation
            // Get user
            var user = await _userManager.FindByNameAsync(userName);
            // Get roles for that user
            var userRoles = await _userManager.GetRolesAsync(user);

            var selectedRoles = roleEditDTO.RoleNames;

            selectedRoles = selectedRoles ?? new string[] { }; // selectedRoles = selectedRoles != null ? selectedRoles : new string[] {};
            // add user to the selected roles except the ones he is already on
            var result = await _userManager.AddToRolesAsync(user, selectedRoles.Except(userRoles));

            if (!result.Succeeded)
            {
                return BadRequest("Failed to add to roles");
            }
            // remove user from the selected roles except the ones he is already on
            result = await _userManager.RemoveFromRolesAsync(user, userRoles.Except(selectedRoles));

            if (!result.Succeeded)
            {
                return BadRequest("Failed to remove the roles");
            }

            return Ok(await _userManager.GetRolesAsync(user));
        }

        [Authorize(Policy = "ModeratePhotoRole")]
        [HttpGet("photosForModeration")]
        public async Task<IActionResult> GetPhotosForModeration()
        {
            var photos = await _context.Photos
                .Include(u => u.User)
                .IgnoreQueryFilters()
                .Where(p => p.IsApproved == false)
                .Select(u => new
                {
                    Id = u.Id,
                    UserName = u.User.UserName,
                    Url = u.Url,
                    IsApproved = u.IsApproved
                }).ToListAsync();

            return Ok(photos);
        }

        [Authorize(Policy = "ModeratePhotoRole")]
        [HttpPost("approvePhoto/{photoId}")]
        public async Task<IActionResult> ApprovePhoto(int photoId)
        {
            var photo = await _context.Photos
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(p => p.Id == photoId);

            photo.IsApproved = true;

            await _context.SaveChangesAsync();

            return Ok();
        }

        [Authorize(Policy = "ModeratePhotoRole")]
        [HttpPost("rejectPhoto/{photoId}")]
        public async Task<IActionResult> RejectPhoto(int photoId)
        {
            var photo = await _context.Photos
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(p => p.Id == photoId);

            if (photo.IsMain)
                return BadRequest("You cannot reject the main photo");

            if (photo.PublicId != null)
            {
                var deleteParams = new DeletionParams(photo.PublicId);

                var result = _cloudinary.Destroy(deleteParams);

                if (result.Result == "ok")
                {
                    _context.Photos.Remove(photo);
                }
            }

            if (photo.PublicId == null)
            {
                _context.Photos.Remove(photo);
            }

            await _context.SaveChangesAsync();

            return Ok();
        }
    }
}