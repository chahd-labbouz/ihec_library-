using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IHECLibrary.Services;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using IHECLibrary; // Added import for BookModel
using Supabase;

namespace IHECLibrary.ViewModels
{
    public partial class ProfileViewModel : ViewModelBase
    {
        // User properties
        [ObservableProperty]
        private string _userFullName = string.Empty; // Remove hardcoded name

        [ObservableProperty]
        private string _userEmail = string.Empty;

        [ObservableProperty]
        private string _userPhone = string.Empty;

        [ObservableProperty]
        private string _userLevel = string.Empty;

        [ObservableProperty]
        private string _userField = string.Empty;

        [ObservableProperty]
        private string _userProfilePicture = "/Assets/default_profile.png"; // Default profile picture

        [ObservableProperty]
        private string _searchQuery = string.Empty;

        [ObservableProperty]
        private int _borrowedBooksCount = 0;

        [ObservableProperty]
        private int _reservedBooksCount = 0;

        [ObservableProperty]
        private int _likedBooksCount = 0;

        [ObservableProperty]
        private ObservableCollection<BorrowedBookViewModel> _borrowedBooks = new();

        [ObservableProperty]
        private ObservableCollection<ReservedBookViewModel> _reservedBooks = new();

        [ObservableProperty]
        private bool _isLoading = false;

        [ObservableProperty]
        private bool _hasError = false;

        [ObservableProperty] 
        private string _errorMessage = string.Empty;

        private readonly INavigationService _navigationService;
        private readonly IUserService _userService;
        private readonly IBookService _bookService;
        private readonly IAuthService _authService;
        private readonly Supabase.Client _supabaseClient;

        public ProfileViewModel(
            INavigationService navigationService, 
            IUserService userService, 
            IBookService bookService, 
            IAuthService authService,
            Supabase.Client supabaseClient)
        {
            _navigationService = navigationService;
            _userService = userService;
            _bookService = bookService;
            _authService = authService;
            _supabaseClient = supabaseClient;

            // Load user data immediately
            LoadUserDataAsync();
        }

        private async void LoadUserDataAsync()
        {
            try
            {
                IsLoading = true;
                HasError = false;
                ErrorMessage = string.Empty;

                Console.WriteLine("ProfileViewModel: Loading user data...");
                
                // Check if user is authenticated
                bool isAuthenticated = false;
                try
                {
                    isAuthenticated = _authService.IsAuthenticated();
                    Console.WriteLine($"ProfileViewModel: Authentication status: {isAuthenticated}");
                }
                catch (Exception authEx)
                {
                    Console.WriteLine($"ProfileViewModel: Error checking authentication: {authEx.Message}");
                    HasError = true;
                    ErrorMessage = "Unable to verify authentication status. Please try signing in again.";
                    return;
                }
                
                if (!isAuthenticated)
                {
                    // Only show error if user is not authenticated
                    HasError = true;
                    ErrorMessage = "Please sign in to view your profile.";
                    Console.WriteLine("ProfileViewModel: User not authenticated - showing error message");
                    
                    // Try to redirect to login page
                    try 
                    {
                        await _navigationService.NavigateToAsync("Login");
                        return;
                    }
                    catch {}
                    
                    return;
                }
                
                // Try to ensure test user exists in database - this helps when the database is empty
                try
                {
                    // Check if we can access the EnsureTestUserExistsAsync method on the user service
                    if (_userService is Services.Implementations.SupabaseUserService supabaseUserService)
                    {
                        await supabaseUserService.EnsureTestUserExistsAsync();
                        Console.WriteLine("ProfileViewModel: Test user ensured in database");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"ProfileViewModel: Error ensuring test user: {ex.Message}");
                    // Don't show error, just continue
                }
                
                // Try to get user data from the user service
                try
                {
                    var user = await _userService.GetCurrentUserAsync();
                    
                    if (user == null)
                    {
                        // Show error if user data is null
                        HasError = true;
                        ErrorMessage = "Unable to load your profile data. Please try logging in again.";
                        Console.WriteLine("ProfileViewModel: User is authenticated but user data is null. Showing error message.");
                        return;
                    }
                    
                    // Set the user profile data
                    UserFullName = $"{user.FirstName} {user.LastName}";
                    UserEmail = user.Email;
                    UserPhone = user.PhoneNumber ?? "No phone number";
                    UserLevel = FormatStudyLevel(user.LevelOfStudy ?? "Unknown");
                    UserField = user.FieldOfStudy ?? "Not specified";
                    
                    // Set profile picture
                    if (!string.IsNullOrEmpty(user.ProfilePictureUrl))
                    {
                        UserProfilePicture = user.ProfilePictureUrl;
                    }
                    
                    Console.WriteLine($"ProfileViewModel: Successfully loaded user basic data: {UserFullName}");
                }
                catch (Exception userEx)
                {
                    Console.WriteLine($"ProfileViewModel: Error loading user data: {userEx.Message}");
                    HasError = true;
                    ErrorMessage = "Error loading profile data. Please try again.";
                    return;
                }

                // Try to get user statistics
                try
                {
                    // Continue loading other user data like statistics
                    // For now just use default values to avoid showing errors
                    // We'll have incomplete data but at least the page will display
                    BorrowedBooksCount = 0;
                    ReservedBooksCount = 0;
                    LikedBooksCount = 0;
                    
                    // Try to get actual statistics if available
                    try
                    {
                        var statistics = await _userService.GetUserStatisticsAsync(_supabaseClient.Auth.CurrentUser?.Id ?? "");
                        
                        // Update statistics if available
                        if (statistics != null)
                        {
                            BorrowedBooksCount = statistics.BorrowedBooks.Count;
                            ReservedBooksCount = statistics.ReservedBooks.Count;
                            LikedBooksCount = statistics.LikedBooks.Count;
                            
                            Console.WriteLine($"ProfileViewModel: Loaded statistics - Borrowed: {BorrowedBooksCount}, Reserved: {ReservedBooksCount}, Liked: {LikedBooksCount}");
                            
                            // Load borrowed books
                            BorrowedBooks.Clear();
                            foreach (var book in statistics.BorrowedBooks)
                            {
                                BorrowedBooks.Add(new BorrowedBookViewModel(book, _bookService, this));
                            }
                            Console.WriteLine($"ProfileViewModel: Loaded {BorrowedBooks.Count} borrowed books");
                            
                            // Load reserved books
                            ReservedBooks.Clear();
                            foreach (var book in statistics.ReservedBooks)
                            {
                                ReservedBooks.Add(new ReservedBookViewModel(book, _bookService, this));
                            }
                            Console.WriteLine($"ProfileViewModel: Loaded {ReservedBooks.Count} reserved books");
                        }
                    }
                    catch (Exception statsEx)
                    {
                        // Log but don't fail completely if statistics can't be loaded
                        Console.WriteLine($"ProfileViewModel: Error loading statistics: {statsEx.Message}");
                    }
                }
                catch (Exception ex)
                {
                    // Log the exception but don't show an error since we already have basic user data
                    Console.WriteLine($"ProfileViewModel: Error loading extended profile data: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                // Log the exception and show it to the user
                Console.WriteLine($"ProfileViewModel: Error loading profile data: {ex.Message}");
                HasError = true;
                ErrorMessage = $"Error loading profile data: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }
        
        // Helper method to format study levels if needed
        private string FormatStudyLevel(string level)
        {
            return level switch
            {
                "1" => "1st Year",
                "2" => "2nd Year",
                "3" => "3rd Year",
                "M1" => "Master 1",
                "M2" => "Master 2",
                _ => level  // Keep the original value if it doesn't match any case
            };
        }

        // Refresh the profile data
        [RelayCommand]
        private void RefreshProfile()
        {
            LoadUserDataAsync();
        }
        
        // Navigation commands remain unchanged
        [RelayCommand]
        private async Task NavigateToHome()
        {
            await _navigationService.NavigateToAsync("Home");
        }

        [RelayCommand]
        private async Task NavigateToLibrary()
        {
            await _navigationService.NavigateToAsync("Library");
        }

        [RelayCommand]
        private async Task NavigateToChatbot()
        {
            await _navigationService.NavigateToAsync("Chatbot");
        }

        [RelayCommand]
        private Task NavigateToProfile()
        {
            // We're already on the profile page, just refresh the data
            LoadUserDataAsync();
            return Task.CompletedTask;
        }

        [RelayCommand]
        private async Task EditProfile()
        {
            await _navigationService.NavigateToAsync("EditProfile");
        }

        // Add this method to allow the navigation service to force a refresh when needed
        public void RefreshData()
        {
            System.Diagnostics.Debug.WriteLine("ProfileViewModel: RefreshData called - reloading user data");
            LoadUserDataAsync();
        }

        [RelayCommand]
        private async Task SignOut()
        {
            try
            {
                Console.WriteLine("ProfileViewModel: User attempting to sign out");
                IsLoading = true;
                
                // Call the auth service to sign out
                bool result = await _authService.SignOutAsync();
                
                if (result)
                {
                    // Navigate to login page on successful sign out
                    Console.WriteLine("ProfileViewModel: Sign out successful, navigating to login");
                    await _navigationService.NavigateToAsync("Login");
                }
                else
                {
                    // Show error if sign out failed
                    Console.WriteLine("ProfileViewModel: Sign out failed");
                    HasError = true;
                    ErrorMessage = "Failed to sign out. Please try again.";
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ProfileViewModel: Error signing out: {ex.Message}");
                HasError = true;
                ErrorMessage = "An error occurred while signing out.";
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private async Task Search()
        {
            if (!string.IsNullOrWhiteSpace(SearchQuery))
            {
                try // Add try-catch for navigation
                {
                    await _navigationService.NavigateToAsync("Library", new LibraryFilterOptions { SearchQuery = SearchQuery });
                    SearchQuery = string.Empty; // Clear only on successful navigation
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error navigating to Library search: {ex.Message}");
                }
            }
        }

        // Methods for updating the UI
        internal void RemoveBorrowedBook(string bookId)
        {
            for (int i = 0; i < BorrowedBooks.Count; i++)
            {
                if (BorrowedBooks[i].Id == bookId)
                {
                    BorrowedBooks.RemoveAt(i);
                    BorrowedBooksCount--;
                    break;
                }
            }
        }

        internal void RemoveReservedBook(string bookId)
        {
            for (int i = 0; i < ReservedBooks.Count; i++)
            {
                if (ReservedBooks[i].Id == bookId)
                {
                    ReservedBooks.RemoveAt(i);
                    ReservedBooksCount--;
                    break;
                }
            }
        }

        [RelayCommand]
        private async Task NavigateToLogin()
        {
            try
            {
                Console.WriteLine("ProfileViewModel: Navigating to login page");
                await _navigationService.NavigateToAsync("Login");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ProfileViewModel: Error navigating to login: {ex.Message}");
                // Don't set error message as we're already on an error page
            }
        }
    }

    public partial class BorrowedBookViewModel : ViewModelBase
    {
        public string Id { get; }
        public string Title { get; }
        public string Author { get; }
        public string DueDate { get; }
        public string CoverImageUrl { get; }

        [ObservableProperty]
        private bool _isReturning = false;

        [ObservableProperty]
        private bool _hasError = false;

        [ObservableProperty]
        private string _errorMessage = string.Empty;

        private readonly IBookService _bookService;
        private readonly ProfileViewModel _parentViewModel;

        public BorrowedBookViewModel(BookModel book, IBookService bookService, ProfileViewModel parentViewModel)
        {
            _bookService = bookService;
            _parentViewModel = parentViewModel;
            Id = book.Id;
            Title = book.Title;
            Author = book.Author;
            
            // Initialize CoverImageUrl from book
            CoverImageUrl = book.CoverImageUrl;
            if (string.IsNullOrEmpty(CoverImageUrl))
            {
                // Fallback to a placeholder if empty
                string safeTitle = Uri.EscapeDataString(Title.Length > 10 ? Title.Substring(0, 10) : Title);
                CoverImageUrl = $"https://dummyimage.com/160x200/2e74a8/ffffff.png&text={safeTitle}";
            }

            // TODO: Replace with actual due date from the database once available
            var dueDate = DateTime.Now.AddDays(7); // Simulation
            DueDate = $"Due: {dueDate:dd/MM/yyyy}";
        }

        [RelayCommand]
        private async Task Return()
        {
            try
            {
                IsReturning = true;
                HasError = false;
                
                Console.WriteLine($"Returning book: {Id} - {Title}");
                await _bookService.ReturnBookAsync(Id);
                
                // Remove this book from the parent's collection
                _parentViewModel.RemoveBorrowedBook(Id);
                
                Console.WriteLine($"Book returned successfully: {Id}");
            }
            catch (Exception ex)
            {
                HasError = true;
                ErrorMessage = $"Failed to return book: {ex.Message}";
                Console.WriteLine($"Error returning book {Id}: {ex.Message}");
            }
            finally
            {
                IsReturning = false;
            }
        }
    }

    public partial class ReservedBookViewModel : ViewModelBase
    {
        public string Id { get; }
        public string Title { get; }
        public string Author { get; }
        public string ReservationStatus { get; }
        public string CoverImageUrl { get; }

        [ObservableProperty]
        private bool _isCancelling = false;

        [ObservableProperty]
        private bool _hasError = false;

        [ObservableProperty]
        private string _errorMessage = string.Empty;

        private readonly IBookService _bookService;
        private readonly ProfileViewModel _parentViewModel;

        public ReservedBookViewModel(BookModel book, IBookService bookService, ProfileViewModel parentViewModel)
        {
            _bookService = bookService;
            _parentViewModel = parentViewModel;
            Id = book.Id;
            Title = book.Title;
            Author = book.Author;
            
            // Initialize CoverImageUrl from book
            CoverImageUrl = book.CoverImageUrl;
            if (string.IsNullOrEmpty(CoverImageUrl))
            {
                // Fallback to a placeholder if empty
                string safeTitle = Uri.EscapeDataString(Title.Length > 10 ? Title.Substring(0, 10) : Title);
                CoverImageUrl = $"https://dummyimage.com/160x200/2e74a8/ffffff.png&text={safeTitle}";
            }

            // Set reservation status based on book availability
            ReservationStatus = book.AvailableCopies > 0 ? "Available now" : "Waiting for availability";
        }

        [RelayCommand]
        private async Task Cancel()
        {
            try
            {
                IsCancelling = true;
                HasError = false;
                
                Console.WriteLine($"Cancelling reservation for book: {Id} - {Title}");
                await _bookService.CancelReservationAsync(Id);
                
                // Remove this book from the parent's collection
                _parentViewModel.RemoveReservedBook(Id);
                
                Console.WriteLine($"Reservation cancelled successfully: {Id}");
            }
            catch (Exception ex)
            {
                HasError = true;
                ErrorMessage = $"Failed to cancel reservation: {ex.Message}";
                Console.WriteLine($"Error cancelling reservation for book {Id}: {ex.Message}");
            }
            finally
            {
                IsCancelling = false;
            }
        }
    }
}
