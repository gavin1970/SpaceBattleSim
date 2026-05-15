using Chizl.DrawGraphics;

namespace SpaceBattleSim
{
    /// <summary>
    /// Provides access to ship skin images associated with each ship type.  Loaded once, shared all instances of ItemReq class.
    /// </summary>
    /// <remarks>This class maintains a mapping between ship types and their corresponding skin images. It is
    /// intended for internal use to retrieve the appropriate image for a given ship type. All skin images are loaded at
    /// application startup and are available for lookup throughout the application's lifetime.</remarks>
    internal static class ShipSkins
    {
        /// <summary>
        /// Stores the mapping between ship types and their associated images used for rendering ship skins.
        /// </summary>
        /// <remarks>Each entry associates a specific ShipType with its corresponding Image. This
        /// dictionary enables efficient retrieval of the correct image for a given ship type when rendering or updating
        /// ship appearances.</remarks>
        private static Dictionary<ShipType, Image> _skins = new Dictionary<ShipType, Image>();
        /// <summary>
        /// Initializes the static data for the ShipSkins class by loading predefined ship skin images for each ship
        /// type.
        /// </summary>
        /// <remarks>This static constructor ensures that all supported ship types have an associated skin
        /// image loaded from the specified file paths. Entries for unused ship types are included to prevent errors
        /// when accessing skin images for those types.</remarks>
        static ShipSkins() 
        {
            // Load skins from a configuration file or directory for hardcoded skins
            _skins.Add(ShipType.Fighter, new ShipSkin(ShipType.Fighter, ".\\skins\\fighter_skin.png").Image);
            _skins.Add(ShipType.Capital, new ShipSkin(ShipType.Capital, ".\\skins\\capital_skin.png").Image);
            _skins.Add(ShipType.Raider, new ShipSkin(ShipType.Raider, ".\\skins\\raider_skin.png").Image);
            _skins.Add(ShipType.RepairRig, new ShipSkin(ShipType.RepairRig, ".\\skins\\repairrig_skin.png").Image);
            _skins.Add(ShipType.Bomber, new ShipSkin(ShipType.Bomber, ".\\skins\\bomber_skin.png").Image);              // not used, but need the entry to prevent failure with SkinImage call.
            _skins.Add(ShipType.Transport, new ShipSkin(ShipType.Transport, ".\\skins\\transport_skin.png").Image);     // not used, but need the entry to prevent failure with SkinImage call.
        }
        /// <summary>
        /// Retrieves the skin image associated with the specified ship type.
        /// </summary>
        /// <param name="sType">The type of ship for which to retrieve the corresponding skin image.</param>
        /// <returns>An image representing the skin for the specified ship type.</returns>
        public static Image SkinImage(ShipType sType) => _skins[sType];

        /// <summary>
        /// Represents the visual appearance and associated image for a specific ship type.
        /// </summary>
        /// <remarks>A ShipSkin encapsulates the image and file path used to visually represent a ship of
        /// a given type. If the specified image file does not exist, a default image is used or generated. This class
        /// is intended for internal use to manage ship skin resources.</remarks>
        private class ShipSkin
        {
            // Constants for skin image management
            const string SKINS_DIRECTORY = ".\\skins\\";
            // Default image path for missing skins
            const string DEFAULT_IMAGE_PATH = $"{SKINS_DIRECTORY}unknown.png";
            // Default font for generating placeholder images when skin files are missing
            private static readonly Font DEFAULT_FONT = new Font("Verdana", 12, FontStyle.Regular, GraphicsUnit.Pixel);
            /// <summary>
            /// Gets the type of ship associated with this instance.
            /// </summary>
            public ShipType ShipType { get; }
            /// <summary>
            /// Gets the file system path to the associated image.
            /// </summary>
            public string ImagePath { get; }
            /// <summary>
            /// Gets the image associated with this instance.
            /// </summary>
            public Image Image { get; }
            /// <summary>
            /// Initializes a new instance of the ShipSkin class using the specified ship type and image path.
            /// </summary>
            /// <remarks>If the specified image file does not exist, a default image is used or
            /// generated. The image path is adjusted to reside within the skins directory if necessary.</remarks>
            /// <param name="sType">The type of ship for which the skin is being created.</param>
            /// <param name="imagePath">The file path to the image representing the ship skin. Must not be null, empty, or consist only of
            /// whitespace.</param>
            /// <exception cref="ArgumentNullException">Thrown if imagePath is null, empty, or consists only of whitespace.</exception>
            public ShipSkin(ShipType sType, string imagePath)
            {
                // Validate input parameters and initialize properties
                this.ShipType = sType;
                // Ensure the image path is valid and load the image, using a default if necessary
                if (string.IsNullOrWhiteSpace(imagePath))
                    throw new ArgumentNullException(nameof(imagePath), "Image path cannot be null or whitespace.");

                // Ensure the image path is within the skins directory and load the image, using a default if necessary
                var fi = new FileInfo(imagePath);
                // If the provided image path does not start with the expected skins directory, adjust it to point to the correct location
                if (!imagePath.ToLower().StartsWith(SKINS_DIRECTORY))
                {
                    // Combine the provided image file name with the skins directory to construct the correct path
                    imagePath = Path.Combine(SKINS_DIRECTORY, fi.Name);
                    // Update the FileInfo object to reflect the new path
                    fi = new FileInfo(imagePath);
                }
                // Check if the image file exists at the specified path. 
                if (!fi.Exists)
                {
                    // Image file does not exist, use a default image.
                    imagePath = DEFAULT_IMAGE_PATH;
                    // Check if the default image file exists. If it does, load it
                    if (File.Exists(imagePath))
                        this.Image = Image.FromFile(imagePath);
                    else
                    {
                        // otherwise, generate a placeholder image with a "?" symbol
                        this.Image = ModImg.TxtToImg("?", DEFAULT_FONT, Color.Yellow, Color.Black);
                        // save it to the default path for future use.
                        this.Image.Save(imagePath);
                    }
                }
                else
                {
                    // Image file exists, load it directly.
                    imagePath = fi.FullName;
                    // Load the image from the specified file path.
                    this.Image = Image.FromFile(fi.FullName);
                }

                this.ImagePath = imagePath;
            }
        }
    }
}
