using System.Collections;

using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

//[ExecuteInEditMode]
public class Server : MonoBehaviour {

    float startTime = 0;
	// Use this for initialization
	void Start ()
    {
        Application.targetFrameRate = 30;

/*#if UNITY_EDITOR
        EditorApplication.update += ()=>
        {
            if (Time.realtimeSinceStartup - startTime > 4)
            {
                DelayClose();
            }
        };
//#else
#endif*/
        //StartCoroutine(DelayClose());
    }
	
	// Update is called once per frame
	/*void Update () {
		if (Time.realtimeSinceStartup - startTime > 4)
        {
            DelayClose();
        }
	}*/


    public void CloseApplication()
    {
#if UNITY_EDITOR
        //EditorSceneManager.UnloadSceneAsync(EditorSceneManager.GetActiveScene());
        //Application.Quit();
        EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
        Debug.Log("Server is Closing");
    }

    IEnumerator DelayClose()
    {
        yield return new WaitForSeconds(5);
        //yield return new WaitForSeconds(2);

#if UNITY_EDITOR
        //EditorSceneManager.UnloadSceneAsync(EditorSceneManager.GetActiveScene());
        //Application.Quit();
        EditorApplication.Exit(0);
#endif
        Application.Quit();
    }
}
