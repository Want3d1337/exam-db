namespace Cinema.DataProcessor
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Xml.Serialization;
    using Cinema.Data.Models;
    using Cinema.DataProcessor.ImportDto;
    using Data;
    using Newtonsoft.Json;

    public class Deserializer
    {

        private const string ErrorMessage = "Invalid data!";
        private const string SuccessfulImportMovie 
            = "Successfully imported {0} with genre {1} and rating {2}!";
        private const string SuccessfulImportHallSeat 
            = "Successfully imported {0}({1}) with {2} seats!";
        private const string SuccessfulImportProjection 
            = "Successfully imported projection {0} on {1}!";
        private const string SuccessfulImportCustomerTicket 
            = "Successfully imported customer {0} {1} with bought tickets: {2}!";

        public static string ImportMovies(CinemaContext context, string jsonString)
        {
            var MovieDtos = JsonConvert.DeserializeObject<ImportMovieDto[]>(jsonString);
            var movies = new List<Movie>();

            var sb = new StringBuilder();

            foreach(var dto in MovieDtos)
            {
                if (isValid(dto))
                {
                    var movie = new Movie
                    {
                        Title = dto.Title,
                        Genre = dto.Genre,
                        Duration = dto.Duration,
                        Rating = dto.Rating,
                        Director = dto.Director,
                    };

                    movies.Add(movie);
                    sb.AppendLine(string.Format(SuccessfulImportMovie, movie.Title, movie.Genre, $"{movie.Rating:f2}"));
                }
                else
                {
                    sb.AppendLine(ErrorMessage);
                }
            }

            context.Movies.AddRange(movies);
            context.SaveChanges();

            return sb.ToString().Trim();
        }

        public static string ImportHallSeats(CinemaContext context, string jsonString)
        {
            var HallSeatsDtos = JsonConvert.DeserializeObject<ImportHallSeatsDto[]>(jsonString);
            var sb = new StringBuilder();

            foreach(var dto in HallSeatsDtos)
            {
                if(isValid(dto))
                {
                    var hall = new Hall
                    {
                        Name = dto.Name,
                        Is4Dx = dto.Is4Dx,
                        Is3D = dto.Is3D,
                    };

                    context.Halls.Add(hall);
                    AddSeatsInDatabase(context, hall.Id, dto.Seats);
                    var projectionType = GetProjectionType(hall);
                    sb.AppendLine(string.Format(SuccessfulImportHallSeat, dto.Name, projectionType, dto.Seats));

                }
                else
                {
                    sb.AppendLine(ErrorMessage);
                }
            }

            context.SaveChanges();
            return sb.ToString().Trim();
        }


        public static string ImportProjections(CinemaContext context, string xmlString)
        {
            var serializer = new XmlSerializer(typeof(ImportProjectionDto[]), new XmlRootAttribute("Projections"));
            var ProjectionDtos = (ImportProjectionDto[])serializer.Deserialize(new StringReader(xmlString));


            var projections = new List<Projection>();

            StringBuilder sb = new StringBuilder();

            foreach(var dto in ProjectionDtos)
            {
                if (isValid(dto) && IsValidMovieId(context, dto.MovieId) && isValidHallId(context, dto.HallId))
                {
                    var projection = new Projection
                    {
                        MovieId = dto.MovieId,
                        HallId = dto.HallId,
                        DateTime = DateTime.ParseExact(
                            dto.DateTime,
                            "yyyy-MM-dd HH:mm:ss",
                            CultureInfo.InvariantCulture),
                    };

                    var movie = context.Movies.FirstOrDefault(a => a.Id == dto.MovieId);
                    var hall = context.Halls.FirstOrDefault(w => w.Id == dto.HallId);

                    projection.Movie = movie;
                    projection.Hall = hall;

                    var dateRes = projection.DateTime.ToString("MM/dd/yyyy", CultureInfo.InvariantCulture);

                    context.Projections.Add(projection);
                    sb.AppendLine(string.Format(SuccessfulImportProjection, projection.Movie.Title, dateRes));
                }
                else
                {
                    sb.AppendLine(ErrorMessage);
                }
            }
            context.SaveChanges();

            return sb.ToString().Trim();
        }


        public static string ImportCustomerTickets(CinemaContext context, string xmlString)
        {
            throw new NotImplementedException();
        }

        private static bool isValid(object obj)
        {
            var validator = new System.ComponentModel.DataAnnotations.ValidationContext(obj);
            var validationRes = new List<ValidationResult>();

            var res = Validator.TryValidateObject(obj, validator, validationRes, validateAllProperties: true);
            return res;
        }

        private static string GetProjectionType(Hall hall)
        {
            var res = "Normal";

            if(hall.Is4Dx && hall.Is3D)
            {
                res = "4Dx/3D";
            }
            else if(hall.Is3D)
            {
                res = "3D";
            }
            else if(hall.Is4Dx)
            {
                res = "4Dx";
            }

            return res;
        }

        private static void AddSeatsInDatabase(CinemaContext context, int HallId, int seatCount)
        {
            var seats = new List<Seat>();
            for(int i = 0; i < seatCount; i++)
            {
                seats.Add(new Seat { HallId = HallId });
            }

            context.AddRange(seats);
            context.SaveChanges();
        }
        private static bool isValidHallId(CinemaContext context, int hallId)
        {
            return context.Halls.Any(h => h.Id == hallId);
        }

        private static bool IsValidMovieId(CinemaContext context, int movieId)
        {
            return context.Movies.Any(m => m.Id == movieId);
        }
    }
}