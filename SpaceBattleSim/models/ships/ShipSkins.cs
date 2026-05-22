using Chizl.DrawGraphics;
using System.Collections.Concurrent;

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
        // Constants for skin image management
        public const string SKINS_DIRECTORY = ".\\skins\\";
        // Default font for generating placeholder images when skin files are missing
        public static readonly Font DEFAULT_FONT = new Font("Verdana", 12, FontStyle.Regular, GraphicsUnit.Pixel);

        /// <summary>
        /// Stores the mapping between ship types and their associated images used for rendering ship skins.
        /// </summary>
        /// <remarks>Each entry associates a specific ShipType with its corresponding Image. This
        /// dictionary enables efficient retrieval of the correct image for a given ship type when rendering or updating
        /// ship appearances.</remarks>
        private static ConcurrentDictionary<ShipType, Image> _skins = new ConcurrentDictionary<ShipType, Image>();
        /// <summary>
        /// Initializes the static data for the ShipSkins class by loading predefined ship skin images for each ship
        /// type.
        /// </summary>
        /// <remarks>This static constructor ensures that all supported ship types have an associated skin
        /// image loaded from the specified file paths. Entries for unused ship types are included to prevent errors
        /// when accessing skin images for those types.</remarks>
        static ShipSkins()
        {
            foreach(var sType in Enum.GetValues<ShipType>())
            {
                try
                {
                    var shipStats = new ShipStats(sType);
                    CreateAsImage(sType, DEFAULT_FONT, shipStats.ShipView, shipStats.ShipColor);
                    // do last, because we want to make sure that the skin is loaded in the dictionary before any File.Delete exeception.
                    var fileName = $"{SKINS_DIRECTORY}{(sType == ShipType.RepairRig ? "repairing" : sType.ToString().ToLower())}_skin.png";
                    if (File.Exists(fileName))
                        File.Delete(fileName); 
                }
                catch (Exception ex)
                {
                    // Log the error and continue loading other skins
                    Console.WriteLine($"Error loading skin for {sType}: {ex.Message}");
                }
            }
        }
        /// <summary>
        /// Retrieves the skin image associated with the specified ship type.
        /// </summary>
        /// <param name="sType">The type of ship for which to retrieve the corresponding skin image.</param>
        /// <returns>An image representing the skin for the specified ship type.</returns>
        public static Image SkinImage(ShipType sType) => _skins[sType];
        /// <summary>
        /// Creates and stores an image representation of the specified Unicode string for the given ship type using the
        /// provided font and foreground color.
        /// </summary>
        /// <remarks>No image is created for ShipType.Transport or ShipType.Bomber. If an image already
        /// exists for the specified ship type and updateIfExists is true, the existing image is replaced.</remarks>
        /// <param name="sType">The ship type for which the image will be created. Must not be ShipType.Transport or ShipType.Bomber.</param>
        /// <param name="font">The font to use when rendering the Unicode string as an image.</param>
        /// <param name="unicodeString">The Unicode string to render as an image.</param>
        /// <param name="fgColor">The foreground color to use for the rendered text.</param>
        /// <param name="updateIfExists">true to update the image if one already exists for the specified ship type; otherwise, false.</param>
        private static void CreateAsImage(ShipType sType, Font font, string unicodeString, Color fgColor, bool updateIfExists = false)
        {
            if (sType is ShipType.Transport or ShipType.Bomber)
                return;

            if (_skins.Keys.Contains(sType) && !updateIfExists)
                return;

            // ARGB has to be 1, because we are generating a BMP from here.
            // Maybe it's a quirk of the image rendering library that treats fully transparent images differently.
            var img = ModImg.TxtToImg(unicodeString, font, fgColor, Color.Transparent);

            if (_skins.Keys.Contains(sType))
                _skins[sType] = img;
            else
                _skins.TryAdd(sType, img);
        }
    }
}
