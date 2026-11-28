using UnityEngine;
using UnityEngine.SceneManagement;
public sealed class FanhuaReturnToMap : MonoBehaviour
{
 [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
 static void Bind(){SceneManager.sceneLoaded-=OnScene;SceneManager.sceneLoaded+=OnScene;}
 static void OnScene(Scene scene,LoadSceneMode mode)
 {if(scene.name!="FanhuaWorldMap" && Object.FindObjectOfType<FanhuaReturnToMap>()==null)new GameObject("Return to paper garden",typeof(FanhuaReturnToMap));}
 private void Update(){if(Input.GetKeyDown(KeyCode.Escape))SceneManager.LoadScene("FanhuaWorldMap");}
}
