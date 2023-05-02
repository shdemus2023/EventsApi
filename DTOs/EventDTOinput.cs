using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace EventsApi.DTOs
{
    public class EventDTOinput
    {
        public int? Id { get; set; }
        public string Name { get; set; }
        public string Tagline { get; set; }
        public DateTime? Schedule { get; set; }
        public string Description { get; set; }
        public int? Moderator { get; set; }
        public int? Category { get; set; }
        public int? Subcategory { get; set; }
        public int? RigorRank { get; set; }
        public ICollection<IFormFile> Images { get; set; }
    }
}
