using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class ObjectGrabber : MonoBehaviour
{
    // ---- / Serialized Variables / ---- //
    [SerializeField] private float maxGrabbableWeight = 50f;
    
    [Header("Grabbing Objects")]
    [SerializeField] private float grabDistance = 3f;
    [SerializeField] private float grabPointerDistance = 0.5f;
    [SerializeField] private LayerMask grabbableObjectLayer;
    [SerializeField] private Transform heldPoint;
    
    [Header("Selling Objects")]
    [SerializeField] private Transform sellPoint;
    [SerializeField] private float sellDistance = 12f;
    
    [Header("Debugging")]
    [SerializeField] private TMP_Text weightText;
    [SerializeField] private TMP_Text soldValueText;

    // ---- / Private Variables / ---- //
    private List<GameObject> _grabbedObjects = new List<GameObject>();
    private float _currentTotalWeight = 0f;
    private Camera _camera;

    private void Start()
    {
        _camera = Camera.main;
        if (!GameController.Instance.DEBUG_MODE)
        {
            weightText.gameObject.SetActive(false);
            soldValueText.gameObject.SetActive(false);
        }
    }

    private void Update()
    {
        if (!GameController.Instance.IsPlayerFrozen)
        {
            if (InputManager.WasGrabOrReleasePressed)
            {
                TryGrabOrReleaseObject();
            }
        }

        if (!GameController.Instance.IsGamePaused)
        {
            CheckIfSellPointInBounds();
        }

        if (GameController.Instance.DEBUG_MODE)
        {
            UpdateDebugGUI();
        }
    }
    
    private void TryGrabOrReleaseObject()
    {
        Ray ray = _camera.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;

        // Perform a SphereCast from the mouse pointer
        if (Physics.SphereCast(ray, grabPointerDistance, out hit, Mathf.Infinity, grabbableObjectLayer))
        {
            // Check if the object hit has an IGrabbable component
            IGrabbable grabbableObject = hit.collider.GetComponent<IGrabbable>();

            if (grabbableObject != null)
            {
                // Check if the hit object is already in the grabbed list
                GameObject hitObject = hit.collider.gameObject;
                if (!_grabbedObjects.Contains(hitObject))
                {
                    // If the object is not in the grabbed list, attempt to grab it
                    TryGrabObject(grabbableObject, hitObject);
                }
                else
                {
                    Debug.Log("Hit object is already grabbed: " + hitObject.name);
                }
            }
            else if (_grabbedObjects.Count > 0)
            {
                // Release the last grabbed object if no grabbable object is found
                ReleaseLastObject();
            }
        }
        else if (_grabbedObjects.Count > 0)
        {
            // Release the last grabbed object if the SphereCast doesn't hit anything
            ReleaseLastObject();
        }
    }

    private void TryGrabObject(IGrabbable grabbableObject, GameObject grabbedObject)
    {
        float distanceToPlayer = Vector3.Distance(transform.position, grabbedObject.transform.position);
        float objectWeight = grabbableObject.GetWeight();

        if (distanceToPlayer <= grabDistance && (_currentTotalWeight + objectWeight) <= maxGrabbableWeight)
        {
            _grabbedObjects.Add(grabbedObject);
            _currentTotalWeight += objectWeight;

            PositionObject(grabbedObject);
            
            
            grabbableObject.OnGrab();

            Debug.Log("Object grabbed: " + grabbedObject.name + " | Total Weight: " + _currentTotalWeight);
        }
        else
        {
            if (distanceToPlayer > grabDistance)
                Debug.Log("Object is too far to grab. Distance: " + distanceToPlayer);
            if (_currentTotalWeight + objectWeight > maxGrabbableWeight)
                Debug.Log("Total weight exceeds limit. Current: " + _currentTotalWeight + ", Max: " + maxGrabbableWeight);
        }
    }

    private void PositionObject(GameObject newObject)
    {
        if (_grabbedObjects.Count == 1)
        {
            newObject.transform.SetParent(heldPoint);
            newObject.transform.localPosition = Vector3.zero;
            newObject.transform.localRotation = Quaternion.identity;
        }
        else
        {
            RaycastHit hit;
            Vector3 origin = new Vector3(heldPoint.position.x, heldPoint.position.y + 100f, heldPoint.position.z);
            if (Physics.Raycast(origin, Vector3.down, out hit, Mathf.Infinity, grabbableObjectLayer))
            {
                newObject.transform.SetParent(heldPoint);
                newObject.transform.position = hit.point;
                newObject.transform.localRotation = Quaternion.identity;

                Debug.Log("Object stacked on top: " + newObject.name);
            }
        }
    }

    private void ReleaseLastObject()
    {
        if (_grabbedObjects.Count > 0)
        {
            GameObject lastObject = _grabbedObjects[_grabbedObjects.Count - 1];

            lastObject.transform.SetParent(null);

            IGrabbable grabbableObject = lastObject.GetComponent<IGrabbable>();
            if (grabbableObject != null)
            {
                grabbableObject.OnRelease();
            }

            _grabbedObjects.RemoveAt(_grabbedObjects.Count - 1);
            _currentTotalWeight -= grabbableObject.GetWeight();

            Debug.Log("Last object released: " + lastObject.name);
        }
    }

    private void SellGrabbedObjects()
    {
        if (_grabbedObjects.Count > 0)
        {
            GameObject lastObject = _grabbedObjects[_grabbedObjects.Count - 1];

            lastObject.transform.SetParent(null);

            IGrabbable grabbableObject = lastObject.GetComponent<IGrabbable>();

            _grabbedObjects.RemoveAt(_grabbedObjects.Count - 1);
            _currentTotalWeight -= grabbableObject.GetWeight();
            
            PointsCounter.Instance.SellObject(grabbableObject, lastObject);
        }
    }

    private Coroutine _sellCoroutine;

    private void CheckIfSellPointInBounds()
    {
        if (Vector3.Distance(transform.position, sellPoint.position) <= sellDistance)
        {
            _sellCoroutine = StartCoroutine(SellItemsWithDelay(10f));
        }
        else
        {
            if (_sellCoroutine != null)
            {
                StopCoroutine(_sellCoroutine);
            }
            _sellCoroutine = null;
        }
    }

    private IEnumerator SellItemsWithDelay(float interval)
    {
        while (_grabbedObjects.Count > 0)
        {
            SellGrabbedObjects();

            yield return new WaitForSeconds(interval);
        }
    }

    private void UpdateDebugGUI()
    {
        weightText.text = $"Weight: {_currentTotalWeight} / {maxGrabbableWeight}";
        soldValueText.text = $"Sold Value: {PointsCounter.Instance.Value} $";
    }
}