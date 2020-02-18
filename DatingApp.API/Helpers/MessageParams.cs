using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DatingApp.API.Helpers
{
	public class MessageParams
	{
		/// <summary>
		/// This is options for our pagination that we're sending back to the client
		/// These options are passed through our url parameters from the client
		/// </summary>
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
		// We allow user to select which message container he wants to use, for example unread, inbox and outbox
		public string MessageContainer { get; set; } = "Unread"; // Show unread messages by default

	}
}