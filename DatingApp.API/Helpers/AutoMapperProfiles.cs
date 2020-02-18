using DatingApp.API.DTO;
using DatingApp.API.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;

namespace DatingApp.API.Helpers
{
    /// <summary>
    /// Helper class for Automapper, where we specify our mapping profiles
    /// </summary>
    public class AutoMapperProfiles : Profile
    {
        public AutoMapperProfiles()
        {
            CreateMap<User, UserForListDTO>()
                .ForMember(destination => destination.PhotoUrl, options =>
                {
                    options.MapFrom(source => source.Photos.FirstOrDefault(photo => photo.IsMain).Url); // We want to map PhotoUrl to the users main photo
                })
                .ForMember(destination => destination.Age, options =>
                {
                    options.ResolveUsing(d => d.DateOfBirth.CalculateAgeFromDate());
                });

            CreateMap<User, UserForDetailedDTO>()
                .ForMember(destination => destination.PhotoUrl, options =>
                {
                    options.MapFrom(source => source.Photos.FirstOrDefault(photo => photo.IsMain).Url); // We want to map PhotoUrl to the users main photo
                })
                .ForMember(destination => destination.Age, options =>
                {
                    options.ResolveUsing(d => d.DateOfBirth.CalculateAgeFromDate());
                });

            CreateMap<Photo, PhotosForDetailedDTO>();
            CreateMap<UserForUpdateDTO, User>();
            CreateMap<Photo, PhotoForReturnDTO>();
            CreateMap<PhotoForCreationDTO, Photo>();
            CreateMap<UserForRegisterDTO, User>();
            CreateMap<MessageForCreationDTO, Message>().ReverseMap();
            CreateMap<Message, MessageToReturnDTO>()
                .ForMember(m => m.SenderPhotoUrl, opt => opt.MapFrom(u => u.Sender.Photos.FirstOrDefault(p => p.IsMain).Url))
                .ForMember(m => m.RecipientPhotoUrl, opt => opt.MapFrom(u => u.Recipient.Photos.FirstOrDefault(p => p.IsMain).Url));

        }
    }
}
