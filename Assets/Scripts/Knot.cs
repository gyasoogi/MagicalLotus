using System.Collections.Generic;
using UnityEngine;

public class Knot : MonoBehaviour
{
    private List<Vector3> stringPoints;
    private KnotAttack knotAttack;

    private void Start()
    {
        GameObject player =
            GameObject.FindGameObjectWithTag("Player");

        if (player != null)
        {
            knotAttack =
                player.GetComponent<KnotAttack>();
        }
    }

    public void SetStringPath(List<Vector3> points)
    {
        stringPoints = new List<Vector3>(points);
    }

    public void Activate()
    {
        if (knotAttack != null && stringPoints != null)
        {
            knotAttack.DashFromKnot(
                transform.position,
                stringPoints
            );
        }
    }
}