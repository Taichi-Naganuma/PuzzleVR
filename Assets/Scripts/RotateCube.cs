using UnityEngine;

public class RotateCube : MonoBehaviour
{
    [SerializeField] private float speed = 90f;

    void Update()
    {
        // Time.deltaTime で掛け算することで、fps に関係なく「毎秒 speed 度」回転する
        transform.Rotate(Vector3.up, speed * Time.deltaTime);
    }
}
