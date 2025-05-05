using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IHECLibrary.Services;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Input;

namespace IHECLibrary.ViewModels
{
    public partial class BookViewModel : ViewModelBase
    {
        [ObservableProperty]
        private string _id = string.Empty;

        [ObservableProperty]
        private string _title = string.Empty;

        [ObservableProperty]
        private string _author = string.Empty;

        [ObservableProperty]
        private string _category = string.Empty;

        [ObservableProperty]
        private string _coverImageUrl = string.Empty;

        [ObservableProperty]
        private bool _isAvailable;

        [ObservableProperty]
        private string _availabilityStatus = string.Empty;

        [ObservableProperty]
        private string _availabilityColor = "#4CAF50"; // Default green color for Available

        [ObservableProperty]
        private string _backgroundColor = "#FFFFFF"; // Default background color
        
        [ObservableProperty]
        private Color _cardColor;

        [ObservableProperty]
        private bool _isLiked = false;

        [ObservableProperty]
        private string _actionButtonText = "View Details";

        [ObservableProperty]
        private string _actionButtonBackground = "#2E74A8"; // Default blue color for action button

        // Common card background colors to rotate through
        private static readonly Color[] CardColors = new[]
        {
            Color.Parse("#e3f2fd"), // Light Blue
            Color.Parse("#f3e5f5"), // Light Purple
            Color.Parse("#e8f5e9"), // Light Green 
            Color.Parse("#fff3e0"), // Light Orange
            Color.Parse("#ffebee")  // Light Red
        };
        
        // Static counter to rotate through colors for consecutive books
        private static int colorIndex = 0;
        
        // Static constructor to initialize the color index with a more random starting point
        static BookViewModel()
        {
            colorIndex = DateTime.Now.Second % CardColors.Length;
        }

        // Make services nullable to avoid warning CS8618
        private readonly IBookService? _bookService;
        private readonly INavigationService? _navigationService;

        public BookViewModel(BookModel book, IBookService? bookService = null, INavigationService? navigationService = null)
        {
            try
            {
                Console.WriteLine($"Creating BookViewModel for book: {book.Title}");
                _bookService = bookService;
                _navigationService = navigationService;
                
                Id = book.Id;
                Title = string.IsNullOrWhiteSpace(book.Title) ? "Untitled Book" : book.Title;
                Author = string.IsNullOrWhiteSpace(book.Author) ? "Unknown Author" : book.Author;
                Category = string.IsNullOrWhiteSpace(book.Category) ? "General" : book.Category;
                
                // Set cover image
                // Always use the book.png image
                CoverImageUrl = "avares://IHECLibrary/Assets/book.png";
                Console.WriteLine($"Using book.png image for book: {Title}");
                
                // Set availability status
                IsAvailable = book.IsAvailable();
                AvailabilityStatus = IsAvailable ? "Available" : "Unavailable";
                AvailabilityColor = IsAvailable ? "#4CAF50" : "#F44336"; // Green for available, red for unavailable
                
                // Assign a background color from our rotation
                CardColor = CardColors[colorIndex];
                colorIndex = (colorIndex + 1) % CardColors.Length;
                
                IsLiked = book.IsLikedByCurrentUser;

                // Set action button properties based on availability
                ActionButtonText = IsAvailable ? "Borrow" : "Reserve";
                ActionButtonBackground = IsAvailable ? "#2E74A8" : "#9E9E9E"; 
                
                Console.WriteLine($"BookViewModel created successfully: {Title}, IsAvailable: {IsAvailable}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating BookViewModel: {ex.Message}");
                
                // Set default values to prevent UI errors
                Title = "Book";
                Author = "Author";
                Category = "General";
                CoverImageUrl = "avares://IHECLibrary/Assets/book.png";
                IsAvailable = true;
                AvailabilityStatus = "Available";
                AvailabilityColor = "#4CAF50";
                CardColor = CardColors[0];
                ActionButtonText = "View";
                ActionButtonBackground = "#2E74A8";
            }
        }

        // Direct command implementation for the action button
        public ICommand ActionCommand => new RelayCommand(ExecuteAction);
        
        private void ExecuteAction()
        {
            try
            {
                Console.WriteLine($"ExecuteAction called for book: {Id} - {Title}");
                
                // Navigate directly to borrow form
                if (_navigationService != null)
                {
                    Console.WriteLine($"Navigating to BorrowForm for book: {Id} - {Title}");
                    
                    // Create a simple parameter object with book data
                    var param = new Dictionary<string, string>
                    {
                        ["BookId"] = Id,
                        ["BookTitle"] = Title
                    };
                    
                    _navigationService.NavigateToAsync("BorrowForm", param).Wait();
                }
                else
                {
                    Console.WriteLine("Error: NavigationService is null");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in ExecuteAction: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }
        }

        [RelayCommand]
        private async Task ToggleLike()
        {
            if (_bookService == null)
            {
                Console.WriteLine("Cannot toggle like: BookService is null");
                return;
            }
            
            try
            {
                bool wasLiked = IsLiked;
                IsLiked = !wasLiked;
                
                // Call the appropriate API method based on the new state
                if (IsLiked)
                {
                    await _bookService.LikeBookAsync(Id);
                }
                else
                {
                    await _bookService.UnlikeBookAsync(Id);
                }
            }
            catch (Exception ex)
            {
                // If the API call fails, revert the UI change
                IsLiked = !IsLiked;
                Console.WriteLine($"Error toggling book like: {ex.Message}");
            }
        }
    }
}