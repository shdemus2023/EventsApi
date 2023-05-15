using EventsApi.DTOs;
using EventsApi.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web;



namespace EventsApi.DAL
{
    public class EventsDAL
    {
        private readonly string _connectionString;

        public EventsDAL(string connectionString)
        {
            _connectionString = connectionString;
        }


        public async Task<EventDTO> GetEventById(int id)
        {
            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                connection.ConnectionString += ";MultipleActiveResultSets=True";

                await connection.OpenAsync();
                string query = "SELECT * FROM Events WHERE Id = @Id";
                using (SqlCommand command = new SqlCommand(query, connection))
                {
                    EventDTO eventDTO = null;
                    command.Parameters.AddWithValue("@Id", id);
                    using (SqlDataReader reader = await command.ExecuteReaderAsync())
                    {
                        if (reader.Read())
                        {
                            eventDTO = new EventDTO
                            {
                                Id = reader["id"] != DBNull.Value ? Convert.ToInt32(reader["id"]) : null,
                                Name = reader["name"] != DBNull.Value ? Convert.ToString(reader["name"]) : null,
                                UId = reader["uid"] != DBNull.Value ? Convert.ToInt32(reader["uid"]) : null,
                                Type = reader["type"] != DBNull.Value ? Convert.ToString(reader["type"]) : null,
                                Tagline = reader["Tagline"] != DBNull.Value ? Convert.ToString(reader["Tagline"]) : null,
                                Schedule = reader["schedule"] != DBNull.Value ? Convert.ToDateTime(reader["schedule"]) : (DateTime?)null,
                                Description = reader["description"] != DBNull.Value ? Convert.ToString(reader["description"]) : null,
                                Moderator = reader["moderator"] != DBNull.Value ? Convert.ToInt32(reader["moderator"]) : null,
                                Category = reader["category"] != DBNull.Value ? Convert.ToInt32(reader["category"]) : null,
                                Subcategory = reader["subcategory"] != DBNull.Value ? Convert.ToInt32(reader["subcategory"]) : null,
                                RigorRank = reader["rigorRank"] != DBNull.Value ? Convert.ToInt32(reader["rigorRank"]) : null
                            };
                                                        
                            eventDTO.Images = getEventImages(connection, id);
                            return eventDTO;
                        }
                        else
                        {
                            return null;
                        }
                    }
                    //eventDTO.Images = getEventImages(connection, id);
                    //return eventDTO;
                }
            }
        }

        private List<Image> getEventImages(SqlConnection connection, int eventId)
        {
            List<Image> Images = new();
            string getImgQuery = "SELECT FileName FROM Images WHERE EventId = @eventId";
            SqlCommand command = new SqlCommand(getImgQuery, connection);

            command.CommandText = getImgQuery;
            command.Parameters.AddWithValue("@eventId", eventId);

            using (SqlDataReader reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    string filename = reader.GetString(0);
                    Images.Add( new Image { FileName = filename });
                }
            }
            return Images;
        }


        public async Task<int> createEvent(EventDTOinput eventDTOinput)
        {
            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                // Create a transaction
                var transaction = connection.BeginTransaction();

                try
                {
                    checkImagesValid(eventDTOinput.Images);

                    string query = @"INSERT INTO [Events]
                                           ([name]
                                           ,[uid]
                                           ,[type]
                                           ,[tagline]
                                           ,[schedule]
                                           ,[description]
                                           ,[moderator]
                                           ,[category]
                                           ,[subcategory]
                                           ,[rigorrank])
                                     VALUES
                                           (@name
                                           ,@uid
                                           ,@type
                                           ,@tagline
                                           ,@schedule
                                           ,@description
                                           ,@moderator
                                           ,@category
                                           ,@subcategory
                                           ,@rigorrank); SELECT SCOPE_IDENTITY();";



                    using (SqlCommand command = new SqlCommand(query, connection, transaction))
                    {
                        // 1. first insert Event details in db

                        command.Parameters.AddWithValue("@name", eventDTOinput.Name);
                        command.Parameters.AddWithValue("@uid", 18);
                        command.Parameters.AddWithValue("@type", "event");

                        command.Parameters.AddWithValue("@tagline", (object)eventDTOinput.Tagline ?? DBNull.Value);
                        command.Parameters.AddWithValue("@schedule", (object)eventDTOinput.Schedule ?? DBNull.Value);
                        command.Parameters.AddWithValue("@description", (object)eventDTOinput.Description ?? DBNull.Value);
                        command.Parameters.AddWithValue("@moderator", (object)eventDTOinput.Moderator ?? DBNull.Value);
                        command.Parameters.AddWithValue("@category", (object)eventDTOinput.Category ?? DBNull.Value);
                        command.Parameters.AddWithValue("@subcategory", (object)eventDTOinput.Subcategory ?? DBNull.Value);
                        command.Parameters.AddWithValue("@rigorRank", (object)eventDTOinput.RigorRank ?? DBNull.Value);

                        object result = await command.ExecuteScalarAsync();
                        int id = -1;

                        if (result == null)     // if cannot insert new event
                            return -1;
                        else
                            id = Convert.ToInt32(result.ToString());  // get id of newly inserted event

                        if (eventDTOinput.Images != null)
                        {

                            foreach (var image in eventDTOinput.Images)
                            {
                                // 2. now insert all Images to db

                                string fileName = await saveImage(image);

                                var cmd = new SqlCommand(
                                    @"INSERT INTO [Images]
                                    ([EventId]
                                    ,[FileName])
                                VALUES
                                    (@EventId
                                    ,@FileName)"
                                    , connection, transaction
                                );

                                cmd.Parameters.AddWithValue("@EventId", id);
                                cmd.Parameters.AddWithValue("@FileName", fileName);

                                await cmd.ExecuteNonQueryAsync();
                            }
                        }

                        transaction.Commit();
                        return id;
                    }
                }
                catch (ArgumentException ex)
                {
                    transaction.Rollback();
                    throw new ArgumentException(ex.Message);
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    throw new Exception(ex.Message);
                }
            }

        }


        private async Task<string> saveImage(IFormFile image)
        {
            try
            {
                string _imagesFolderPath = Path.Combine(
                       Directory.GetCurrentDirectory(), "UploadedImages");

                string uniqueId = Guid.NewGuid().ToString();
                var fileExtension = Path.GetExtension(image.FileName);
                var fileName = $"{uniqueId}{fileExtension}";
                string filePath = Path.Combine(_imagesFolderPath, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await image.CopyToAsync(stream);                    
                }

                return fileName;
            }
            catch (Exception)
            {
                throw;
            }
        }
        private void checkImagesValid(ICollection<IFormFile> images)
        {
            if (images == null)
                return;

            var invalidImages = new List<string>();
            
            foreach (var image in images)
            {
                var ext = Path.GetExtension(image.FileName).ToLower();

                if (ext != ".jpg" && ext != ".jpeg" && ext != ".gif" && ext != ".png")
                {
                    invalidImages.Add(image.FileName);
                }
            }

            if (invalidImages.Any())
            {
                throw new ArgumentException($"Invalid image files: {string.Join(",", invalidImages)}");
            }
            // method executes successfully if images are valid
            // otherwise exception is thrown
        }

        private void deleteEventImages(int eventId)
        {
            List<string> filenames = new List<string>();

            try
            {
                using (SqlConnection connection = new SqlConnection(_connectionString))
                {
                    connection.Open();

                    string query = "SELECT FileName FROM Images WHERE EventId = @eventId";

                    using (SqlCommand command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@eventId", eventId);

                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                string filename = reader.GetString(0);
                                filenames.Add(filename);
                            }                            
                        }
                    }                    
                }

                foreach (var file in filenames)
                {
                    deleteImage(file);
                }
            }
            catch (Exception ex)
            {
                // handle exception
            }
        }

        private void deleteImage(string fileName)
        {
            string _imagesFolderPath = Path.Combine(
                       Directory.GetCurrentDirectory(), "UploadedImages");

            string filePath = Path.Combine(_imagesFolderPath, fileName);
                        
            try
            {
                // Check if the file exists
                if (File.Exists(filePath))
                {
                    // Delete the file
                    File.Delete(filePath);                 
                }                
            }
            catch (Exception ex)
            {   
                throw new Exception($"An error occurred while deleting the file: {ex.Message}");
            }
        }

        public async Task<int> editEvent(EventDTOinput eventDTOinput)
        {

            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                // Create a transaction
                var transaction = connection.BeginTransaction();

                try
                {
                    checkImagesValid(eventDTOinput.Images);

                    // Check if the event exists in the Events table
                    string checkEventQuery = "SELECT COUNT(*) FROM [Events] WHERE [Id] = @eventId";
                    using (SqlCommand checkEventCmd = new SqlCommand(checkEventQuery, connection, transaction))
                    {
                        checkEventCmd.Parameters.AddWithValue("@eventId", eventDTOinput.Id);
                        int eventCount = (int)await checkEventCmd.ExecuteScalarAsync();

                        if (eventCount == 0) // Event doesn't exist
                        {
                            return 0;
                        }
                    }

                    string query = @"UPDATE [Events]
                                 SET [name] = @name,
                                     [tagline] = @tagline,
                                     [schedule] = @schedule,
                                     [description] = @description,
                                     [moderator] = @moderator,
                                     [category] = @category,
                                     [subcategory] = @subcategory,
                                     [rigorrank] = @rigorrank
                                 WHERE [id] = @id";


                    int rowsEdited = -1;


                    using (SqlCommand command = new SqlCommand(query, connection, transaction))
                    {
                        command.Parameters.AddWithValue("@name", eventDTOinput.Name);
                        command.Parameters.AddWithValue("@tagline", (object)eventDTOinput.Tagline ?? DBNull.Value);
                        command.Parameters.AddWithValue("@schedule", (object)eventDTOinput.Schedule ?? DBNull.Value);
                        command.Parameters.AddWithValue("@description", (object)eventDTOinput.Description ?? DBNull.Value);
                        command.Parameters.AddWithValue("@moderator", (object)eventDTOinput.Moderator ?? DBNull.Value);
                        command.Parameters.AddWithValue("@category", (object)eventDTOinput.Category ?? DBNull.Value);
                        command.Parameters.AddWithValue("@subcategory", (object)eventDTOinput.Subcategory ?? DBNull.Value);
                        command.Parameters.AddWithValue("@rigorRank", (object)eventDTOinput.RigorRank ?? DBNull.Value);
                        command.Parameters.AddWithValue("@id", eventDTOinput.Id);

                        rowsEdited = await command.ExecuteNonQueryAsync();

                        if (rowsEdited == 0)     // if cannot update existing event
                            return -1;
                    }

                    // delete all existing images for this event
                    //var deleteImagesCmd = new SqlCommand(
                    //    @"DELETE FROM [Images]
                    //      WHERE [EventId] = @eventId"
                    //            , connection, transaction
                    //        );
                    //deleteImagesCmd.Parameters.AddWithValue("@eventId", eventDTOinput.Id);
                    //await deleteImagesCmd.ExecuteNonQueryAsync();

                    
                    if (eventDTOinput.Images != null)
                    {
                        foreach (var image in eventDTOinput.Images)
                        {
                            // 2. now insert all Images to db

                            string fileName = await saveImage(image);

                            var cmd = new SqlCommand(
                                @"INSERT INTO [Images]
                                    ([EventId]
                                    ,[FileName])
                                VALUES
                                    (@EventId
                                    ,@FileName)"
                                , connection, transaction
                            );

                            cmd.Parameters.AddWithValue("@EventId", eventDTOinput.Id);
                            cmd.Parameters.AddWithValue("@FileName", fileName);

                            await cmd.ExecuteNonQueryAsync();
                        }
                    }

                    transaction.Commit();
                    return rowsEdited;
                }
                catch (ArgumentException ex)
                {
                    transaction.Rollback();
                    throw new ArgumentException(ex.Message);
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    throw new Exception(ex.Message);
                }
            }

        }



        public async Task<int> deleteEvent(int eventId)
        {
            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                // Check if the event exists in the Events table
                string checkEventQuery = "SELECT COUNT(*) FROM [Events] WHERE [Id] = @eventId";
                using (SqlCommand checkEventCmd = new SqlCommand(checkEventQuery, connection))
                {
                    checkEventCmd.Parameters.AddWithValue("@eventId", eventId);
                    int eventCount = (int)await checkEventCmd.ExecuteScalarAsync();

                    if (eventCount == 0) // Event doesn't exist
                    {
                        return 0;
                    }
                }

                string query = @"DELETE FROM [Events] WHERE [Id] = @eventId";

                // Create a transaction
                var transaction = connection.BeginTransaction();

                try
                {
                    using (SqlCommand command = new SqlCommand(query, connection, transaction))
                    {
                        command.Parameters.AddWithValue("@eventId", eventId);

                        int count = await command.ExecuteNonQueryAsync();

                        // Check if event was successfully deleted
                        if (count == 1)
                        {
                            // Delete all images associated with the event

                            deleteEventImages(eventId); // delete files

                            var cmd = new SqlCommand(
                                @"DELETE FROM [Images] WHERE [EventId] = @eventId",
                                connection, transaction
                            );
                            cmd.Parameters.AddWithValue("@eventId", eventId);
                            
                            // delete from table
                            object result = await cmd.ExecuteNonQueryAsync();
                            
                            transaction.Commit();
                            
                            return count;
                        }
                        else
                        {
                            transaction.Rollback();
                            return -1; // if cannot delete event
                        }
                    }
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    throw new Exception(ex.Message);
                }                
            }            
        }


        public async Task<List<EventDTO>> GetRecentEventsPaged(int pageNumber, int pageSize)
        {
            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                connection.ConnectionString += ";MultipleActiveResultSets=True";

                await connection.OpenAsync();

                string query = @"SELECT * 
                                 FROM [Events]
                                 ORDER BY [schedule] DESC, [id] DESC
                                 OFFSET @Offset ROWS 
                                 FETCH NEXT @PageSize ROWS ONLY";

                int offset = (pageNumber - 1) * pageSize;

                using (SqlCommand command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@PageSize", pageSize);
                    command.Parameters.AddWithValue("@Offset", offset);

                    using (SqlDataReader reader = await command.ExecuteReaderAsync())
                    {
                        List<EventDTO> events = new List<EventDTO>();

                        while (await reader.ReadAsync())
                        {
                            EventDTO eventDTO = new EventDTO
                            {
                                Id = reader["id"] != DBNull.Value ? Convert.ToInt32(reader["id"]) : null,
                                Name = reader["name"] != DBNull.Value ? Convert.ToString(reader["name"]) : null,
                                UId = reader["uid"] != DBNull.Value ? Convert.ToInt32(reader["uid"]) : null,
                                Type = reader["type"] != DBNull.Value ? Convert.ToString(reader["type"]) : null,
                                Tagline = reader["Tagline"] != DBNull.Value ? Convert.ToString(reader["Tagline"]) : null,
                                Schedule = reader["schedule"] != DBNull.Value ? Convert.ToDateTime(reader["schedule"]) : (DateTime?)null,
                                Description = reader["description"] != DBNull.Value ? Convert.ToString(reader["description"]) : null,
                                Moderator = reader["moderator"] != DBNull.Value ? Convert.ToInt32(reader["moderator"]) : null,
                                Category = reader["category"] != DBNull.Value ? Convert.ToInt32(reader["category"]) : null,
                                Subcategory = reader["subcategory"] != DBNull.Value ? Convert.ToInt32(reader["subcategory"]) : null,
                                RigorRank = reader["rigorRank"] != DBNull.Value ? Convert.ToInt32(reader["rigorRank"]) : null
                            };
                            
                            eventDTO.Images = getEventImages(connection, (int)eventDTO.Id);
                            events.Add(eventDTO);
                        }

                        return events;
                    }
                }
            }
        }

    }
}
