using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

public class HttpTest : MonoBehaviour
{
    private string FakeApiUrl = "https://my-json-server.typicode.com/manolovillarreal/jsonDB/";

    private string Url = "https://rickandmortyapi.com/api/character/";

    //private int characterId = Mathf.Clamp(315, 1, 826); // Clamp the character ID to be between 1 and 826


    void Start()
    {
        StartCoroutine(GetUserProfile(1));
    }

    public void GetCharacterButtonClick()
    {
        int newcharacterId = Mathf.Clamp(Random.Range(1, 827), 1, 826); // Clamp the character ID to be between 1 and 826
        StartCoroutine(GetCharacter(newcharacterId));
    }
    IEnumerator GetUserProfile(int userId)
    {
        UnityWebRequest www = UnityWebRequest.Get(FakeApiUrl +"/users/"+userId);
        yield return www.SendWebRequest();

        if (www.result != UnityWebRequest.Result.Success)
        {
            Debug.Log(www.error);
        }
        else
        {
            Debug.Log(www.downloadHandler.text);            
            UserInfo userInfo = JsonUtility.FromJson<UserInfo>(www.downloadHandler.text);

            foreach (int cardId in userInfo.deck)
            {
                StartCoroutine(GetCharacter(cardId));
                
            }

        }
    }
    IEnumerator GetCharacter(int characterId)
    {
        UnityWebRequest www = UnityWebRequest.Get(Url+ characterId);
        yield return www.SendWebRequest();

        if (www.result != UnityWebRequest.Result.Success)
        {
            Debug.Log(www.error);
        }
        else
        {
            // Show results as text
            Character character = JsonUtility.FromJson<Character>(www.downloadHandler.text);
            StartCoroutine(GetImage(character.image));            

        }
    }
    IEnumerator GetImage(string imageUrl)
    {
        UnityWebRequest www = UnityWebRequestTexture.GetTexture(imageUrl);
        yield return www.SendWebRequest();
        if (www.result != UnityWebRequest.Result.Success)
        {
            Debug.Log(www.error);
        }
        else
        {
            Texture2D texture = DownloadHandlerTexture.GetContent(www);
            GameObject.Find("RawImage").GetComponent<RawImage>().texture = texture;
            // Do something with the texture, e.g., apply it to a material
        }
    }
}

public class UserInfo
{
    public int id;
    public string username;
    public bool state;
    public int[] deck;
}
public class Character
{
    public int id;
    public string name;
    public string species;
    public string image;

}