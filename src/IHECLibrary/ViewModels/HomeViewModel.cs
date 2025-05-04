using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IHECLibrary.Services;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using IHECLibrary; // Corrected namespace for BookModel

namespace IHECLibrary.ViewModels
{
    public partial class HomeViewModel : ViewModelBase
    {
        [ObservableProperty]
        private string _welcomeMessage = string.Empty;

        [ObservableProperty]
        private string _recommendationSubtitle = string.Empty;

        [ObservableProperty]
        private string _searchQuery = string.Empty;

        [ObservableProperty]
        private string _userFullName = string.Empty;

        [ObservableProperty]
        private string _userProfilePicture = string.Empty;

        [ObservableProperty]
        private ObservableCollection<BookViewModel> _recommendedBooks = new();

        private readonly INavigationService _navigationService;
        private readonly IBookService _bookService;
        private readonly IUserService _userService;

        public HomeViewModel(INavigationService navigationService, IBookService bookService, IUserService userService)
        {
            _navigationService = navigationService;
            _bookService = bookService;
            _userService = userService;

            LoadUserData();
            LoadRecommendedBooks();
        }

        private async void LoadUserData()
        {
            var user = await _userService.GetCurrentUserAsync();
            if (user != null)
            {
                UserFullName = $"{user.FirstName} {user.LastName}";
                UserProfilePicture = user.ProfilePictureUrl ?? "/Assets/default_profile.png";
                WelcomeMessage = $"Welcome back, {user.FirstName}!";
                RecommendationSubtitle = $"Here are some recommendations for your {user.FieldOfStudy} studies";
            }
        }

        private async void LoadRecommendedBooks()
        {
            try
            {
                Console.WriteLine("HomeViewModel: Loading recommended books...");
                var books = await _bookService.GetRecommendedBooksAsync();
                
                Console.WriteLine($"HomeViewModel: Received {books.Count} books from service");
                
                // Clear existing books
                RecommendedBooks.Clear();
                
                if (books.Count == 0)
                {
                    Console.WriteLine("WARNING: No books returned from GetRecommendedBooksAsync");
                    
                    // Try loading all books instead
                    books = await _bookService.GetRealBooksAsync(1, 10);
                    Console.WriteLine($"Fallback to GetRealBooksAsync returned {books.Count} books");
                    
                    if (books.Count == 0)
                    {
                        // No books found, update UI message
                        RecommendationSubtitle = "No books found in the library. Please contact an administrator.";
                        return;
                    }
                }
                
                // Add books to the view model collection
                foreach (var book in books)
                {
                    try
                    {
                        Console.WriteLine($"Adding book to UI: {book.Title}");
                        var bookViewModel = new BookViewModel(book, _bookService);
                        RecommendedBooks.Add(bookViewModel);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error adding book to UI: {ex.Message}");
                    }
                }
                
                Console.WriteLine($"HomeViewModel: Added {RecommendedBooks.Count} books to UI");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in LoadRecommendedBooks: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                
                // Update UI with error message
                RecommendationSubtitle = "Unable to load books. Please try again later.";
            }
        }

        [RelayCommand]
        private Task NavigateToHome()
        {
            // Déjà sur la page d'accueil, ne rien faire
            return Task.CompletedTask;
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
        private async Task NavigateToProfile()
        {
            await _navigationService.NavigateToAsync("Profile");
        }

        [RelayCommand]
        private async Task ViewAllRecommendations()
        {
            await _navigationService.NavigateToAsync("Library", new LibraryFilterOptions { Category = "Recommended" });
        }

        [RelayCommand]
        private async Task Search()
        {
            if (!string.IsNullOrWhiteSpace(SearchQuery))
            {
                await _navigationService.NavigateToAsync("Library", new LibraryFilterOptions { SearchQuery = SearchQuery });
                SearchQuery = string.Empty;
            }
        }
    }
}
