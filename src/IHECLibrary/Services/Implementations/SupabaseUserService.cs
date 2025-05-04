using Supabase;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Postgrest.Attributes;
using Postgrest.Models;
using Postgrest.Responses;
using IHECLibrary.Services.Models; // Import the correct models namespace

namespace IHECLibrary.Services.Implementations
{
    public class SupabaseUserService : IUserService
    {
        private readonly Supabase.Client _supabaseClient;
        private readonly IAuthService _authService;

        public SupabaseUserService(Supabase.Client supabaseClient, IAuthService authService)
        {
            _supabaseClient = supabaseClient;
            _authService = authService;
        }

        public async Task<UserModel?> GetCurrentUserAsync()
        {
            try
            {
                Console.WriteLine("GetCurrentUserAsync: Starting user data fetch");
                
                // Check if supabase client is available
                if (_supabaseClient == null)
                {
                    Console.WriteLine("GetCurrentUserAsync: Supabase client is null");
                    return null;
                }
                
                // Check if auth is available and we have current user
                if (_supabaseClient.Auth == null)
                {
                    Console.WriteLine("GetCurrentUserAsync: Supabase Auth is null");
                    return null;
                }
                
                // Log auth status 
                var isAuthenticated = _authService.IsAuthenticated();
                Console.WriteLine($"GetCurrentUserAsync: Authentication status check: {isAuthenticated}");
                
                if (!isAuthenticated)
                {
                    Console.WriteLine("GetCurrentUserAsync: User is not authenticated");
                    return null;
                }

                var currentUser = _supabaseClient.Auth.CurrentUser;
                if (currentUser == null)
                {
                    Console.WriteLine("GetCurrentUserAsync: CurrentUser is null");
                    return null;
                }

                var userId = currentUser.Id;
                if (string.IsNullOrEmpty(userId))
                {
                    Console.WriteLine("GetCurrentUserAsync: CurrentUser.Id is null or empty");
                    return null;
                }

                Console.WriteLine($"GetCurrentUserAsync: Looking up user with ID: {userId}");
                
                // Try to create at least a minimal user model even if the database lookup fails
                var basicUser = new UserModel
                {
                    Id = userId,
                    Email = currentUser.Email ?? "",
                    FirstName = "",
                    LastName = ""
                };
                
                // Try to extract values from user metadata if available
                if (currentUser.UserMetadata != null)
                {
                    // Safely get first name
                    if (currentUser.UserMetadata.TryGetValue("first_name", out var firstNameObj) && firstNameObj != null)
                    {
                        basicUser.FirstName = firstNameObj.ToString() ?? "";
                    }
                    
                    // Safely get last name
                    if (currentUser.UserMetadata.TryGetValue("last_name", out var lastNameObj) && lastNameObj != null)
                    {
                        basicUser.LastName = lastNameObj.ToString() ?? "";
                    }
                }
                
                try
                {
                    // Try to get the full user info from database
                    return await GetUserByIdAsync(userId) ?? basicUser;
                }
                catch (Exception dbEx)
                {
                    Console.WriteLine($"GetCurrentUserAsync: Database lookup failed: {dbEx.Message}");
                    Console.WriteLine($"GetCurrentUserAsync: Returning basic user info instead");
                    return basicUser;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"GetCurrentUserAsync Exception: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                return null;
            }
        }

        public async Task<UserModel?> GetUserByIdAsync(string userId)
        {
            try
            {
                Console.WriteLine($"GetUserByIdAsync: Starting lookup for user with ID {userId}");

                // Try to get user data using standard ORM query
                var usersResponse = await _supabaseClient.From<DbUser>()
                    .Where(p => p.UserId == userId)
                    .Get();
                
                // Better handling of query results
                if (usersResponse.Models.Count == 0)
                {
                    Console.WriteLine($"GetUserByIdAsync: No user found with ID {userId} in database");
                    
                    // Try lookup by email instead
                    if (_supabaseClient.Auth.CurrentUser?.Email != null)
                    {
                        var emailQuery = await _supabaseClient.From<DbUser>()
                            .Where(p => p.Email == _supabaseClient.Auth.CurrentUser.Email)
                            .Get();
                        
                        if (emailQuery.Models.Count > 0)
                        {
                            var emailUser = emailQuery.Models.First();
                            Console.WriteLine($"GetUserByIdAsync: Found user by email: {emailUser.Email} with name: {emailUser.FirstName} {emailUser.LastName}");
                            
                            // Create user model from user found by email
                            var emailUserModel = new UserModel
                            {
                                Id = userId,
                                Email = emailUser.Email ?? "",
                                FirstName = emailUser.FirstName ?? "",
                                LastName = emailUser.LastName ?? "",
                                PhoneNumber = emailUser.PhoneNumber ?? "",
                                ProfilePictureUrl = emailUser.ProfilePictureUrl ?? "/Assets/default_profile.png",
                                LevelOfStudy = emailUser.LevelOfStudy ?? "",
                                FieldOfStudy = emailUser.FieldOfStudy ?? "",
                                IsStudent = emailUser.IsStudent
                            };
                            
                            return emailUserModel;
                        }
                    }
                    
                    // Failed to find user by either ID or email
                    Console.WriteLine($"GetUserByIdAsync: Failed to find user by ID or email");
                    return null;
                }
                
                // Get the first user from the response
                var user = usersResponse.Models.FirstOrDefault();
                
                if (user == null)
                {
                    Console.WriteLine($"GetUserByIdAsync: User is null despite query returning results");
                    return null;
                }

                // DEBUG: Log values to help diagnose the issue
                Console.WriteLine($"GetUserByIdAsync: Found user with these values:");
                Console.WriteLine($"  - UserId: {user.UserId}");
                Console.WriteLine($"  - Email: {user.Email}");
                Console.WriteLine($"  - FirstName: '{user.FirstName}'");
                Console.WriteLine($"  - LastName: '{user.LastName}'");
                Console.WriteLine($"  - Level of Study: '{user.LevelOfStudy}'");
                Console.WriteLine($"  - Field of Study: '{user.FieldOfStudy}'");
                Console.WriteLine($"  - Books Borrowed: {user.BooksBorrowed}");
                Console.WriteLine($"  - Books Reserved: {user.BooksReserved}");
                Console.WriteLine($"  - Ranking: '{user.Ranking}'");
                Console.WriteLine($"  - Is Student: {user.IsStudent}");

                // Create user model with available information directly from user table
                var userModel = new UserModel
                {
                    Id = user.UserId ?? "",
                    Email = user.Email ?? "",
                    FirstName = user.FirstName ?? "",
                    LastName = user.LastName ?? "",
                    PhoneNumber = user.PhoneNumber ?? "",
                    ProfilePictureUrl = !string.IsNullOrEmpty(user.ProfilePictureUrl) ? 
                        user.ProfilePictureUrl : 
                        "/Assets/default_profile.png",
                    LevelOfStudy = user.LevelOfStudy ?? "",
                    FieldOfStudy = user.FieldOfStudy ?? "",
                    IsStudent = user.IsStudent
                };
                
                try
                {
                    // Attempt to get admin profile (non-critical data)
                    var adminProfileResponse = await _supabaseClient.From<DbAdminProfile>()
                        .Where(p => p.AdminId == userId)
                        .Get();
                    
                    var adminProfile = adminProfileResponse.Models.FirstOrDefault();
                    userModel.IsAdmin = adminProfile != null && adminProfile.IsApproved;
                }
                catch (Exception ex)
                {
                    // Log but continue - this is non-critical data
                    Console.WriteLine($"GetUserByIdAsync: Error getting admin profile: {ex.Message}");
                }

                return userModel;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"GetUserByIdAsync: Exception: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                return null;
            }
        }

        // Helper method to create a mock profile when database access fails
        private UserProfileModel CreateMockProfile()
        {
            Console.WriteLine("Creating mock profile data as a fallback");
            string userId = _supabaseClient.Auth.CurrentUser?.Id ?? Guid.NewGuid().ToString();
            string email = _supabaseClient.Auth.CurrentUser?.Email ?? "default@example.com";
            
            string firstName = "Guest";
            string lastName = "User";
            
            // Safely extract metadata if available
            if (_supabaseClient.Auth.CurrentUser?.UserMetadata != null)
            {
                if (_supabaseClient.Auth.CurrentUser.UserMetadata.TryGetValue("first_name", out var fName) && fName != null)
                {
                    firstName = fName.ToString() ?? "Guest";
                }
                
                if (_supabaseClient.Auth.CurrentUser.UserMetadata.TryGetValue("last_name", out var lName) && lName != null)
                {
                    lastName = lName.ToString() ?? "User";
                }
            }
            
            return new UserProfileModel
            {
                UserId = Guid.Parse(userId),
                FirstName = firstName,
                LastName = lastName,
                Email = email,
                PhoneNumber = "Not available",
                ProfilePictureUrl = "/Assets/default_profile.png",
                LevelOfStudy = "Not specified",
                FieldOfStudy = "Not specified",
                BooksBorrowed = 0,
                BooksReserved = 0,
                Ranking = "Bronze",
                CreatedAt = DateTime.UtcNow,
                LastLogin = DateTime.UtcNow
            };
        }

        public async Task<UserProfileModel?> GetCurrentUserProfileAsync()
        {
            try
            {
                if (_supabaseClient.Auth.CurrentUser == null)
                {
                    Console.WriteLine("GetCurrentUserProfileAsync: CurrentUser is null");
                    return null;
                }

                var userId = _supabaseClient.Auth.CurrentUser.Id;
                if (string.IsNullOrEmpty(userId))
                {
                    Console.WriteLine("GetCurrentUserProfileAsync: CurrentUser.Id is null or empty");
                    return null;
                }

                Console.WriteLine($"GetCurrentUserProfileAsync: Looking up profile for user with ID: {userId}");
                
                // Get user information with all profile data now included in the users table
                try 
                {
                    var usersResponse = await _supabaseClient.From<DbUser>()
                        .Where(p => p.UserId == userId)
                        .Get();
                    
                    if (usersResponse.Models.Count == 0)
                    {
                        Console.WriteLine($"GetCurrentUserProfileAsync: No user found with ID {userId} in database");
                        return CreateMockProfile();
                    }
                    
                    var user = usersResponse.Models.First();
                    
                    Console.WriteLine($"GetCurrentUserProfileAsync: Found user profile data:");
                    Console.WriteLine($"  - Level of Study: '{user.LevelOfStudy}'");
                    Console.WriteLine($"  - Field of Study: '{user.FieldOfStudy}'");
                    Console.WriteLine($"  - Books Borrowed: {user.BooksBorrowed}");
                    Console.WriteLine($"  - Books Reserved: {user.BooksReserved}");
                    Console.WriteLine($"  - Ranking: '{user.Ranking}'");
                    
                    // Create the profile model with all user information from users table
                    var profileModel = new UserProfileModel
                    {
                        UserId = Guid.Parse(userId),
                        FirstName = user.FirstName ?? string.Empty,
                        LastName = user.LastName ?? string.Empty,
                        Email = user.Email ?? string.Empty,
                        PhoneNumber = user.PhoneNumber ?? string.Empty,
                        ProfilePictureUrl = user.ProfilePictureUrl ?? string.Empty,
                        LevelOfStudy = user.LevelOfStudy ?? string.Empty,
                        FieldOfStudy = user.FieldOfStudy ?? string.Empty,
                        BooksBorrowed = user.BooksBorrowed,
                        BooksReserved = user.BooksReserved,
                        Ranking = user.Ranking ?? "Bronze",
                        CreatedAt = user.CreatedAt ?? DateTime.UtcNow,
                        LastLogin = user.LastLogin ?? DateTime.UtcNow
                    };
                    
                    return profileModel;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"GetCurrentUserProfileAsync: Database error: {ex.Message}");
                    return CreateMockProfile();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"GetCurrentUserProfileAsync Exception: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                return null;
            }
        }

        // Helper method to capitalize first letter of a string
        private string CapitalizeFirstLetter(string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;
                
            return char.ToUpper(input[0]) + (input.Length > 1 ? input.Substring(1) : "");
        }

        public async Task<List<UserModel>> SearchUsersAsync(string searchQuery)
        {
            try
            {
                var users = await _supabaseClient.From<DbUser>()
                    .Where(p => (p.FirstName != null && p.FirstName.Contains(searchQuery)) || 
                                (p.LastName != null && p.LastName.Contains(searchQuery)))
                    .Get();

                var userModels = new List<UserModel>();
                foreach (var user in users.Models)
                {
                    if (user == null || user.UserId == null)
                        continue;
                    
                    // Vérifier si l'utilisateur est un administrateur
                    var adminProfile = await _supabaseClient.From<DbAdminProfile>()
                        .Where(p => p.AdminId == user.UserId)
                        .Single();

                    var userModel = new UserModel
                    {
                        Id = user.UserId ?? "",
                        Email = user.Email ?? "",
                        FirstName = user.FirstName ?? "",
                        LastName = user.LastName ?? "",
                        PhoneNumber = user.PhoneNumber ?? "",
                        ProfilePictureUrl = user.ProfilePictureUrl,
                        LevelOfStudy = user.LevelOfStudy ?? "",
                        FieldOfStudy = user.FieldOfStudy ?? "",
                        IsStudent = user.IsStudent,
                        IsAdmin = adminProfile != null && adminProfile.IsApproved
                    };

                    userModels.Add(userModel);
                }

                return userModels;
            }
            catch
            {
                return new List<UserModel>();
            }
        }

        public async Task<bool> UpdateUserProfileAsync(UserProfileUpdateModel model)
        {
            try
            {
                if (_supabaseClient.Auth.CurrentUser == null)
                    return false;

                var userId = _supabaseClient.Auth.CurrentUser.Id;
                if (string.IsNullOrEmpty(userId))
                    return false;
                
                // Get the current user data
                var user = await _supabaseClient.From<DbUser>()
                    .Where(p => p.UserId == userId)
                    .Single();
                
                if (user == null)
                    return false;
                
                // Update the properties
                user.FirstName = model.FirstName ?? "";
                user.LastName = model.LastName ?? "";
                user.PhoneNumber = model.PhoneNumber ?? "";
                
                // Now we can directly update the level and field of study as they're in the users table
                if (!string.IsNullOrEmpty(model.LevelOfStudy))
                    user.LevelOfStudy = model.LevelOfStudy;
                
                if (!string.IsNullOrEmpty(model.FieldOfStudy))
                    user.FieldOfStudy = model.FieldOfStudy;
                
                if (!string.IsNullOrEmpty(model.ProfilePictureUrl))
                    user.ProfilePictureUrl = model.ProfilePictureUrl;
                
                // Save the changes
                await _supabaseClient.From<DbUser>()
                    .Update(user);

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"UpdateUserProfileAsync Exception: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                return false;
            }
        }

        public async Task<string> GetUserRankingAsync(string userId)
        {
            try
            {
                // Obtenir le nombre de livres empruntés par l'utilisateur
                var borrowings = await _supabaseClient.From<DbBookBorrowing>()
                    .Where(b => b.UserId == userId)
                    .Get();

                int borrowedBooksCount = borrowings.Models.Count;

                // Déterminer le rang en fonction du nombre de livres empruntés
                if (borrowedBooksCount > 10)
                    return "Master";
                else if (borrowedBooksCount >= 5)
                    return "Gold";
                else if (borrowedBooksCount >= 2)
                    return "Silver";
                else
                    return "Bronze";
            }
            catch
            {
                return "Bronze"; // Rang par défaut en cas d'erreur
            }
        }

        public async Task<UserStatisticsModel> GetUserStatisticsAsync(string userId)
        {
            try
            {
                var statistics = new UserStatisticsModel();
                
                // Get user data to retrieve books_borrowed, books_reserved, and ranking
                var user = await _supabaseClient.From<DbUser>()
                    .Where(u => u.UserId == userId)
                    .Single();
                
                if (user != null)
                {
                    // Use the values directly from the users table
                    statistics.BorrowedBooksCount = user.BooksBorrowed;
                    statistics.ReservedBooksCount = user.BooksReserved;
                    statistics.Ranking = user.Ranking ?? "Bronze";
                }

                // Obtenir les emprunts actifs
                var activeBorrowings = await _supabaseClient.From<DbBookBorrowing>()
                    .Where(b => b.UserId == userId && !b.IsReturned)
                    .Get();

                // Obtenir les livres d'intérêt (réservations)
                var booksOfInterest = await _supabaseClient.From<DbBookOfInterest>()
                    .Where(r => r.UserId == userId)
                    .Get();

                // Obtenir les livres aimés
                var likedBooks = await _supabaseClient.From<DbBookLike>()
                    .Where(l => l.UserId == userId)
                    .Get();

                statistics.LikedBooksCount = likedBooks.Models.Count;
                
                // Make sure the count matches the active borrowings
                if (statistics.BorrowedBooksCount != activeBorrowings.Models.Count)
                {
                    Console.WriteLine($"Warning: User has {activeBorrowings.Models.Count} active borrowings but books_borrowed is {statistics.BorrowedBooksCount}");
                    statistics.BorrowedBooksCount = activeBorrowings.Models.Count;
                }
                
                // Make sure the count matches the books of interest
                if (statistics.ReservedBooksCount != booksOfInterest.Models.Count)
                {
                    Console.WriteLine($"Warning: User has {booksOfInterest.Models.Count} books of interest but books_reserved is {statistics.ReservedBooksCount}");
                    statistics.ReservedBooksCount = booksOfInterest.Models.Count;
                }

                // Récupérer les détails des livres empruntés
                foreach (var borrowing in activeBorrowings.Models)
                {
                    if (borrowing?.BookId == null)
                        continue;

                    var book = await _supabaseClient.From<DbBook>()
                        .Where(b => b.BookId == borrowing.BookId)
                        .Single();

                    if (book != null)
                    {
                        statistics.BorrowedBooks.Add(new BookModel
                        {
                            Id = book.BookId ?? "",
                            Title = book.Title ?? "",
                            Author = book.Author ?? "",
                            ISBN = book.ISBN ?? "",
                            PublicationYear = book.PublicationYear,
                            Publisher = book.Publisher ?? "",
                            Category = book.Category ?? "",
                            Description = book.Description ?? "",
                            CoverImageUrl = "", 
                            AvailableCopies = 0,
                            TotalCopies = 0,
                            LikesCount = 0
                        });
                    }
                }

                // Récupérer les détails des livres d'intérêt
                foreach (var bookOfInterest in booksOfInterest.Models)
                {
                    if (bookOfInterest?.BookId == null)
                        continue;

                    var book = await _supabaseClient.From<DbBook>()
                        .Where(b => b.BookId == bookOfInterest.BookId)
                        .Single();

                    if (book != null)
                    {
                        statistics.ReservedBooks.Add(new BookModel
                        {
                            Id = book.BookId ?? "",
                            Title = book.Title ?? "",
                            Author = book.Author ?? "",
                            ISBN = book.ISBN ?? "",
                            PublicationYear = book.PublicationYear,
                            Publisher = book.Publisher ?? "",
                            Category = book.Category ?? "",
                            Description = book.Description ?? "",
                            CoverImageUrl = "",
                            AvailableCopies = 0,
                            TotalCopies = 0,
                            LikesCount = 0
                        });
                    }
                }

                // Récupérer les détails des livres aimés
                foreach (var like in likedBooks.Models)
                {
                    if (like?.BookId == null)
                        continue;

                    var book = await _supabaseClient.From<DbBook>()
                        .Where(b => b.BookId == like.BookId)
                        .Single();

                    if (book != null)
                    {
                        statistics.LikedBooks.Add(new BookModel
                        {
                            Id = book.BookId ?? "",
                            Title = book.Title ?? "",
                            Author = book.Author ?? "",
                            ISBN = book.ISBN ?? "",
                            PublicationYear = book.PublicationYear,
                            Publisher = book.Publisher ?? "",
                            Category = book.Category ?? "",
                            Description = book.Description ?? "",
                            CoverImageUrl = "",
                            AvailableCopies = 0,
                            TotalCopies = 0,
                            LikesCount = 0
                        });
                    }
                }

                return statistics;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"GetUserStatisticsAsync Exception: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                return new UserStatisticsModel
                {
                    Ranking = "Bronze" // Rang par défaut en cas d'erreur
                };
            }
        }

        // Method to ensure a test user exists in the database for testing
        public async Task<bool> EnsureTestUserExistsAsync()
        {
            try
            {
                // Check if the test user already exists in the database
                var testUserId = _supabaseClient.Auth.CurrentUser?.Id;
                if (string.IsNullOrEmpty(testUserId))
                {
                    Console.WriteLine("EnsureTestUserExistsAsync: No current user");
                    return false;
                }
                
                Console.WriteLine($"EnsureTestUserExistsAsync: Checking if user with ID {testUserId} exists");
                
                var usersResponse = await _supabaseClient.From<DbUser>()
                    .Where(p => p.UserId == testUserId)
                    .Get();
                
                if (usersResponse.Models.Count > 0)
                {
                    Console.WriteLine("EnsureTestUserExistsAsync: Test user already exists in database");
                    return true;
                }
                
                // Create a new test user entry in the database
                var testUser = new DbUser
                {
                    UserId = testUserId,
                    Email = _supabaseClient.Auth.CurrentUser?.Email ?? "test@example.com",
                    FirstName = "Test",
                    LastName = "User",
                    PhoneNumber = "0123456789",
                    LevelOfStudy = "Master 1",
                    FieldOfStudy = "Computer Science",
                    ProfilePictureUrl = "/Assets/default_profile.png",
                    IsStudent = true,
                    BooksBorrowed = 3,
                    BooksReserved = 1,
                    Ranking = "Silver",
                    CreatedAt = DateTime.UtcNow,
                    LastLogin = DateTime.UtcNow
                };
                
                // Insert the test user into the database
                var response = await _supabaseClient.From<DbUser>().Insert(testUser);
                Console.WriteLine($"EnsureTestUserExistsAsync: Created test user in database. Response status: {response.ResponseMessage?.StatusCode}");
                
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"EnsureTestUserExistsAsync Exception: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                return false;
            }
        }
    }

    // Only keep the user-specific model classes here
    // All book-related database models are now defined in DatabaseModels.cs

    [Table("users")]
    public class DbUser : BaseModel
    {
        [PrimaryKey("user_id")]
        public string? UserId { get; set; }

        [Column("email")]
        public string? Email { get; set; }

        [Column("password_hash")]
        public string? PasswordHash { get; set; }

        [Column("first_name")]
        public string? FirstName { get; set; }

        [Column("last_name")]
        public string? LastName { get; set; }

        [Column("phone_number")]
        public string? PhoneNumber { get; set; }

        [Column("profile_picture_url")]
        public string? ProfilePictureUrl { get; set; }
        
        [Column("created_at")]
        public DateTime? CreatedAt { get; set; }
        
        [Column("last_login")]
        public DateTime? LastLogin { get; set; }
        
        [Column("is_active")]
        public bool IsActive { get; set; }
        
        [Column("level_of_study")]
        public string? LevelOfStudy { get; set; }
        
        [Column("field_of_study")]
        public string? FieldOfStudy { get; set; }
        
        [Column("books_borrowed")]
        public int BooksBorrowed { get; set; }
        
        [Column("books_reserved")]
        public int BooksReserved { get; set; }
        
        [Column("ranking")]
        public string? Ranking { get; set; }
        
        [Column("is_student")]
        public bool IsStudent { get; set; }
    }

    [Table("student_profiles")]
    public class DbStudentProfile : BaseModel
    {
        [PrimaryKey("student_id")]
        public string? StudentId { get; set; }

        [Column("level_of_study")]
        public string? LevelOfStudy { get; set; }

        [Column("field_of_study")]
        public string? FieldOfStudy { get; set; }
        
        [Column("books_borrowed")]
        public int BooksBorrowed { get; set; }
        
        [Column("books_reserved")]
        public int BooksReserved { get; set; }
        
        [Column("ranking")]
        public string? Ranking { get; set; }
    }

    [Table("admin_profiles")]
    public class DbAdminProfile : BaseModel
    {
        [PrimaryKey("admin_id")]
        public string? AdminId { get; set; }

        [Column("job_title")]
        public string? JobTitle { get; set; }

        [Column("is_approved")]
        public bool IsApproved { get; set; }

        [Column("approved_by")]
        public string? ApprovedBy { get; set; }
    }
}
