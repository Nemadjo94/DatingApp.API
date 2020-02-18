using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DatingApp.API.Helpers
{
	/// <summary>
	/// This is options for our pagination that we're sending back to the client
	/// These options are passed through our url parameters from the client
	/// </summary>
    public class UserParams
    {
		private const int MaxPageSize = 50; // max 50 items per page allowed
        public int PageNumber { get; set; } = 1; // default page is always 1

		private int pageSize = 10; // Default users per page

		public int PageSize
		{
			get { return pageSize; }
			// Dont allow page size to be set from more than maximum which is 50 in this case
			set { pageSize = (value > MaxPageSize) ? MaxPageSize : value; }
		}

		// We need current users id to be passed so we can filter him out from members search
		public int UserId { get; set; }

		// We want user to be able to choose the gender he wants displayed
		public string Gender { get; set; }

		public int MinAge { get; set; } = 18;

		public int MaxAge { get; set; } = 99;

		public string OrderBy { get; set; }

		public bool Likees { get; set; } = false;

		public bool Likers { get; set; } = false;
	}
}
