using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

//[ExecuteInEditMode]
public class Script {

    public static bool bCloseServer = false;
    // Use this for initialization

    static float startTime = 0;
    static List<Scene> openedScene = new List<Scene>();

    static List<Scene> loadedScene = new List<Scene>();
    static int loadedCount = 0;
    static float delaySecs = 4;

    static Predicate<GameObject> findAgent = (gameObject) =>
        {
            return gameObject.name == "HumanoidAgent";
        };

    public static void StartServer()
    {
#if UNITY_EDITOR
        Debug.Log("Run in editor mode");
#else
        Debug.Log("Run in play mode");
#endif
        startTime = Time.realtimeSinceStartup;
        EditorApplication.update += FrameUpdate;// () =>        ;

        EditorSceneManager.OpenScene(Application.dataPath + "/Examples/Scenes/1_multiple_agent_sizes.unity");
        //SceneManager.LoadScene(Application.dataPath + "/Examples/Scenes/start.unity");
        //SceneManager.LoadScene(0);

        EditorSceneManager.sceneOpened += (scene, mode) =>
        {
            Debug.Log("sceneOpened");
            openedScene.Add(scene);
            /*foreach(var rootGo in scene.GetRootGameObjects())
            {
                Debug.Log(rootGo.name);
            }*/
            var go = scene.GetRootGameObjects().Find(findAgent);

            if (go != null)
            {
                go.GetComponent<NavMeshAgent>().destination = new Vector3(13.0f, 0.2f, -2.6f);
            }
        };

        EditorSceneManager.sceneClosing += (scene, removingScene) =>
        {
            Debug.Log("sceneClosing");
            openedScene.Remove(scene);
        };
    }

    private static void FrameUpdate()
    {
        /*foreach(var scene in openedScene)
        {
            var go = scene.GetRootGameObjects().Find(findAgent);

            if (go != null)
            {
                Debug.Log(go.transform.localPosition);
            }
        }*/
        if (EditorSceneManager.loadedSceneCount > loadedCount)
        {
            for(int i = loadedCount; i < EditorSceneManager.loadedSceneCount; ++i)
            {
                loadedScene.Add(EditorSceneManager.GetSceneAt(i));

                var go = loadedScene[loadedScene.Count - 1].GetRootGameObjects().Find(findAgent);

                if (go != null)
                {
                    Debug.Log(go.name);
                    var navAgent = go.GetComponent<NavMeshAgent>();
                    Debug.Log(navAgent.isOnNavMesh);
                    navAgent.destination = new Vector3(13.0f, 0.2f, -2.6f);
                }
            }

            loadedCount = EditorSceneManager.loadedSceneCount;
        }
        for(int i = 0; i < EditorSceneManager.loadedSceneCount; ++i)
        {
            var go = EditorSceneManager.GetSceneAt(i).GetRootGameObjects().Find(findAgent);

            if (go != null)
            {
                /*if (Time.realtimeSinceStartup - startTime > delaySecs)
                {
                    go.GetComponent<NavMeshAgent>().destination = new Vector3(13.0f, 0.2f, -2.6f);
                    Debug.Log(go.GetComponent<NavMeshAgent>().isOnNavMesh);
                    delaySecs = Mathf.Infinity;
                }*/

                Debug.Log(go.transform.localPosition);
            }
        }
        

        if (Time.realtimeSinceStartup - startTime > 6)
        {
            Debug.Log("Now closing...");
            EditorApplication.Exit(0);
            //DelayClose();
        }
    }
}
