using DatingApp.API.Data.Abstraction;
using DatingApp.API.DTO;
using DatingApp.API.Helpers;
using DatingApp.API.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DatingApp.API.Data
{
    public class DatingRepository : IDatingRepository
    {
        private DataContext _context;

        public DatingRepository(DataContext context)
        {
            _context = context;
        }

        public void Add<T>(T entity) where T : class
        {
            _context.Add(entity);
        }

        public void Delete<T>(T entity) where T : class
        {
            _context.Remove(entity);
        }

        public async Task<Like> GetLike(int userId, int recipientId)
        {
            // This checks if the like between users already exists
            return await _context.Likes.FirstOrDefaultAsync(u => u.LikerId == userId && u.LikeeId == recipientId);
        }

        public async Task<Photo> GetMainPhotoForUser(int userId)
        {
            return await _context.Photos.Where(u => u.User.Id == userId).FirstOrDefaultAsync(p => p.IsMain);
        }

        public async Task<Message> GetMessage(int messageId)
        {
            return await _context.Messages.FirstOrDefaultAsync(m => m.Id == messageId);
        }

        public async Task<PagedList<Message>> GetMessagesForUser(MessageParams messageParams)
        {
            var messages = _context.Messages
                .Include(u => u.Sender).ThenInclude(u => u.Photos)
                .Include(u => u.Recipient).ThenInclude(u => u.Photos)
                .AsQueryable();

            switch (messageParams.MessageContainer)
            {
                case "Inbox":
                    messages = messages.Where(m => m.RecipientId == messageParams.UserId && m.RecipientDeleted == false);
                    break;
                case "Outbox":
                    messages = messages.Where(m => m.SenderId == messageParams.UserId && m.SenderDeleted == false);
                    break;
                default:
                    messages = messages.Where(m => m.RecipientId == messageParams.UserId && m.RecipientDeleted == false && m.IsRead == false);
                    break;
            }

            messages = messages.OrderByDescending(d => d.DateSent);

            return await PagedList<Message>.CreateAsync(messages, messageParams.PageNumber, messageParams.PageSize);
        }

        public async Task<IEnumerable<Message>> GetMessageThread(int userId, int recipientId)
        {
            // Returns the conversation between two users
            var messages = await _context.Messages
                .Include(u => u.Sender).ThenInclude(u => u.Photos)
                .Include(u => u.Recipient).ThenInclude(u => u.Photos)
                .Where(m => m.RecipientId == userId && m.RecipientDeleted == false && m.SenderId == recipientId 
                    || 
                    m.RecipientId == recipientId && m.SenderId == userId && m.SenderDeleted == false)
                .OrderByDescending(m => m.DateSent)
                .ToListAsync();

            return messages;
        }

        public async Task<Photo> GetPhoto(int photoId)
        {
            var photo = await _context.Photos
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(p => p.Id == photoId);

            return photo;
        }

        public async Task<User> GetUser(int id, bool isCurrentUser)
        {
            //var user = await _context.Users.Include(p => p.Photos).FirstOrDefaultAsync(u => u.Id == id);

            //return user;

            var query = _context.Users.Include(p => p.Photos).AsQueryable();

            // if its the current user ignore filters so we can get all of his photos back
            if (isCurrentUser)
                query = query.IgnoreQueryFilters();

            var user = await query.FirstOrDefaultAsync(u => u.Id == id);

            return user;

        }

        //public async Task<IEnumerable<User>> GetUsers()
        //{
        //    var users = await _context.Users.Include(p => p.Photos).ToListAsync();

        //    return users;
        //}

        public async Task<PagedList<User>> GetUsers(UserParams userParams)
        {
            // Get users with their photos. Needs to be queryable so we can apply our query onto it
            var users = _context.Users.Include(p => p.Photos).AsQueryable();

            // Get users without the logged in user, filter out the current user
            users = users.Where(u => u.Id != userParams.UserId);

            // Get users of the specified gender
            users = users.Where(u => u.Gender == userParams.Gender);

            if (userParams.Likers)
            {
                var userLikers = await GetLikers(userParams.UserId);
                users = users.Where(u => userLikers.Contains(u.Id));
            }

            if (userParams.Likees)
            {
                var userLikees = await GetLikees(userParams.UserId);
                users = users.Where(u => userLikees.Contains(u.Id));
            }

            // If these values in if statement dont match that means the user has specified his own age values
            if(userParams.MinAge != 18 || userParams.MaxAge != 99)
            {
                var minDob = DateTime.Today.AddYears(-userParams.MaxAge - 1);
                var maxDob = DateTime.Today.AddYears(-userParams.MinAge);

                users = users.Where(u => u.DateOfBirth >= minDob && u.DateOfBirth <= maxDob);
            }

            if (!string.IsNullOrEmpty(userParams.OrderBy))
            {
                switch (userParams.OrderBy)
                {
                    case "created":
                        users = users.OrderByDescending(u => u.Created);
                        break;
                    default:
                        users = users.OrderByDescending(u => u.LastActive);
                        break;

                }
            }

            return await PagedList<User>.CreateAsync(users, userParams.PageNumber, userParams.PageSize);
        }

        public async Task<bool> SaveAll()
        {
            return await _context.SaveChangesAsync() > 0; // If greater than zero means something has been saved, return true
        }

        // Gets Likers if they're set to true or likees by default
        //private async Task<IEnumerable<int>> GetUserLikes(int userId, bool likers)
        //{
        //    var user = await _context.Users
        //        .Include(u => u.Likers)
        //        .Include(u => u.Likees)
        //        .FirstOrDefaultAsync(u => u.Id == userId);

        //    if (likers)
        //    {
        //        return user.Likers.Where(u => u.LikeeId == userId)
        //            .Select(i => i.LikerId);
        //    }
        //    else
        //    {
        //        return user.Likees.Where(u => u.LikerId == userId)
        //            .Select(i => i.LikeeId);
        //    }

        //}

        private async Task<IEnumerable<int>> GetLikers(int userId)
        {
            var user = await _context.Users
               .Include(u => u.Likers)
               .Include(u => u.Likees)
               .FirstOrDefaultAsync(u => u.Id == userId);

            
             return user.Likers.Where(u => u.LikeeId == userId)
                .Select(i => i.LikerId);
            
        }

        private async Task<IEnumerable<int>> GetLikees(int userId)
        {
            var user = await _context.Users
               .Include(u => u.Likers)
               .Include(u => u.Likees)
               .FirstOrDefaultAsync(u => u.Id == userId);


            return user.Likees.Where(u => u.LikerId == userId)
               .Select(i => i.LikeeId);

        }
    }
}
