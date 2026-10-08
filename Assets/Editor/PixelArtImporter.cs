using UnityEditor;
using UnityEngine;

namespace MoonlightPost.EditorTools
{
    /// <summary>
    /// Assets/Resources/Art 아래의 그림을 픽셀 아트용 설정으로 자동 임포트한다.
    /// (Point 필터, 무압축, 1 유닛 = 16 픽셀, 크기 변형 없음)
    /// 설정이 이상하면 메뉴 「달빛 우체국 > 아트 다시 가져오기」를 누른다.
    /// </summary>
    public class PixelArtImporter : AssetPostprocessor
    {
        const string ArtRoot = "Assets/Resources/Art";

        public override uint GetVersion() => 2;

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(ArtRoot)) return;
            var ti = (TextureImporter)assetImporter;
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.spritePixelsPerUnit = 16;
            ti.filterMode = FilterMode.Point;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.mipmapEnabled = false;
            ti.npotScale = TextureImporterNPOTScale.None;
            ti.alphaIsTransparency = true;
            ti.wrapMode = TextureWrapMode.Clamp;
        }

        void OnPreprocessAudio()
        {
            if (!assetPath.StartsWith(ArtRoot)) return;
            var ai = (AudioImporter)assetImporter;
            var settings = ai.defaultSampleSettings;
            // 음악은 스트리밍, 효과음은 메모리에 미리 올린다.
            settings.loadType = assetPath.Contains("/Music/") ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
            ai.defaultSampleSettings = settings;
        }

        [MenuItem("달빛 우체국/아트 다시 가져오기")]
        static void ReimportArt()
        {
            AssetDatabase.ImportAsset(ArtRoot, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ImportRecursive);
            Debug.Log("[달빛 우체국] 아트를 다시 가져왔습니다.");
        }
    }
}
