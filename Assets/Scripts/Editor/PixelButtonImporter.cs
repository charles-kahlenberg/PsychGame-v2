using UnityEditor;

// Imports the pixel-art buttons in Resources/PixelButtons (drawn by
// Tools/PixelButtons/make_pixel_buttons.py) as UI sprites: no mipmaps, which
// would blur them, and uncompressed, so their colours stay exact.
public class PixelButtonImporter : AssetPostprocessor
{
    private const string Folder = "Assets/Resources/PixelButtons/";

    private void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith(Folder)) return;

        var importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
    }
}
