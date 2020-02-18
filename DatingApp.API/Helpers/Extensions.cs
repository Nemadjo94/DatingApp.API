using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DatingApp.API.Helpers
{
    public static class Extensions
    {
        /// <summary>
        /// This is an extension method for HttpResponse class for adding various application errors. 
        /// </summary>
        /// <param name="response"></param>
        /// <param name="message"></param>
        // "this HttpResponse" is the class we are adding this extension method to
        public static void AddApplicationError(this HttpResponse response, string message)
        {
            response.Headers.Add("Application-Error", message); // This sends back the error message
            response.Headers.Add("Access-Control-Expose-Headers", "Applicaiton-Error"); // These last two allow the message to be displayed
            response.Headers.Add("Access-Control-Allow-Origin", "*");
        }

        /// <summary>
        /// This extends our HttpResponse to support pagination 
        /// </summary>
        /// <param name="response"></param>
        /// <param name="currentPage"></param>
        /// <param name="itemsPerPage"></param>
        /// <param name="totalItems"></param>
        /// <param name="totalPages"></param>
        public static void AddPagination(this HttpResponse response, int currentPage, int itemsPerPage, int totalItems, int totalPages)
        {
            // Set up our http header
            var paginationHeader = new PaginationHeader(currentPage, itemsPerPage, totalItems, totalPages);

            // This are just options to send back our header parameters as camelCase and not as TitleCase
            var camelCaseFormatter = new JsonSerializerSettings();
            camelCaseFormatter.ContractResolver = new CamelCasePropertyNamesContractResolver();

            // add our pagination headers to the response and serialize it to json format
            response.Headers.Add("Pagination", JsonConvert.SerializeObject(paginationHeader, camelCaseFormatter));
            // Add access control expose headers to pagination header to stop cors errors
            response.Headers.Add("Access-Control-Expose-Headers", "Pagination");
        }

        /// <summary>
        /// This is an extension method for DateTime class
        /// </summary>
        /// <param name="dateTime"></param>
        /// <returns></returns>
        public static int CalculateAgeFromDate(this DateTime dateTime)
        {
            var age = DateTime.Today.Year - dateTime.Year;

            if(dateTime.AddYears(age) > DateTime.Today)
            {
                age--;
            }

            return age;
        }
    }
}
