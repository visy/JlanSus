using System.Collections;
using UnityEngine;
 
class DragAndDrop : MonoBehaviour
{
    private Color mouseOverColor = Color.white;
    private Color originalColor = Color.red;
    private Color goalColor = Color.green;
    public GameObject goalObject; 
    private bool dragging = false;
    private bool onGoal = false;
    private float distance;

    void Start()
    {
        GetComponent<SpriteRenderer>().material.color = originalColor;
    }

    void OnMouseEnter()
    {
        if (!onGoal) GetComponent<SpriteRenderer>().material.color = mouseOverColor;
    }
 
    void OnMouseExit()
    {
        if (onGoal) GetComponent<SpriteRenderer>().material.color = goalColor;
        else GetComponent<SpriteRenderer>().material.color = originalColor;
    }
 
    void OnMouseDown()
    {
        if (!onGoal) {
            distance = Vector3.Distance(transform.position, Camera.main.transform.position);
            dragging = true;
        }
    }
 
    void OnMouseUp()
    {
        dragging = false;
    }

    void OnCollisionEnter2D(Collision2D col)
    {
        if (col.gameObject == goalObject && Vector2.Distance(col.gameObject.transform.position,transform.position) < 4.5)
        {
            GetComponent<Rigidbody2D>().constraints = RigidbodyConstraints2D.FreezeAll;
            var pos = col.gameObject.transform.position;
            transform.position = pos;
            transform.rotation = col.gameObject.transform.rotation;
            col.gameObject.SetActive(false);

            onGoal = true;
        }
    }

    void OnCollisionExit2D(Collision2D col)
    {
        if (col.gameObject == goalObject)
        {
            onGoal = false;
        }
    }

    void Update()
    {
        if (dragging && !onGoal)
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            Vector3 rayPoint = ray.GetPoint(distance);
            rayPoint.z = 0.9f;
            transform.position = rayPoint;
        }
    }
}