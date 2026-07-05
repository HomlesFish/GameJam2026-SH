using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BtnSetActive : MonoBehaviour
{
    public GameObject activeObject;


    public void OnSetActive()
    {
        activeObject.SetActive(!activeObject.activeInHierarchy);
    }
}
