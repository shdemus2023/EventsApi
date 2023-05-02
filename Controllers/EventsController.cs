using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EventsApi.DAL;
using EventsApi.Entities;
using EventsApi.DTOs;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace EventsApi.Controllers
{
    [Route("api/v3/app/[controller]")]
    [ApiController]
    public class EventsController : ControllerBase
    {   
        private readonly EventsDAL _eventsDAL;

        public EventsController(EventsDAL eventsDAL)
        {
            _eventsDAL = eventsDAL;
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<Event>> GetById(int id)
        {
            try
            {
                EventDTO e = await _eventsDAL.GetEventById(id);
                
                return e==null ? BadRequest("Event not found") : Ok(e);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<EventDTO>>> GetPagedEvents(
                    [FromQuery(Name = "type")] string type,
                    [FromQuery(Name = "limit")] int limit,
                    [FromQuery(Name = "page")] int page )
        {
            
            if (type.ToLower() != "latest")
            {
                return BadRequest("Invalid type parameter. Only 'latest' is supported.");
            }

            int offset = (page - 1) * limit;

            List<EventDTO> events = await _eventsDAL.GetRecentEventsPaged(page, limit);

            return events;
        }



        [HttpPost]
        public async Task<ActionResult>  createEvent([FromForm] EventDTOinput eventDTOinput)
        {
            try
            {
                int id = await _eventsDAL.createEvent(eventDTOinput);

                if(id > 0)
                    return Ok(new { id = id, msg = "Event created" });
                else
                    return BadRequest("Cannot create Event");
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }


        [HttpPut("{id}")]
        public async Task<ActionResult> Edit(int id, [FromForm] EventDTOinput eventDTOinput)
        {
            eventDTOinput.Id = id;
            try
            {
                int n = await _eventsDAL.editEvent(eventDTOinput);

                if (n == 1)
                    return Ok(new { id = id, msg = "Event edited" });
                else if (n == 0)
                    return BadRequest(new { id = eventDTOinput.Id, msg = "Event NOT found" });
                else
                    return BadRequest(new { id = eventDTOinput.Id, msg = "Error while editing Event" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }

        }

        
        [HttpDelete("{id}")]        
        public async Task<ActionResult> Delete(int id)
        {
            try
            {
                int n = await _eventsDAL.deleteEvent(id);

                if ( n == 1)
                    return Ok(new { id = id, msg = "Event deleted" });
                else if( n == 0 )
                    return BadRequest(new { id = id, msg = "Event NOT found" });
                else
                    return BadRequest(new { id = id, msg = "Error while deleting Event" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }

        }
    }
}
