using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using TMPro;

public class NasaGalleryManager : MonoBehaviour
{
    [Header("Configuración de APIs")]
    [SerializeField] private string usersApiUrl = "https://my-json-server.typicode.com/Nicofon1/SII_entrega1/users";
    private const string NasaSearchUrl = "https://images-api.nasa.gov/search?nasa_id=";

    [Header("Pantallas (Menús)")]
    [SerializeField] private GameObject screenUsers;    // GameObject 'Users'
    [SerializeField] private GameObject screenGallery;  // GameObject 'Gallery'

    [Header("Pantalla Users - Tripulantes")]
    [SerializeField] private UserCardUI[] userCards; // Tamaño 5: Explorer, Astronaut, Astronomer, Commander, Pilot

    [Header("Pantalla Gallery - Elementos")]
    [Tooltip("Asigna las 5 imágenes en orden visual de izquierda a derecha (Lejano Izq, Medio Izq, Centro, Medio Der, Lejano Der)")]
    [SerializeField] private Image[] galleryImageSlots; // Image (3), Image (4), Image (1), Image (2), Image
    [SerializeField] private TextMeshProUGUI txtTitulo;        // Titulo
    [SerializeField] private TextMeshProUGUI txtDescripcion;   // Descripcion
    [SerializeField] private Button btnBack;                   // Back (Flecha Izquierda)
    [SerializeField] private Button btnFoward;                 // Foward (Flecha Derecha)
    [SerializeField] private Button btnExit;                   // Exit (Botón Planeta para volver)

    // Estado interno
    private List<UserInfo> usersList = new List<UserInfo>();
    private UserInfo selectedUser;
    private int currentCenterIndex = 0;

    // Cache de datos de la NASA para el deck activo (evita reconsultar la API al rotar)
    private Dictionary<string, NasaAssetData> loadedNasaAssets = new Dictionary<string, NasaAssetData>();

    void Start()
    {
        // Configurar botones de navegación
        if (btnBack != null) btnBack.onClick.AddListener(PrevAsset);
        if (btnFoward != null) btnFoward.onClick.AddListener(NextAsset);
        if (btnExit != null) btnExit.onClick.AddListener(ShowUsersScreen);

        ShowUsersScreen();
        StartCoroutine(FetchUsers());
    }

    #region Control de Pantallas

    public void ShowUsersScreen()
    {
        if (screenUsers != null) screenUsers.SetActive(true);
        if (screenGallery != null) screenGallery.SetActive(false);
    }

    public void ShowGalleryScreen(UserInfo user)
    {
        selectedUser = user;
        currentCenterIndex = 0;
        loadedNasaAssets.Clear();

        if (screenUsers != null) screenUsers.SetActive(false);
        if (screenGallery != null) screenGallery.SetActive(true);

        // Cargar todos los elementos del deck del usuario seleccionado
        StartCoroutine(LoadDeckNasaData());
    }

    #endregion

    #region Petición de Usuarios (JSON Server)

    IEnumerator FetchUsers()
    {
        using (UnityWebRequest req = UnityWebRequest.Get(usersApiUrl))
        {
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"<color=red>[ERROR USERS]</color> {req.responseCode} - {req.error}");
            }
            else
            {
                // Envolver el arreglo JSON para compatibilidad con JsonUtility
                string jsonWrapped = "{\"users\":" + req.downloadHandler.text + "}";
                UserListWrapper wrapper = JsonUtility.FromJson<UserListWrapper>(jsonWrapped);
                usersList = new List<UserInfo>(wrapper.users);

                SetupUsersUI();
            }
        }
    }

    private void SetupUsersUI()
    {
        for (int i = 0; i < userCards.Length; i++)
        {
            if (i < usersList.Count)
            {
                UserInfo user = usersList[i];
                userCards[i].cardRoot.SetActive(true);

                if (userCards[i].nameText != null)
                    userCards[i].nameText.text = user.username;

                // Descargar avatar del astronauta/usuario
                if (userCards[i].avatarImage != null)
                    StartCoroutine(DownloadTexture(user.img, userCards[i].avatarImage, user.username));

                // Configurar click para abrir la galería
                userCards[i].actionButton.onClick.RemoveAllListeners();
                userCards[i].actionButton.onClick.AddListener(() => ShowGalleryScreen(user));
            }
            else
            {
                userCards[i].cardRoot.SetActive(false);
            }
        }
    }

    #endregion

    #region Peticiones NASA API & Carrusel

    IEnumerator LoadDeckNasaData()
    {
        if (selectedUser == null || selectedUser.deck == null || selectedUser.deck.Length == 0)
            yield break;

        if (txtTitulo != null) txtTitulo.text = "Cargando archivos...";
        if (txtDescripcion != null) txtDescripcion.text = "Consultando base de datos de la NASA...";

        // Consultar metadatos e imágenes para cada nasa_id del deck
        foreach (string nasaId in selectedUser.deck)
        {
            if (!loadedNasaAssets.ContainsKey(nasaId))
            {
                yield return StartCoroutine(FetchNasaAsset(nasaId));
            }
        }

        UpdateGalleryView();
    }

    IEnumerator FetchNasaAsset(string nasaId)
    {
        string url = NasaSearchUrl + UnityWebRequest.EscapeURL(nasaId);

        using (UnityWebRequest req = UnityWebRequest.Get(url))
        {
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"<color=red>[ERROR NASA API]</color> ID: {nasaId} | {req.error}");
            }
            else
            {
                NasaSearchResponse res = JsonUtility.FromJson<NasaSearchResponse>(req.downloadHandler.text);

                if (res != null && res.collection != null && res.collection.items != null && res.collection.items.Length > 0)
                {
                    var item = res.collection.items[0];
                    NasaAssetData asset = new NasaAssetData();

                    if (item.data != null && item.data.Length > 0)
                    {
                        asset.title = item.data[0].title;
                        asset.description = item.data[0].description;
                        asset.nasaId = item.data[0].nasa_id;
                    }

                    if (item.links != null && item.links.Length > 0)
                    {
                        asset.imageUrl = item.links[0].href;
                    }

                    loadedNasaAssets[nasaId] = asset;
                }
            }
        }
    }

    private void UpdateGalleryView()
    {
        if (selectedUser == null || selectedUser.deck.Length == 0) return;

        int total = selectedUser.deck.Length;

        // Distribución cíclica para los 5 slots visuales: [-2, -1, 0, +1, +2]
        for (int i = 0; i < 5 && i < galleryImageSlots.Length; i++)
        {
            int offset = i - 2; // -2 (Far Left), -1 (Mid Left), 0 (Center), 1 (Mid Right), 2 (Far Right)
            int itemIndex = (currentCenterIndex + offset) % total;
            if (itemIndex < 0) itemIndex += total;

            string nasaId = selectedUser.deck[itemIndex];

            if (loadedNasaAssets.TryGetValue(nasaId, out NasaAssetData asset))
            {
                // Descargar/Asignar la imagen al slot
                if (galleryImageSlots[i] != null && !string.IsNullOrEmpty(asset.imageUrl))
                {
                    StartCoroutine(DownloadTexture(asset.imageUrl, galleryImageSlots[i], $"Slot_{i}_{nasaId}"));
                }

                // Si es el elemento central (offset == 0), actualizar textos principales
                if (offset == 0)
                {
                    if (txtTitulo != null) txtTitulo.text = asset.title;
                    if (txtDescripcion != null) txtDescripcion.text = asset.description;
                }
            }
        }
    }

    public void NextAsset()
    {
        if (selectedUser == null || selectedUser.deck.Length == 0) return;
        currentCenterIndex = (currentCenterIndex + 1) % selectedUser.deck.Length;
        UpdateGalleryView();
    }

    public void PrevAsset()
    {
        if (selectedUser == null || selectedUser.deck.Length == 0) return;
        currentCenterIndex = (currentCenterIndex - 1 + selectedUser.deck.Length) % selectedUser.deck.Length;
        UpdateGalleryView();
    }

    #endregion

    #region Utilidad de Descarga de Imágenes

    IEnumerator DownloadTexture(string url, Image targetImage, string tag = "Asset")
    {
        if (string.IsNullOrEmpty(url) || targetImage == null) yield break;

        using (UnityWebRequest req = UnityWebRequestTexture.GetTexture(url.Trim()))
        {
            req.redirectLimit = 5;
            req.SetRequestHeader("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64)");

            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"[DESCARGA FALLIDA - {tag}] Code: {req.responseCode} | Error: {req.error}");
            }
            else
            {
                Texture2D texture = DownloadHandlerTexture.GetContent(req);
                if (texture != null)
                {
                    // Fotografías reales de la NASA: Bilinear para máxima calidad y suavizado natural
                    texture.filterMode = FilterMode.Bilinear;
                    texture.wrapMode = TextureWrapMode.Clamp;

                    Sprite sprite = Sprite.Create(
                        texture,
                        new Rect(0, 0, texture.width, texture.height),
                        new Vector2(0.5f, 0.5f)
                    );

                    targetImage.sprite = sprite;
                    targetImage.color = Color.white;
                    targetImage.preserveAspect = true;
                }
            }
        }
    }

    #endregion
}

#region Estructuras de Datos y Clases Serializables

[Serializable]
public class UserCardUI
{
    public GameObject cardRoot;
    public TextMeshProUGUI nameText;
    public Image avatarImage;
    public Button actionButton;
}

public class NasaAssetData
{
    public string nasaId;
    public string title;
    public string description;
    public string imageUrl;
}

// JSON Wrappers para Fake API
[Serializable]
public class UserListWrapper
{
    public UserInfo[] users;
    public int[] worlds;
}

[Serializable]
public class UserInfo
{
    public int id;
    public string username;
    public bool state;
    public string img;
    public string[] deck;
}

// JSON Wrappers para NASA API
[Serializable]
public class NasaSearchResponse
{
    public NasaCollection collection;
}

[Serializable]
public class NasaCollection
{
    public NasaItem[] items;
}

[Serializable]
public class NasaItem
{
    public NasaData[] data;
    public NasaLink[] links;
}

[Serializable]
public class NasaData
{
    public string title;
    public string description;
    public string nasa_id;
}

[Serializable]
public class NasaLink
{
    public string href;
    public string rel;
    public string render;
}

#endregion