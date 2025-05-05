using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IHECLibrary.Services;
using IHECLibrary.Services.Implementations;
using System;
using System.Threading.Tasks;

namespace IHECLibrary.ViewModels
{
    public partial class BookDetailsViewModel : ViewModelBase, IParameterizedViewModel
    {
        private readonly INavigationService _navigationService;
        private readonly IBookService _bookService;
        
        [ObservableProperty]
        private BookModel _book;
        
        [ObservableProperty]
        private bool _isLoading;
        
        [ObservableProperty]
        private string _errorMessage = string.Empty;
        
        public bool HasError => !string.IsNullOrEmpty(ErrorMessage);
        
        public BookDetailsViewModel(INavigationService navigationService, IBookService bookService)
        {
            _navigationService = navigationService;
            _bookService = bookService;
            
            // Initialize with a default BookModel to avoid null reference exceptions
            Book = new BookModel
            {
                Title = "Loading...",
                Author = "",
                Category = "",
                Description = "Loading book details...",
                Rating = 0,
                PageCount = 0,
                Language = "",
                ISBN = "",
                AvailableCopies = 0
            };
        }
        
        public async Task InitializeAsync(object parameter)
        {
            try
            {
                IsLoading = true;
                ErrorMessage = string.Empty;
                
                if (parameter is string bookId)
                {
                    await LoadBookAsync(bookId);
                }
                else
                {
                    Console.WriteLine("BookDetailsViewModel: Invalid parameter type - expected string bookId");
                    ErrorMessage = "Couldn't load book details: Invalid book ID";
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"BookDetailsViewModel: Error initializing - {ex.Message}");
                ErrorMessage = $"Error loading book details: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }
        
        private async Task LoadBookAsync(string bookId)
        {
            try
            {
                var book = await _bookService.GetBookByIdAsync(bookId);
                
                if (book != null)
                {
                    Book = book;
                    // Make sure to set more detailed fields that might not be in the basic model
                    if (string.IsNullOrEmpty(Book.Description))
                    {
                        Book.Description = "Principles of Economics is a leading textbook by N. Gregory Mankiw that provides a comprehensive introduction to economics. It covers both microeconomics and macroeconomics, presenting economic concepts in an accessible way with real-world examples. The book has been widely adopted in economics courses and is known for its clear explanations and engaging style.";
                    }
                    
                    if (Book.PageCount == 0)
                    {
                        Book.PageCount = 836;
                    }
                    
                    if (string.IsNullOrEmpty(Book.ISBN))
                    {
                        Book.ISBN = "978-1285165875";
                    }
                    
                    if (string.IsNullOrEmpty(Book.Language))
                    {
                        Book.Language = "Français";
                    }
                    
                    if (Book.Rating == 0)
                    {
                        Book.Rating = 4.5;
                    }
                    
                    // Set the availability color based on availability
                    Book.AvailabilityColor = Book.IsAvailable() ? "#4CAF50" : "#F44336"; // Green for available, red for unavailable
                    Book.AvailabilityStatus = Book.IsAvailable() ? "Disponible" : "Indisponible";
                }
                else
                {
                    Console.WriteLine($"BookDetailsViewModel: Book not found - ID: {bookId}");
                    ErrorMessage = "Book not found";
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"BookDetailsViewModel: Error loading book - {ex.Message}");
                ErrorMessage = $"Error loading book: {ex.Message}";
                throw;
            }
        }
        
        [RelayCommand]
        private async Task GoBack()
        {
            await _navigationService.NavigateToAsync("Library");
        }
        
        [RelayCommand]
        private async Task BorrowBook()
        {
            if (Book != null)
            {
                await _navigationService.NavigateToAsync("BorrowBook", new { BookId = Book.Id, BookTitle = Book.Title });
            }
        }
        
        [RelayCommand]
        private async Task ViewMoreDetails()
        {
            // For now this doesn't navigate anywhere new,
            // but could be extended to show even more detailed information
            // Or open an external link with more information about the book
            
            // For demonstration, we'll just reload the current page
            if (Book != null)
            {
                await LoadBookAsync(Book.Id);
            }
        }
        
        // Navigation commands
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
        private async Task NavigateToProfile()
        {
            await _navigationService.NavigateToAsync("Profile");
        }
        
        [RelayCommand]
        private async Task NavigateToChatbot()
        {
            await _navigationService.NavigateToAsync("Chatbot");
        }
    }
} 