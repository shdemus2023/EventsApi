using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace EventsApi.Entities
{
    public class Image
    {
        public int Id { get; set; }
        public int EventId { get; set; }
        public string FileName { get; set; }
    }
}
