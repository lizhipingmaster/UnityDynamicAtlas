using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

[RequireComponent(typeof(Tilemap))]
public class OnCameraChange : MonoBehaviour
{
    private bool show;

    [DependAttirbute("show")]
    private int a;

    [DependAttirbute("show")]
    public Camera mainCamera;


    public Tilemap tm;

    // Start is called before the first frame update
    void Start()
    {
        mainCamera = GameObject.FindGameObjectWithTag("MainCamera").GetComponent<Camera>();
        tm = GetComponent<Tilemap>();

        //Debug.Log(Matrix4x4.Rotate(mainCamera.transform.rotation));
        //Debug.Log(tm.orientationMatrix);
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
