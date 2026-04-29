using System.Diagnostics;

namespace Chizl.Applications
{
    /// <summary>
    /// Provides application version and metadata information retrieved from the executable file.
    /// </summary>
    internal static class About
    {
        static string _fileVersion = string.Empty;
        static int[] _fileVerArray = new int[4] { 0, 0, 0, 0 };
        static string _productVersion = string.Empty;
        static string _title = string.Empty;
        static string _company = string.Empty;
        static string _productName = string.Empty;
        static string _copyright = string.Empty;
        static string _description = string.Empty;
        static string _trademark = string.Empty;
        static string _appFullPath = string.Empty;
        static string _appRootDir = string.Empty;  
        static string _appFileName = string.Empty;
        static string _domainName = string.Empty;
        static string _userName = string.Empty;

        /// <summary>
        /// Returns the file version string composed of elements from the version array up to the specified depth.
        /// </summary>
        /// <param name="depth">The number of version elements to include in the result.</param>
        /// <returns>A dot-separated file version string if depth is valid; otherwise, an empty string.</returns>
        public static string GetFileVersion(int depth) => 
                        depth > 0 && depth <= _fileVerArray.Length 
                            ? string.Join(".", _fileVerArray.Take(depth)) 
                            : "";
        /// <summary>
        /// Gets the version of the file as a string.
        /// </summary>
        public static string FileVersion => _fileVersion;
        /// <summary>
        /// Gets the version of the product.
        /// </summary>
        public static string ProductVersion => _productVersion;
        /// <summary>
        /// Gets the application title.
        /// </summary>
        public static string Title => _title;
        /// <summary>
        /// Gets the application title combined with the full file version.
        /// </summary>
        public static string TitleWithFileVersion => $"{_title} v{_fileVersion}";
        /// <summary>
        /// Gets the company name.
        /// </summary>
        public static string Company => _company;
        /// <summary>
        /// Gets the name of the product.
        /// </summary>
        public static string ProductName => _productName;
        /// <summary>
        /// Gets the copyright information for the application.
        /// </summary>
        public static string Copyright => _copyright;
        /// <summary>
        /// Gets the description text.
        /// </summary>
        public static string Description => _description;
        /// <summary>
        /// Gets the trademark information associated with the application.
        /// </summary>
        public static string Trademark => _trademark;
        /// <summary>
        /// Gets the full path to the application.
        /// </summary>
        public static string AppFullPath => _appFullPath;
        /// <summary>
        /// Gets the application's root directory path.
        /// </summary>
        public static string AppRootDir => _appRootDir;
        /// <summary>
        /// Gets the application file name.
        /// </summary>
        public static string AppFileName => _appFileName;
        /// <summary>
        /// Gets the domain name used by the application.
        /// </summary>
        public static string DomainName => _domainName;
        /// <summary>
        /// Gets the current user's name.
        /// </summary>
        public static string UserName => _userName;

        /// <summary>
        /// Initializes static fields with application version and metadata information from the executable file.
        /// </summary>
        static About()
        {
            _domainName = Environment.UserDomainName;
            _userName = Environment.UserName;

            // Retrieve the file version information for the current executable and populate the static fields.
            FileVersionInfo fi = FileVersionInfo.GetVersionInfo(Application.ExecutablePath);
            if (fi == null)
                return;

            _fileVerArray = new int[] { fi.FileMajorPart, fi.FileMinorPart, fi.FileBuildPart, fi.FilePrivatePart };
            _fileVersion = fi.FileVersion ?? "0";
            _productVersion = fi.ProductVersion ?? "0";
            _title = fi.FileDescription?.Trim() ?? "";
            _company = fi.CompanyName?.Trim() ?? "";
            _productName = fi.ProductName?.Trim() ?? "";
            _copyright = fi.LegalCopyright?.Trim() ?? "";
            _description = fi.Comments?.Trim() ?? "";
            _trademark = fi.LegalTrademarks?.Trim() ?? "";
            _appFullPath = fi.FileName?.Trim() ?? "";
            _appRootDir = Path.GetDirectoryName(_appFullPath) ?? "";
            _appFileName = fi.OriginalFilename?.Trim() ?? "";
        }
    }
}
