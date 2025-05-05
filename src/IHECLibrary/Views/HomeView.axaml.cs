using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using IHECLibrary.Services;
using IHECLibrary.Services.Implementations;
using IHECLibrary.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;

namespace IHECLibrary.Views
{
    public partial class HomeView : UserControl
    {
        private readonly INavigationService? _navigationService;

        public HomeView()
        {
            InitializeComponent();
            
            // Try to get the navigation service directly from the DataContext
            if (DataContext is HomeViewModel homeViewModel)
            {
                // Access the navigation service from the ViewModel if possible
                var field = typeof(HomeViewModel).GetField("_navigationService", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                _navigationService = field?.GetValue(homeViewModel) as INavigationService;
            }
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private async void OnBorrowButtonClicked(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is Button button && button.Tag is BookViewModel book)
                {
                    Console.WriteLine($"OnBorrowButtonClicked: {book.Id} - {book.Title}");
                    
                    // Get the navigation service from ViewModel if not already cached
                    INavigationService? navigationService = _navigationService;
                    
                    if (navigationService == null && DataContext is HomeViewModel viewModel)
                    {
                        // Access the navigation service from the ViewModel if possible
                        var field = typeof(HomeViewModel).GetField("_navigationService", 
                            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        navigationService = field?.GetValue(viewModel) as INavigationService;
                    }
                    
                    if (navigationService != null)
                    {
                        // Create a simple parameter object with book data
                        var param = new Dictionary<string, string>
                        {
                            ["BookId"] = book.Id,
                            ["BookTitle"] = book.Title
                        };
                        
                        // Navigate to the borrow form with the book data
                        await navigationService.NavigateToAsync("BorrowForm", param);
                    }
                    else
                    {
                        Console.WriteLine("Navigation service is null");
                    }
                }
                else
                {
                    Console.WriteLine("Invalid sender or tag");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in OnBorrowButtonClicked: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }
        }
    }
}
