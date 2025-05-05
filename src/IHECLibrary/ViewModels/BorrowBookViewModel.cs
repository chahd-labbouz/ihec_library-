using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IHECLibrary.Services;
using IHECLibrary.Services.Implementations;
using System;
using System.Threading.Tasks;

namespace IHECLibrary.ViewModels
{
    public partial class BorrowBookViewModel : ViewModelBase, IParameterizedViewModel
    {
        private readonly INavigationService _navigationService;
        private readonly IBookService _bookService;
        private readonly IUserService _userService;
        
        [ObservableProperty]
        private string _fullName = string.Empty;
        
        [ObservableProperty]
        private string _email = string.Empty;
        
        [ObservableProperty]
        private string _bookTitle = string.Empty;
        
        [ObservableProperty]
        private string _bookId = string.Empty;
        
        [ObservableProperty]
        private DateTime _borrowDate = DateTime.Now;
        
        [ObservableProperty]
        private DateTime _returnDate = DateTime.Now.AddDays(15);
        
        [ObservableProperty]
        private bool _acceptTerms = false;
        
        [ObservableProperty]
        private bool _isLoading = false;
        
        [ObservableProperty]
        private string _errorMessage = string.Empty;
        
        public bool HasError => !string.IsNullOrEmpty(ErrorMessage);
        
        public BorrowBookViewModel(INavigationService navigationService, IBookService bookService, IUserService userService)
        {
            _navigationService = navigationService;
            _bookService = bookService;
            _userService = userService;
            
            // Set default values when view model is created
            LoadUserInfo();
        }
        
        private async void LoadUserInfo()
        {
            try
            {
                IsLoading = true;
                var user = await _userService.GetCurrentUserAsync();
                if (user != null)
                {
                    FullName = $"{user.FirstName} {user.LastName}";
                    Email = user.Email;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading user info: {ex.Message}");
                ErrorMessage = "Failed to load user information. Please enter your details manually.";
            }
            finally
            {
                IsLoading = false;
            }
        }
        
        // Method to set the book details when navigating to this view
        public void SetBookDetails(string bookId, string bookTitle)
        {
            BookId = bookId;
            BookTitle = bookTitle;
        }
        
        [RelayCommand]
        private async Task ConfirmBorrow()
        {
            if (string.IsNullOrWhiteSpace(FullName) || string.IsNullOrWhiteSpace(Email))
            {
                ErrorMessage = "Please enter your name and email.";
                return;
            }
            
            if (!AcceptTerms)
            {
                ErrorMessage = "You must accept the terms to borrow this book.";
                return;
            }
            
            if (ReturnDate > BorrowDate.AddDays(15))
            {
                ErrorMessage = "The maximum borrowing period is 15 days.";
                return;
            }
            
            try
            {
                IsLoading = true;
                ErrorMessage = string.Empty;
                
                // Call book service to submit borrow request
                bool success = await _bookService.BorrowBookAsync(BookId, ReturnDate);
                
                if (success)
                {
                    // Navigate back to library or home
                    await _navigationService.NavigateToAsync("Library");
                }
                else
                {
                    ErrorMessage = "Failed to borrow the book. Please try again.";
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error borrowing book: {ex.Message}");
                ErrorMessage = $"Error borrowing book: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }
        
        [RelayCommand]
        private async Task Cancel()
        {
            await _navigationService.NavigateToAsync("Library");
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
        
        // Implement IParameterizedViewModel interface
        public async Task InitializeAsync(object parameter)
        {
            if (parameter is object paramObj)
            {
                try
                {
                    // Try to extract properties using reflection
                    var properties = paramObj.GetType().GetProperties();
                    
                    foreach (var prop in properties)
                    {
                        if (prop.Name == "BookId" && prop.GetValue(paramObj) is string bookId)
                        {
                            BookId = bookId;
                        }
                        else if (prop.Name == "BookTitle" && prop.GetValue(paramObj) is string bookTitle)
                        {
                            BookTitle = bookTitle;
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error initializing BorrowBookViewModel: {ex.Message}");
                    ErrorMessage = "Failed to load book details.";
                }
            }
            
            await Task.CompletedTask;
        }
    }
} 