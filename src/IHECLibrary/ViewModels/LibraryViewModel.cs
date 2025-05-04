using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IHECLibrary.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using IHECLibrary; // Added import for BookModel

namespace IHECLibrary.ViewModels
{
    public partial class LibraryViewModel : ViewModelBase
    {
        [ObservableProperty]
        private string _pageTitle = "All Books";

        [ObservableProperty]
        private string _searchQuery = string.Empty;

        [ObservableProperty]
        private string _userFullName = string.Empty;

        [ObservableProperty]
        private string _userProfilePicture = string.Empty;

        [ObservableProperty]
        private ObservableCollection<BookViewModel> _books = new();

        [ObservableProperty]
        private ObservableCollection<CategoryViewModel> _categories = new();

        [ObservableProperty]
        private ObservableCollection<LanguageViewModel> _languages = new();

        [ObservableProperty]
        private bool _isAvailableOnly = false;

        [ObservableProperty]
        private string _selectedSortOption = string.Empty;

        [ObservableProperty]
        private int _currentPage = 1;

        [ObservableProperty]
        private int _totalPages = 1;

        [ObservableProperty]
        private bool _isLoading = false;

        [ObservableProperty]
        private bool _hasNextPage = false;

        [ObservableProperty]
        private bool _hasPreviousPage = false;

        private const int PAGE_SIZE = 12;

        public ObservableCollection<string> SortOptions { get; } = new ObservableCollection<string>
        {
            "Most Popular", "Newest", "Title A-Z", "Author A-Z"
        };

        private readonly INavigationService _navigationService;
        private readonly IBookService _bookService;
        private readonly IUserService _userService;
        private LibraryFilterOptions? _initialFilters;

        public LibraryViewModel(INavigationService navigationService, IBookService bookService, IUserService userService, object? parameter = null)
        {
            _navigationService = navigationService;
            _bookService = bookService;
            _userService = userService;
            _initialFilters = parameter as LibraryFilterOptions;

            SelectedSortOption = SortOptions[0];
            InitializeCategories();
            InitializeLanguages();
            LoadUserData();
            LoadBooks();
        }

        private void InitializeCategories()
        {
            Categories.Add(new CategoryViewModel { Name = "Finance", IsSelected = false });
            Categories.Add(new CategoryViewModel { Name = "Management", IsSelected = false });
            Categories.Add(new CategoryViewModel { Name = "Marketing", IsSelected = false });
            Categories.Add(new CategoryViewModel { Name = "Economics", IsSelected = false });
            Categories.Add(new CategoryViewModel { Name = "Accounting", IsSelected = false });
            Categories.Add(new CategoryViewModel { Name = "BI", IsSelected = true });
            Categories.Add(new CategoryViewModel { Name = "Big Data", IsSelected = false });

            // Si des filtres initiaux sont fournis, les appliquer
            if (_initialFilters != null && !string.IsNullOrEmpty(_initialFilters.Category))
            {
                foreach (var category in Categories)
                {
                    category.IsSelected = category.Name == _initialFilters.Category;
                }
                PageTitle = $"{_initialFilters.Category} Books";
            }
        }

        private void InitializeLanguages()
        {
            Languages.Add(new LanguageViewModel { Name = "English", IsSelected = true });
            Languages.Add(new LanguageViewModel { Name = "French", IsSelected = false });
            Languages.Add(new LanguageViewModel { Name = "Arabic", IsSelected = false });
        }

        private async void LoadUserData()
        {
            var user = await _userService.GetCurrentUserAsync();
            if (user != null)
            {
                UserFullName = $"{user.FirstName} {user.LastName}";
                UserProfilePicture = user.ProfilePictureUrl ?? "/Assets/default_profile.png";
            }
        }

        private async void LoadBooks()
        {
            IsLoading = true;
            try
            {
                Console.WriteLine($"LibraryViewModel: Loading books for page {CurrentPage}, pageSize: {PAGE_SIZE}");
                
                List<BookModel> books = new List<BookModel>();
                string? categoryFilter = null;
                string? searchFilter = null;

                // Apply initial filters if provided
                if (_initialFilters != null && !string.IsNullOrEmpty(_initialFilters.SearchQuery))
                {
                    SearchQuery = _initialFilters.SearchQuery;
                    searchFilter = SearchQuery;
                    PageTitle = $"Search Results: {SearchQuery}";
                    Console.WriteLine($"Applying initial search filter: {searchFilter}");
                }
                else if (_initialFilters != null && !string.IsNullOrEmpty(_initialFilters.Category))
                {
                    categoryFilter = _initialFilters.Category;
                    PageTitle = $"{categoryFilter} Books";
                    Console.WriteLine($"Applying initial category filter: {categoryFilter}");
                    
                    // Make sure the category is selected in UI
                    var categoryVM = Categories.FirstOrDefault(c => c.Name == categoryFilter);
                    if (categoryVM != null)
                    {
                        // Reset all categories
                        foreach (var cat in Categories)
                        {
                            cat.IsSelected = false;
                        }
                        categoryVM.IsSelected = true;
                    }
                }
                // Determine filters from UI selections
                else if (!string.IsNullOrEmpty(SearchQuery))
                {
                    searchFilter = SearchQuery;
                    PageTitle = $"Search Results: {SearchQuery}";
                    Console.WriteLine($"Applying search filter from UI: {searchFilter}");
                }
                else
                {
                    // Get selected categories for the filter
                    var selectedCategories = Categories.Where(c => c.IsSelected).Select(c => c.Name).ToList();
                    if (selectedCategories.Count > 0)
                    {
                        categoryFilter = selectedCategories.First(); // Use first category for the filter
                        PageTitle = $"{categoryFilter} Books";
                        Console.WriteLine($"Applying category filter from UI: {categoryFilter}");
                    }
                    else
                    {
                        PageTitle = "All Books";
                        Console.WriteLine("No filters applied, showing all books");
                    }
                }

                // First try to load all books to ensure we have something to display
                Console.WriteLine("Loading all books first to ensure we have data to show");
                books = await _bookService.GetRealBooksAsync(
                    page: 1, 
                    pageSize: 50, // Get more books to have enough data
                    category: null,
                    searchQuery: null
                );
                
                Console.WriteLine($"Loaded {books.Count} total books");
                
                // Then apply filters locally if we have books and filters
                if (books.Count > 0)
                {
                    if (!string.IsNullOrEmpty(categoryFilter))
                    {
                        var filteredBooks = books.Where(b => 
                            !string.IsNullOrEmpty(b.Category) && 
                            b.Category.Equals(categoryFilter, StringComparison.OrdinalIgnoreCase)
                        ).ToList();
                        
                        if (filteredBooks.Count > 0)
                        {
                            books = filteredBooks;
                            Console.WriteLine($"Applied category filter, now have {books.Count} books");
                        }
                        else
                        {
                            Console.WriteLine($"No books match category '{categoryFilter}', showing all books");
                        }
                    }
                    
                    if (!string.IsNullOrEmpty(searchFilter))
                    {
                        var filteredBooks = books.Where(b => 
                            (!string.IsNullOrEmpty(b.Title) && b.Title.Contains(searchFilter, StringComparison.OrdinalIgnoreCase)) ||
                            (!string.IsNullOrEmpty(b.Author) && b.Author.Contains(searchFilter, StringComparison.OrdinalIgnoreCase)) ||
                            (!string.IsNullOrEmpty(b.Description) && b.Description.Contains(searchFilter, StringComparison.OrdinalIgnoreCase))
                        ).ToList();
                        
                        if (filteredBooks.Count > 0)
                        {
                            books = filteredBooks;
                            Console.WriteLine($"Applied search filter, now have {books.Count} books");
                        }
                        else
                        {
                            Console.WriteLine($"No books match search '{searchFilter}', showing all books");
                        }
                    }
                }
                
                // Apply availability filter if needed
                if (IsAvailableOnly && books.Count > 0)
                {
                    var originalCount = books.Count;
                    books = books.Where(b => b.IsAvailable()).ToList();
                    Console.WriteLine($"Applied availability filter: {originalCount} -> {books.Count} books");
                }
                
                // Apply sorting
                books = SortBooks(books);
                Console.WriteLine("Applied sorting");
                
                // Update pagination info
                TotalPages = Math.Max(1, (int)Math.Ceiling(books.Count / (double)PAGE_SIZE));
                HasNextPage = CurrentPage < TotalPages;
                HasPreviousPage = CurrentPage > 1;
                
                // Apply pagination
                var pagedBooks = books
                    .Skip((CurrentPage - 1) * PAGE_SIZE)
                    .Take(PAGE_SIZE)
                    .ToList();
                
                Console.WriteLine($"After pagination: {pagedBooks.Count} books for display");
                
                // Update the UI with books
                Books.Clear();
                
                foreach (var book in pagedBooks)
                {
                    try
                    {
                        // Create a debug copy of the book in case of errors
                        Console.WriteLine($"Processing book: ID={book.Id}, Title={book.Title}, Author={book.Author}, Category={book.Category}");
                        var bookViewModel = new BookViewModel(book, _bookService);
                        Books.Add(bookViewModel);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error creating BookViewModel: {ex.Message}");
                    }
                }
                
                Console.WriteLine($"LibraryViewModel: Added {Books.Count} books to UI");
                
                // If still no books, show message or take other action
                if (Books.Count == 0)
                {
                    Console.WriteLine("WARNING: No books to display after all attempts");
                    PageTitle = "No Books Found";
                }
                
                // Reset initial filters after first load to prevent them from being reapplied
                _initialFilters = null;
            }
            catch (Exception ex) 
            {
                Console.WriteLine($"Error loading books: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                PageTitle = "Error Loading Books";
                Books.Clear();
            }
            finally
            {
                IsLoading = false;
            }
        }

        private List<BookModel> SortBooks(List<BookModel> books)
        {
            return SelectedSortOption switch
            {
                "Most Popular" => books.OrderByDescending(b => b.LikesCount).ToList(),
                "Newest" => books.OrderByDescending(b => b.PublicationYear).ToList(),
                "Title A-Z" => books.OrderBy(b => b.Title).ToList(),
                "Author A-Z" => books.OrderBy(b => b.Author).ToList(),
                _ => books
            };
        }

        [RelayCommand]
        private async Task NavigateToHome()
        {
            await _navigationService.NavigateToAsync("Home");
        }

        [RelayCommand]
        private Task NavigateToLibrary()
        {
            // Déjà sur la page de bibliothèque, ne rien faire
            return Task.CompletedTask;
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
        private Task ApplyFilters()
        {
            LoadBooks();
            return Task.CompletedTask;
        }

        [RelayCommand]
        private async Task Search()
        {
            if (!string.IsNullOrWhiteSpace(SearchQuery))
            {
                PageTitle = $"Search Results: {SearchQuery}";
                var books = await _bookService.GetBooksBySearchAsync(SearchQuery);
                Books.Clear();
                foreach (var book in books)
                {
                    Books.Add(new BookViewModel(book, _bookService));
                }
            }
        }

        [RelayCommand]
        private Task NextPage()
        {
            if (HasNextPage)
            {
                CurrentPage++;
                LoadBooks();
            }
            return Task.CompletedTask;
        }

        [RelayCommand]
        private Task PreviousPage()
        {
            if (HasPreviousPage)
            {
                CurrentPage--;
                LoadBooks();
            }
            return Task.CompletedTask;
        }

        [RelayCommand]
        private Task RefreshBooks()
        {
            CurrentPage = 1;
            LoadBooks();
            return Task.CompletedTask;
        }
    }

    public partial class CategoryViewModel : ViewModelBase
    {
        [ObservableProperty]
        private string _name = string.Empty;

        [ObservableProperty]
        private bool _isSelected;
    }

    public partial class LanguageViewModel : ViewModelBase
    {
        [ObservableProperty]
        private string _name = string.Empty;

        [ObservableProperty]
        private bool _isSelected;
    }
}
