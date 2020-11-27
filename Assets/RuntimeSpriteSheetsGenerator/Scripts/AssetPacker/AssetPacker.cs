using DaVikingCode.RectanglePacking;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Events;
using System.Linq;
using UnityEngine.Networking;
using System.Reflection;



namespace DaVikingCode.AssetPacker
{
    public class Tex2DPath
    {
        public Texture2D t2d;
        public string path;
    }

    public class Page
    {
        public Texture2D texture = null;
        public int maxId = 0;
        public List<string> spriteName = new List<string>();    // 方便移除
        public RectanglePacker packer = null;      //判断图片能不能放进来

        public Page(Texture2D t, RectanglePacker p)
        {
            texture = t;
            packer = p;
        }
    }

    public enum AtlasSize
    {
        [EnumAttirbute("编辑器中勿选")]
        POT_0 = 0,
        [EnumAttirbute("512x512")]
        POT_9 = 2 << 9,
        [EnumAttirbute("1024x1024")]
        POT_10 = 2 << 10,
        [EnumAttirbute("2048x2048")]
        POT_11 = 2 << 11,
        [EnumAttirbute("4096x4096")]
        POT_12 = 2 << 12,
    }

    public class AssetPacker : MonoBehaviour
    {
        public static AssetPacker Instance = null;
        [HideInInspector]
        public UnityEvent OnProcessCompleted;

        public float pixelsPerUnit = 100.0f;

#if RUNTIME_ATLAS_CACHE
        [HideInInspector]
        public bool useCache = false;
        [HideInInspector]
        [DependAttirbute("useCache")]
        public string cacheName = "";
        [HideInInspector]
        [DependAttirbute("useCache")]
        public int cacheVersion = 1;
        [HideInInspector]
        [DependAttirbute("useCache")]
        public bool deletePreviousCacheVersion = true;
#endif
        protected Dictionary<string, Sprite> mSprites = new Dictionary<string, Sprite>();
        protected List<string> itemsToRaster = new List<string>();
        protected List<Page> pages = new List<Page>(4);

        [EnumAttirbute("图集大小")]
        public AtlasSize atlasSize = AtlasSize.POT_11;

        //protected bool allow4096Textures = false;
        [Range(1, 4)]
        public int padding = 1;

        private Material material;
        public void Awake()
        {
            Instance = this;
        }

        public void Start()
        {
#if UNITY_EDITOR
            var files = Directory.GetFiles(Application.dataPath + "/Res/Wall", "*.png", SearchOption.TopDirectoryOnly);
            AddTexturesToPack(files);
            Process();
#endif
        }

        public void AddTextureToPack(string filePath, Texture2D t)
        {
            List<Tex2DPath> add = new List<Tex2DPath>(1);
            Tex2DPath _1 = new Tex2DPath
            {
                t2d = t,
                // TODO路径优化
                path = filePath
            };

            add.Add(_1);
            Pack(add);
        }

        /*
         * 需要调用Process
         */
        public void AddTexturesToPack(string[] files)
        {
            foreach (string file in files)
                itemsToRaster.Add(file);
        }

        /*
         * 需要调用Process
         */
        public void AddTextureToPack(string file)
        {
            itemsToRaster.Add(file);
        }

        public void Process(/*bool allow4096Textures = false*/AtlasSize sizeOverride = AtlasSize.POT_0)
        {
            //this.allow4096Textures = allow4096Textures;
#if RUNTIME_ATLAS_CACHE
            if (useCache)
            {

                if (cacheName == "")
                    throw new Exception("No cache name specified");

                string path = Application.persistentDataPath + "/AssetPacker/" + cacheName + "/" + cacheVersion + "/";

                bool cacheExist = Directory.Exists(path);

                if (!cacheExist)
                    StartCoroutine(LoadTexture_Pack(path));
                else
                    StartCoroutine(LoadCache(path));

            }
            else
#endif
                StartCoroutine(LoadTexture_Pack());

        }
        
        protected IEnumerator LoadTexture_Pack(string savePath = "")
        {
#if RUNTIME_ATLAS_CACHE
            if (savePath != "")
            {
                if (deletePreviousCacheVersion && Directory.Exists(Application.persistentDataPath + "/AssetPacker/" + cacheName + "/"))
                    foreach (string dirPath in Directory.GetDirectories(Application.persistentDataPath + "/AssetPacker/" + cacheName + "/", "*", SearchOption.AllDirectories))
                        Directory.Delete(dirPath, true);

                Directory.CreateDirectory(savePath);
            }
#endif
            List<Tex2DPath> add = new List<Tex2DPath>(itemsToRaster.Count);
            foreach (var streamPath in itemsToRaster)
            {
                var req = UnityWebRequestTexture.GetTexture("file:///" + streamPath);
                req.downloadHandler = new DownloadHandlerTexture();
                yield return req.SendWebRequest();

                Tex2DPath _1 = new Tex2DPath
                {
                    t2d = (req.downloadHandler as DownloadHandlerTexture).texture,
                    // TODO路径优化
                    path = streamPath
                };
                req.Dispose();

                add.Add(_1);
            }
            itemsToRaster.Clear();

            Pack(add, savePath);

            OnProcessCompleted.Invoke();
        }

        private void Pack(List<Tex2DPath> add, string savePath = "")
        {
            int textureSize = (int)atlasSize;
            // TODO警告
            add.RemoveAll(t => { return t.t2d.width > textureSize || t.t2d.height > textureSize; });

            bool reuse = true;
            while (add.Count > 0)
            {
                // 使用最后一页或创建一个新的
                var useLast = pages.Count > 0 && reuse;
                Page page = useLast ? pages.Last() : new Page(GenEmptyTexture(textureSize), new RectanglePacker(textureSize, textureSize/*宽高一样*/, padding));
                if (!useLast) pages.Add(page);

                var texture = page.texture;
                RectanglePacker packer = page.packer;
                int t2dIndex = 0;
                add.ForEach(t2d => { packer.insertRectangle(t2d.t2d.width, t2d.t2d.height, t2dIndex++); });

                packer.packRectangles();

                IntegerRectangle rect = new IntegerRectangle();
                List<TextureAsset> textureAssets = new List<TextureAsset>();// 序列化索引

                List<int> indexes = new List<int>(packer.rectangleCount);
                while (page.maxId < packer.rectangleCount)
                {
                    int index = packer.getRectangleId(page.maxId);
                    rect = packer.getRectangle(page.maxId, rect);

                    // TODO更高效的图像负责算法
                    //texture.SetPixels32(rect.x, rect.y, rect.width, rect.height, add[index].t2d.GetPixels32());
                    Graphics.CopyTexture(add[index].t2d, 0, 0, 0, 0, rect.width, rect.height, texture, 0, 0, rect.x, rect.y);

                    TextureAsset textureAsset = new TextureAsset();
                    textureAsset.x = rect.x;
                    textureAsset.y = rect.y;
                    textureAsset.width = rect.width;
                    textureAsset.height = rect.height;
                    textureAsset.name = Path.GetFileNameWithoutExtension(add[index].path);

                    textureAssets.Add(textureAsset);

                    indexes.Add(index);
                    page.maxId++;
                }

                // 由大到小
                indexes.Sort((l, r) => { return r - l; });
                foreach (var index in indexes)
                {
                    add.RemoveAt(index);
                }
                texture.Apply();    // 必须的吗?
#if RUNTIME_ATLAS_CACHE
                if (savePath != "")
                {
                    File.WriteAllBytes(savePath + "/data" + (pages.Count - 1) + ".png", texture.EncodeToPNG());
                    File.WriteAllText(savePath + "/data" + (pages.Count - 1) + ".json", JsonUtility.ToJson(new TextureAssets(textureAssets.ToArray())));
                }
#endif
                foreach (TextureAsset textureAsset in textureAssets)
                    mSprites.Add(textureAsset.name, Sprite.Create(texture, new Rect(textureAsset.x, textureAsset.y, textureAsset.width, textureAsset.height), Vector2.zero, pixelsPerUnit, 0, SpriteMeshType.FullRect));

                // 塞不下，准备创建一个新的
                reuse = false;
            }
        }

        private Texture2D GenEmptyTexture(int textureSize)
        {
            // 可能一张图片塞不下，需要多张图片
            var texture = new Texture2D(textureSize, textureSize,
#if UNITY_ANDROID
                    TextureFormat.ETC2_RGBA8
#elif UNITY_IOS
                    TextureFormat.PVRTC_RGBA4
#else
                    TextureFormat.ARGB32
#endif
                    , false);
            //Color32[] fillColor = texture.GetPixels32();
            //for (int i = 0; i < fillColor.Length; ++i)
            //    fillColor[i] = Color.clear;

            //texture.SetPixels32(fillColor); // 清空
            return texture;
        }

        public void SetTexture(Texture2D texture, Rect rect, RenderTexture dstTexture)
        {
            if (material == null)
            {
                material = new Material(Shader.Find("UI/Default"));
            }

            material.SetTexture("_MainTex", texture);
            if (material.SetPass(0))
            {
                Graphics.SetRenderTarget(dstTexture);
                //Graphics.SetRenderTarget(mainTexture);

                GL.PushMatrix();
                GL.LoadOrtho();
                GL.Begin(GL.QUADS);
                {
                    Vector3 vertex1 = new Vector3(rect.x, rect.y, 0);
                    Vector3 vertex2 = new Vector3(rect.x, rect.y + rect.height, 0);
                    Vector3 vertex3 = new Vector3(rect.x + rect.width, rect.y + rect.height, 0);
                    Vector3 vertex4 = new Vector3(rect.x + rect.width, rect.y, 0);

                    GL.TexCoord2(0, 0);
                    GL.Vertex(vertex1);

                    GL.TexCoord2(0, 1);
                    GL.Vertex(vertex2);

                    GL.TexCoord2(1, 1);
                    GL.Vertex(vertex3);

                    GL.TexCoord2(1, 0);
                    GL.Vertex(vertex4);
                }
                GL.End();
                GL.PopMatrix();
            }
            //material.SetTexture(MainTex_ID, mainTexture);
            //image.material = material;
        }

#if RUNTIME_ATLAS_CACHE
        protected IEnumerator LoadCache(string savePath)
        {
            // 可写目录
            int numFiles = Directory.GetFiles(savePath).Length;

            for (int i = 0; i < numFiles / 2; ++i)
            {

                var req = UnityWebRequest.Get("file:///" + savePath + "/data" + i + ".json");
                yield return req.SendWebRequest();
                TextureAssets textureAssets = JsonUtility.FromJson<TextureAssets>(req.downloadHandler.text);
                req.Dispose();

                req = UnityWebRequestTexture.GetTexture("file:///" + savePath + "/data" + i + ".png");
                req.downloadHandler = new DownloadHandlerTexture();
                yield return req.SendWebRequest();
                var t = (req.downloadHandler as DownloadHandlerTexture).texture;
                req.Dispose();

                foreach (TextureAsset textureAsset in textureAssets.assets)
                    mSprites.Add(textureAsset.name, Sprite.Create(t, new Rect(textureAsset.x, textureAsset.y, textureAsset.width, textureAsset.height), Vector2.zero, pixelsPerUnit, 0, SpriteMeshType.FullRect));
            }

            yield return null;
            OnProcessCompleted.Invoke();
        }
#endif

        public void Dispose()
        {

            // 会释放多次吗？
            foreach (var asset in mSprites)
                Destroy(asset.Value.texture);

            mSprites.Clear();
        }

        void Destroy()
        {
            Dispose();
        }

        public Sprite GetSprite(string id)
        {

            mSprites.TryGetValue(id, out Sprite sprite);
            return sprite;
        }

        public Sprite[] GetSprites(string prefix)
        {
            var spriteNames = (from key in mSprites.Keys where key.StartsWith(prefix) select key).ToList();

            spriteNames.Sort(StringComparer.Ordinal);

            List<Sprite> sprites = new List<Sprite>();
            for (int i = 0; i < spriteNames.Count; ++i)
            {
                mSprites.TryGetValue(spriteNames[i], out Sprite sprite);
                sprites.Add(sprite);
            }

            return sprites.ToArray();
        }
    }

}
