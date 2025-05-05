using System;

namespace IHECLibrary.Services.Implementations
{
    /// <summary>
    /// A simple container for navigation parameters to avoid dynamic object issues
    /// </summary>
    public class NavigationParameter
    {
        public string BookId { get; set; } = string.Empty;
        public string BookTitle { get; set; } = string.Empty;
    }
} 