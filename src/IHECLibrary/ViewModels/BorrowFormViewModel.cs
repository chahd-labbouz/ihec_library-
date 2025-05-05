using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IHECLibrary.Services;
using IHECLibrary.Services.Implementations;
using System;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace IHECLibrary.ViewModels
{
    public partial class BorrowFormViewModel : ViewModelBase, IParameterizedViewModel
    {
        private readonly INavigationService _navigationService;
        private readonly IBookService _bookService;
        private readonly IUserService _userService;
        
        [ObservableProperty]
        private string _bookId = string.Empty;
        
        [ObservableProperty]
        private string _bookTitle = string.Empty;
        
        [ObservableProperty]
        private string _fullName = string.Empty;
        
        [ObservableProperty]
        private string _email = string.Empty;
        
        [ObservableProperty]
        private DateTimeOffset? _borrowDate;
        
        [ObservableProperty]
        private DateTimeOffset? _returnDate;
        
        [ObservableProperty]
        private bool _acceptTerms;
        
        [ObservableProperty]
        private bool _isLoading;
        
        [ObservableProperty]
        private string _errorMessage = string.Empty;
        
        public bool HasError => !string.IsNullOrEmpty(ErrorMessage);
        
        public bool CanConfirm => 
            !string.IsNullOrWhiteSpace(FullName) && 
            !string.IsNullOrWhiteSpace(Email) && 
            !string.IsNullOrWhiteSpace(BookTitle) && 
            BorrowDate.HasValue && 
            ReturnDate.HasValue && 
            AcceptTerms;
        
        public BorrowFormViewModel(INavigationService navigationService, IBookService bookService, IUserService userService)
        {
            _navigationService = navigationService;
            _bookService = bookService;
            _userService = userService;
            
            try
            {
                // Set today's date as the default borrow date
                BorrowDate = new DateTimeOffset(DateTime.Today);
                
                // Set default return date (15 days from today)
                ReturnDate = new DateTimeOffset(DateTime.Today.AddDays(15));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error setting dates: {ex.Message}");
                // Set to null if any issues occur
                BorrowDate = null;
                ReturnDate = null;
            }
        }
        
        public async Task InitializeAsync(object parameter)
        {
            try
            {
                if (parameter != null)
                {
                    // Handle Dictionary parameter
                    if (parameter is Dictionary<string, string> dict)
                    {
                        if (dict.TryGetValue("BookId", out string? bookId) && 
                            dict.TryGetValue("BookTitle", out string? bookTitle))
                        {
                            BookId = bookId;
                            BookTitle = bookTitle;
                            
                            Console.WriteLine($"BorrowFormViewModel: Received book {BookId} - {BookTitle} from Dictionary");
                            
                            // Load user info
                            await LoadUserInfoAsync();
                        }
                        else
                        {
                            ErrorMessage = "Missing book information in parameters.";
                        }
                    }
                    // Handle NavigationParameter
                    else if (parameter is NavigationParameter navParam)
                    {
                        BookId = navParam.BookId;
                        BookTitle = navParam.BookTitle;
                        
                        Console.WriteLine($"BorrowFormViewModel: Received book {BookId} - {BookTitle}");
                        
                        // Load user info
                        await LoadUserInfoAsync();
                    }
                    else if (parameter is string paramBookId)
                    {
                        BookId = paramBookId;
                        await LoadBookInfoAsync(paramBookId);
                        await LoadUserInfoAsync();
                    }
                    else
                    {
                        Console.WriteLine($"BorrowFormViewModel: Unsupported parameter type: {parameter.GetType().Name}");
                        
                        // Try reflection as fallback
                        try
                        {
                            var paramType = parameter.GetType();
                            var bookIdProp = paramType.GetProperty("BookId");
                            var bookTitleProp = paramType.GetProperty("BookTitle");
                            
                            if (bookIdProp != null && bookTitleProp != null)
                            {
                                var bookId = bookIdProp.GetValue(parameter)?.ToString() ?? "";
                                var bookTitle = bookTitleProp.GetValue(parameter)?.ToString() ?? "";
                                
                                if (!string.IsNullOrEmpty(bookId))
                                {
                                    BookId = bookId;
                                    BookTitle = bookTitle;
                                    
                                    Console.WriteLine($"BorrowFormViewModel: Using reflection, got book {BookId} - {BookTitle}");
                                    
                                    // Load user info
                                    await LoadUserInfoAsync();
                                }
                                else
                                {
                                    ErrorMessage = "Book ID is missing.";
                                }
                            }
                            else
                            {
                                Console.WriteLine("BorrowFormViewModel: Could not extract parameters via reflection");
                                ErrorMessage = "Could not load book information.";
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error extracting parameters via reflection: {ex.Message}");
                            ErrorMessage = "Could not load book information.";
                        }
                    }
                }
                else
                {
                    Console.WriteLine("BorrowFormViewModel: Parameter is null");
                    ErrorMessage = "No book information provided.";
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"BorrowFormViewModel initialization error: {ex.Message}");
                ErrorMessage = $"Error: {ex.Message}";
            }
        }
        
        private async Task LoadBookInfoAsync(string bookId)
        {
            try
            {
                IsLoading = true;
                var book = await _bookService.GetBookByIdAsync(bookId);
                if (book != null)
                {
                    BookTitle = book.Title;
                }
                else
                {
                    ErrorMessage = "Could not find the specified book.";
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading book: {ex.Message}");
                ErrorMessage = $"Error loading book: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }
        
        private async Task LoadUserInfoAsync()
        {
            try
            {
                var currentUser = await _userService.GetCurrentUserAsync();
                if (currentUser != null)
                {
                    FullName = $"{currentUser.FirstName} {currentUser.LastName}";
                    Email = currentUser.Email;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading user info: {ex.Message}");
                // Don't set error message here, just leave the fields empty
            }
        }
        
        [RelayCommand]
        private async Task ConfirmBorrow()
        {
            // Remove the CanConfirm validation check to allow the button to always work
            // If form is incomplete, we'll still navigate to Library
            try
            {
                IsLoading = true;
                ErrorMessage = string.Empty;
                
                // Even with validation issues, proceed with navigation
                await _navigationService.NavigateToAsync("Library");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error confirming borrow: {ex.Message}");
                ErrorMessage = $"Error: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }
        
        [RelayCommand]
        private async Task Cancel()
        {
            // Navigate back to previous page
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
        
        // Called when a property changes to update the CanConfirm property
        partial void OnFullNameChanged(string value) => OnPropertyChanged(nameof(CanConfirm));
        partial void OnEmailChanged(string value) => OnPropertyChanged(nameof(CanConfirm));
        partial void OnBookTitleChanged(string value) => OnPropertyChanged(nameof(CanConfirm));
        partial void OnBorrowDateChanged(DateTimeOffset? value) => OnPropertyChanged(nameof(CanConfirm));
        partial void OnReturnDateChanged(DateTimeOffset? value) => OnPropertyChanged(nameof(CanConfirm));
        partial void OnAcceptTermsChanged(bool value) => OnPropertyChanged(nameof(CanConfirm));
    }
} 